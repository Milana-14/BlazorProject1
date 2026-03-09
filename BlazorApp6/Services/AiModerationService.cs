using BlazorApp6.Models;
using OpenAI.Chat;
using OpenAI.Moderations;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class AiModerationService
    {
        private readonly ChatClient chatClient;

        public async Task<Models.ModerationResult> CheckMessage(List<Message> messages, SubjectEnum subject)
        {
            var studentsMessages = string.Join("\n", messages.Select(m => $"[message]\n{m.Content}\n[/message]"));// Ще изпращам само поредица от няколко (или 1) съобщения, които един ученик изпратил едно след друго, за да не изпращам твърде много заявки към OpenAI API-то.

            var systemPrompt = $"""Анализирай всички съобщения(е) на ученика като едно цяло към друг ученик в рамките на една учебна сесия между двама ученици по предмет {subject.GetDisplayName().ToLower()}.""" +

                            "\n\nОпредели вероятността стойност между 0 и 1 като десетично число с максимум два знака след точката) за това дали съобщението(та) съдържа(т):\n" +
                            "1) toxic - неуместно съдържание като обиди, заплахи, насилие, сексуално съдържание или нецензурни думи.\n" +
                            "2) factual_error - дали има фактологична грешка в обяснение на учебния материал\n\n" +

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
            var studentsMessagesPrompt = "\nСъобщение(я) от ученика:\n" +
                                        $"""{studentsMessages}""";

            var options = new ChatCompletionOptions { Temperature = 0 };
            var response = await chatClient.CompleteChatAsync(new ChatMessage[] { ChatMessage.CreateSystemMessage(systemPrompt),
                                                              ChatMessage.CreateUserMessage(studentsMessagesPrompt) }, 
                                                              options);

            var preContent = response.Value.Content[0].Text;
            var content = preContent.StartsWith("{") && preContent.EndsWith("}")
                ? preContent
                : preContent.Substring(preContent.IndexOf('{'), preContent.LastIndexOf('}') - preContent.IndexOf('{') + 1);

            try
            {
                var moderation = JsonSerializer.Deserialize<Models.ModerationResult>(content);
                return moderation;
            }
            catch (JsonException)
            {
                return new Models.ModerationResult
                {
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