using BlazorApp6.Models;
using OpenAI.Chat;
using OpenAI.Moderations;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class AiModerationService // Когато ученик изпрати съобщение, то ИИ-то ще го анализира за токсичност и фактологични грешки.
    {
        private readonly ChatClient chatClient;

        public async Task<Models.ModerationResult> CheckMessage(List<Message> previousMessages, Message message, SubjectEnum subject)
        {
            previousMessages = previousMessages.TakeLast(3).ToList();

            if (message.Content.Trim().Length < 6)
            {
                return new Models.ModerationResult
                {
                    Toxic = 0,
                    Error = 0,
                    Suggestion = null
                };
            }

            var studentsPreviousMessages = string.Join("\n", previousMessages.Select(m => $"[context_message]\n{m.Content}\n[/context_message]"));
            var studentsMessage = $"[message_to_analyze]\n{message.Content}\n[/message_to_analyze]";

            var systemPrompt = $"""Анализирай съобщението на ученика, като използваш контекста на предходните 3 съобщения, към друг ученик в рамките на една учебна сесия между двама ученици по предмет {subject.GetDisplayName().ToLower()}.""" +
                            "\nАнализирай САМО [message_to_analyze]. Контекстните съобщения са само за разбиране на разговора.\n" +
                            "Ако съобщението съдържа само въпрос, задай factual_error = 0.00\n\n" +

                            "\n\nОпредели вероятността (стойност между 0 и 1 като десетично число с максимум два знака след точката) за това дали съобщението съдържа:\n" +
                            "1) toxic - неуместно съдържание като обиди, заплахи, насилие, сексуално съдържание или нецензурни думи.\n" +
                            "2) factual_error - дали има фактологична грешка в обяснение на учебния материал\n" +
                            "Ако съобщението не съдържа обяснение на учебен материал,\r\nзадай factual_error = 0.00.\n\n" +

                            "Интерпретация на стойностите:\n" +
                            "0.00 – няма проблем\n0.10 – много малка вероятност\n0.30 – възможен проблем\n0.50 – вероятен проблем\n0.80 – силно вероятен проблем\n1.00 – сигурен проблем\n\n" +

                            "Не маркирай toxic за:\n" +
                            "- приятелски шеги\n" +
                            "- неформален ученически език\n" +
                            "- лек сарказъм без обиди\n\n" +

                            "factual_error означава реална фактологична грешка в учебния материал. Не маркирай factual_error ако:\n" +
                            "- обяснението е просто опростено\n" +
                            "- липсват подробности\n" +
                            "- ученикът използва разговорен език\n\n" +

                            "Ако factual_error >= 0.30, предложи кратка корекция, обръщайки се директно към ученика.\n" +
                            "Ако factual_error< 0.30, върни \"suggestion\": null.\n" +
                            "Не измисляй фактологични грешки. Ако не си сигурен дали твърдението е грешно, то задай factual_error <= 0.20.\n" +
                            "Значенията на toxic и factual_error трябва да бъдат десетични между 0.00 и 1.00 с максимум два знака след точката.\n" +

                            "suggestion трябва да бъде максимум 200 символа. Пиши пояснително, кратко, ясно и директно към ученика. Не добавяй излишни обяснения. Коригирай само конкретната фактологична грешка.\n" +
                            "Върни САМО валиден JSON без никакъв допълнителен текст (Без текст. Без обяснения. Без Markdown). Отговорът трябва да започва с { и да завършва с }.\n\n" +

                            "Формат:\n" +
                            "{\n" +
                            "\"toxic\": 0.00,\n" +
                            "\"factual_error\": 0.00,\n" +
                            "\"suggestion\": \"Текст\" или null\n" +
                            "}";
            var studentsMessagesPrompt = "\nКонтекст на разговора:\n" +
                                         $"""{studentsPreviousMessages}""" +
                                         "\n\nСъобщение(я) от ученика, което трябва да превериш:\n" +
                                         $"""{studentsMessage}""";

            var options = new ChatCompletionOptions 
            { 
                Temperature = 0,
                MaxOutputTokenCount = 120
            };
            var response = await chatClient.CompleteChatAsync(new ChatMessage[] { ChatMessage.CreateSystemMessage(systemPrompt),
                                                              ChatMessage.CreateUserMessage(studentsMessagesPrompt) }, 
                                                              options);

            var preContent = response.Value.Content.FirstOrDefault()?.Text ?? "";
            string content;

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
                var moderation = JsonSerializer.Deserialize<Models.ModerationResult>(content);
                moderation.MessageId = message.Id;
                return moderation;
            }
            catch (JsonException)
            {
                return new Models.ModerationResult
                {
                    MessageId = Guid.Empty,
                    Toxic = 0,
                    Error = 0,
                    Suggestion = "Грешка при анализа на съобщението. Моля, опитайте отново."
                };
            }
        }


        // Когато ученикът си редактира съобщението, пак го изпращам (но чрез друга логика) към OpenAI API-то.
        // Ако токсичността е намаляла, то да се премахне единия waring, ако е увеличила, да се добави един warning.
        // Ако ученикът е редактирам съобщението си със грешното си твърдение, то пак ИИ-то да го провери и ако е коригирано, да се премахне корекцията от ИИ-то, ако не е корегирано, то да се запази корекцията от ИИ-то.

    }
}