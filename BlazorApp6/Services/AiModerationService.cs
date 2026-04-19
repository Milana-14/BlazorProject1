using Azure.AI.OpenAI;
using BlazorApp6.Models;
using OpenAI.Chat;
using OpenAI.Moderations;
using System.ClientModel;
using System.Collections.Concurrent;
using System.Text.Json;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace BlazorApp6.Services
{
    public class AiModerationService // Когато ученик изпрати съобщение, то ИИ-то ще го анализира за токсичност и фактологични грешки.
    {
        private readonly ChatClient chatClient;
        private readonly ConcurrentDictionary<string, List<ChatMessage>> histories = new();
        private readonly ChatManager chatManager;
        private readonly SwapManager swapManager;

        public AiModerationService(IConfiguration config, ChatManager chatManager, SwapManager swapManager)
        {
            var token = config["EDUSWAPS_AI_TOKEN"];
            if (string.IsNullOrWhiteSpace(token))
                throw new Exception("AI токенът не е зададен. EDUSWAPS_AI_TOKEN");

            var endpoint = new Uri("https://models.inference.ai.azure.com");
            var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(token));

            chatClient = client.GetChatClient("gpt-4o");
            this.chatManager = chatManager;
            this.swapManager = swapManager;
        }

        public async Task<Models.AiModerationMessage> HandleMessageChecking(Swap swap, StudentToConnect student, MessageToSend message)
        {
            var previous = chatManager.GetMessagesFromDb(swap.Id).TakeLast(3).ToList();

            var msgForCheck = new Message
            {
                Id = message.Id,
                SwapId = swap.Id,
                SenderId = student.Id,
                Content = message.Content,
                Timestamp = DateTime.UtcNow,
                ReplyToMessageId = message.ReplyToMessage
            };
            var subject = swap?.SubjectForHelp ?? SubjectEnum.NotSpecified;

           var moderationResult = await CheckMessage(previous, msgForCheck, subject);

            if (moderationResult.Toxic >= 0.5 && swap.ToxicMessagesCount <= 3)
            {
                swapManager.SwapToxicWarning(swap);
                moderationResult.ToxicWarning = $"[AI moderation] токсичност: {Math.Round(moderationResult.Toxic * 100)}%. " +
                    $"При още {3 - swap.ToxicMessagesCount} {(((3 - swap.ToxicMessagesCount) == 1)? "нарушение" : "нарушения")} свапът ще се затвори автоматично. " +
                    $"Моля, изтрийте или редактирайте съобщението.";
            }
            else if (moderationResult.Toxic >= 0.5 && swap.ToxicMessagesCount >= 3)
            {
                swapManager.SwapToxicWarning(swap);
                moderationResult.ToxicWarning = $"[AI moderation] токсичност: {Math.Round(moderationResult.Toxic * 100)}%. " +
                    $"Свапът е затворен автоматично поради многократни нарушения.";
                moderationResult.LastToxicWarning = true;
            }

            return moderationResult;
        }

        public async Task<Models.AiModerationMessage> CheckMessage(List<Message> previousMessages, Message message, SubjectEnum subject)
        {
            if (message.Content.Trim().Length < 4)
            {
                return new Models.AiModerationMessage
                {
                    MessageId = Guid.Empty,
                    Toxic = 0,
                    FactualError = 0,
                    Suggestion = null
                };
            }

            var studentsPreviousMessages = string.Join("\n", previousMessages.Select(m => $"[context_message]\n{m.Content}\n[/context_message]"));
            var studentsMessage = $"[message_to_analyze]\n{message.Content}\n[/message_to_analyze]";

            var systemPrompt = $"""Анализирай съобщението на ученика, като използваш контекста на предходните 3 съобщения, към друг ученик в рамките на една учебна сесия между двама ученици по предмет {subject.GetDisplayName().ToLower()}.""" +
                            "\nАнализирай САМО [message_to_analyze]. Контекстните съобщения са само за разбиране на разговора.\n" +
                            "Ако съобщението съдържа само въпрос, задай FactualError = 0.00\n\n" +

                            "\n\nОпредели вероятността (стойност между 0 и 1 като десетично число с максимум два знака след точката) за това дали съобщението съдържа:\n" +
                            "1) Toxic - неуместно съдържание като обиди, заплахи, насилие, сексуално съдържание или нецензурни думи.\n" +
                            "2) FactualError - дали има фактологична грешка в обяснение на учебния материал\n" +
                            "Ако съобщението не съдържа обяснение на учебен материал,\r\nзадай FactualError = 0.00.\n\n" +

                            "Интерпретация на стойностите:\n" +
                            "0.00 – няма проблем\n0.10 – много малка вероятност\n0.30 – възможен проблем\n0.50 – вероятен проблем\n0.80 – силно вероятен проблем\n1.00 – сигурен проблем\n\n" +

                            "Не маркирай Toxic за:\n" +
                            "- приятелски шеги\n" +
                            "- неформален ученически език\n" +
                            "- лек сарказъм без обиди\n\n" +

                            "FactualError означава реална фактологична грешка в учебния материал. Не маркирай FactualError ако:\n" +
                            "- обяснението е просто опростено\n" +
                            "- липсват подробности\n" +
                            "- ученикът използва разговорен език\n\n" +

                            "Ако FactualError >= 0.30, предложи кратка корекция, обръщайки се директно към ученика.\n" +
                            "Ако FactualError< 0.30, върни \"Suggestion\": null.\n" +
                            "Не измисляй фактологични грешки. Ако не си сигурен дали твърдението е грешно, то задай FactualError <= 0.20.\n" +
                            "Значенията на Toxic и FactualError трябва да бъдат десетични между 0.00 и 1.00 с максимум два знака след точката.\n" +

                            "Suggestion трябва да бъде максимум 200 символа. Пиши пояснително, кратко, ясно и директно към ученика. Не добавяй излишни обяснения. Коригирай само конкретната фактологична грешка.\n" +
                            "Върни САМО валиден JSON без никакъв допълнителен текст (Без текст. Без обяснения. Без Markdown). Отговорът трябва да започва с { и да завършва с }.\n\n" +

                            "Формат:\n" +
                            "{\n" +
                            "\"Toxic\": 0.00,\n" +
                            "\"FactualError\": 0.00,\n" +
                            "\"Suggestion\": \"Текст\" или null\n" +
                            "}";
            var studentsMessagesPrompt = "\nКонтекст на разговора:\n" +
                                         $"""{studentsPreviousMessages}""" +
                                         "\n\nСъобщение(я) от ученика, което трябва да превериш:\n" +
                                         $"""{studentsMessage}""";

            var options = new ChatCompletionOptions
            {
                Temperature = 0,
                MaxOutputTokenCount = 200
            };

            var response = await chatClient.CompleteChatAsync(new ChatMessage[] { ChatMessage.CreateSystemMessage(systemPrompt),
                                                              ChatMessage.CreateUserMessage(studentsMessagesPrompt) },
                                                              options);

            var preContent = response.Value.Content.FirstOrDefault()?.Text ?? "";
            string content;

            Console.WriteLine(preContent); ///////////////////////////////////

            var start = preContent.IndexOf('{');
            var end = preContent.LastIndexOf('}');

            if (start >= 0 && end > start)
            {
                content = preContent.Substring(start, end - start + 1);
            }
            else
            {
                throw new Exception("Invalid AI response");
            }

            try
            {
                var moderation = JsonSerializer.Deserialize<Models.AiModerationMessage>(content);
                moderation.MessageId = message.Id;
                return moderation;
            }
            catch (JsonException)
            {
                return new Models.AiModerationMessage
                {
                    MessageId = Guid.Empty,
                    Toxic = 0,
                    FactualError = 0,
                    Suggestion = "Грешка при анализа на съобщението. Моля, опитайте отново."
                };
            }
        }


        // Когато ученикът си редактира съобщението, пак го изпращам (но чрез друга логика) към OpenAI API-то.
        // Ако ученикът е редактирам съобщението си със грешното си твърдение, то пак ИИ-то да го провери и ако е коригирано, да се премахне корекцията от ИИ-то, ако не е корегирано, то да се запази корекцията от ИИ-то.

    }
}