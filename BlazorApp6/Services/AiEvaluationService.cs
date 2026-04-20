using Azure.AI.OpenAI;
using BlazorApp6.Models;
using MudBlazor;
using OpenAI.Chat;
using System.ClientModel;
using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace BlazorApp6.Services
{
    public class AiEvaluationService // Когато свапът се завършва, то ИИ-то ще анализира чата, ще генерира въпроси и ще оцени нивото на обяснение и разбиране
    {
        private readonly ChatClient chatClient;
        private readonly ChatManager chatManager;

        public AiEvaluationService(IConfiguration config, ChatManager chatManager)
        {
            var token = config["EDUSWAPS_AI_TOKEN"];
            if (string.IsNullOrWhiteSpace(token))
                throw new Exception("AI токенът не е зададен. EDUSWAPS_AI_TOKEN");

            var endpoint = new Uri("https://models.inference.ai.azure.com");
            var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(token));

            chatClient = client.GetChatClient("gpt-4o");
            this.chatManager = chatManager;
        }

        public async Task<Models.AiQuestions> GenerateQuestions(Swap swap)
        {
            List<Message> messages = chatManager.GetMessagesFromDb(swap.Id).OrderBy(m => m.Timestamp).ToList();

            string wholeChat = string.Empty;

            foreach (var msg in messages)
            {
                if (swap.Student1Id == msg.SenderId)
                {
                    wholeChat += $"Получаващ помощ: {msg.Content}\n";
                }
                else if (swap.Student2Id == msg.SenderId)
                {
                    wholeChat += $"Обясняващ: {msg.Content}\n";
                }
            }

        var systemPrompt = $"""Анализирай целия чат между двама ученици в рамките на една учебна сесия по предмет {swap.SubjectForHelp.GetDisplayName().ToLower()}.\n""" +

        "В чата има два типа участници:" +
        "- \"Обясняващ\" – ученикът, който помага и обяснява материала" +
        "- \"Получаващ помощ\" – ученикът, който задава въпроси и учи" + 

        "Твоята задача е да генерираш 3 въпроса, които да проверяват дали \"Получаващ помощ\" е разбрал обясненията." +

        "ВАЖНО:" +
        "- Въпросите трябва да са базирани САМО на информация от чата" +
        "- НЕ измисляй нова информация извън разговора и винаги използвай само верните твърдения от чата" +
        "- Въпросите трябва да проверяват реално разбиране, а не запаметяване" +
        "- Избягвай прекалено лесни въпроси (напр. \"Какво е...?\" ако вече е директно казано)" +
        "- Предпочитай въпроси, които изискват мислене (разбиране, приложение, логика)" +

        "За всеки въпрос:" +
        "- Дай точно 3 отговора (A, B, C), от които само един трябва да е правилен" +
        "- Грешните отговори трябва да звучат правдоподобно (не очевидно грешни)" +

        "Изисквания:" +
        "- Въпросите да са кратки и ясни (до ~150 символа)" +
        "- Отговорите да са кратки (до ~100 символа)" +
        "- CorrectAnswer трябва да бъде число:" +
            "1 = A" +
            "2 = B" +
            "3 = C" +

        "Ако чатът е твърде кратък или няма достатъчно учебно съдържание:" +
        "- генерирай по-прости въпроси, но пак свързани с разговора" +

        "Върни САМО валиден JSON без никакъв допълнителен текст (Без текст. Без обяснения. Без Markdown). Отговорът трябва да започва с { и да завършва с }." +

        "Формат:" +
        "{" +
          "\"Question1\": \"Текст\"," +
          "\"OptionA1\": \"Текст\"," +
          "\"OptionB1\": \"Текст\"," +
          "\"OptionC1\": \"Текст\"," +
          "\"CorrectAnswer1\": 1," +

          "\"Question2\": \"Текст\"," +
          "\"OptionA2\": \"Текст\"," +
          "\"OptionB2\": \"Текст\"," +
          "\"OptionC2\": \"Текст\"," +
          "\"CorrectAnswer2\": 2," +

         "\"Question3\": \"Текст\"," +
         "\"OptionA3\": \"Текст\"," +
         "\"OptionB3\": \"Текст\"," +
         "\"OptionC3\": \"Текст\"," +
         "\"CorrectAnswer3\": 3" +
        "}";

            var studentsWholeChatPrompt = $"\nТова е целият чат между двамата ученици:\n[whole_chat_to_analyze]{wholeChat}[/whole_chat_to_analyze]";

            var options = new ChatCompletionOptions
            {
                Temperature = 0,
                MaxOutputTokenCount = 400
            };

            var response = await chatClient.CompleteChatAsync(new ChatMessage[] { ChatMessage.CreateSystemMessage(systemPrompt),
                                                              ChatMessage.CreateUserMessage(studentsWholeChatPrompt) }, options);
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
                var questions = JsonSerializer.Deserialize<Models.AiQuestions>(content);
                questions.SwapId = swap.Id;
                return questions;
            }
            catch (JsonException)
            {
                return new Models.AiQuestions
                {
                    SwapId = Guid.Empty,
                    Question1 = "Грешка при анализа на съобщението. Моля, опитайте отново.",
                    OptionA1 = "",
                    OptionB1 = "",
                    OptionC1 = "",
                    CorrectAnswer1 = 0,
                    Question2 = "",
                    OptionA2 = "",
                    OptionB2 = "",
                    OptionC2 = "",
                    CorrectAnswer2 = 0,
                    Question3 = "",
                    OptionA3 = "",
                    OptionB3 = "",
                    OptionC3 = "",
                    CorrectAnswer3 = 0
                };
            }
        }

        public async Task<AiEvaluation> EvaluateQuality(Swap swap, int correctAnswersCount)
        {
            List<Message> messages = chatManager.GetMessagesFromDb(swap.Id).OrderBy(m => m.Timestamp).ToList();

            string wholeChat = string.Empty;

            foreach (var msg in messages)
            {
                if (swap.Student1Id == msg.SenderId)
                {
                    wholeChat += $"Получаващ помощ: {msg.Content}\n";
                }
                else if (swap.Student2Id == msg.SenderId)
                {
                    wholeChat += $"Обясняващ: {msg.Content}\n";
                }
            }

            var systemPrompt = "Анализирай целия чат между двама ученици в рамките на една учебна сесия по предмет " + swap.SubjectForHelp.GetDisplayName().ToLower() + ".\n\n" +

            "\nВ чата има два типа участници:\n" +
            "- \"Обясняващ\" – ученикът, който помага и обяснява материала\n" +
            "- \"Получаващ помощ\" – ученикът, който задава въпроси и учи\n" +

            "\nТвоята задача е да оцениш качеството на обучението чрез два показателя:\n" +
            "1. ExplanationClarity (яснота на обяснението)\n" +
            "2. UnderstandingLevel (ниво на разбиране)\n" +

            "\nВАЖНО:\n" +

            "\nExplanationClarity:\n" +
            "- Оцени колко ясно, логично и разбираемо \"Обясняващ\" обяснява\n" +
            "- Вземи предвид:\n" +
            "  - дали дава примери\n" +
            "  - дали обяснява стъпка по стъпка\n" +
            "  - дали отговаря на въпросите адекватно\n" +
            "  - дали избягва объркващи или грешни твърдения\n" +
            "- Стойност между 0 и 10:\n" +
            "  - 0 = напълно неясно / грешно\n" +
            "  - 5 = частично ясно\n" +
            "  - 10 = много ясно и добре обяснено\n" +

            "\nUnderstandingLevel:\n" +
            "- Оцени доколко \"Получаващ помощ\" е разбрал материала\n" +
            "- Вземи предвид:\n" +
            "  - въпросите, които задава\n" +
            "  - дали показва разбиране в отговорите си\n" +
            "  - дали прави логически изводи\n" +
            "- ЗАДЪЛЖИТЕЛНО вземи предвид correctAnswersCount като основен фактор:\n" +
            "  - 0 верни → ниско разбиране (~0–3)\n" +
            "  - 1 верен → частично (~3–5)\n" +
            "  - 2 верни → добро (~5–8)\n" +
            "  - 3 верни → много добро (~8–10)\n" +

            "\nДопълнителни фактори:\n" +
            "- Ако има токсични съобщения → леко намали оценките\n" +
            "- Ако чатът е много кратък → не давай високи оценки\n" +
            "- Ако обясненията са повърхностни → намали ExplanationClarity\n" +

            "\nИЗИСКВАНИЯ:\n" +
            "- Върни САМО валиден JSON без никакъв допълнителен текст (Без текст. Без обяснения. Без Markdown). Отговорът трябва да започва с { и да завършва с }.\n" +
            "- Стойностите трябва да са числа между 0 и 10 (int)\n" +

            "\nФормат:\n" +
            "{\n" +
            "  \"ExplanationClarity\": 0,\n" +
            "  \"UnderstandingLevel\": 0\n" +
            "}";

            var studentsWholeChatPrompt = $"\nТова е целият чат между двамата ученици:\n[whole_chat_to_analyze]{wholeChat}[/whole_chat_to_analyze]" +
                $"\nБрой правилни отговори на въпросите: {correctAnswersCount}";

            var options = new ChatCompletionOptions
            {
                Temperature = 0,
                MaxOutputTokenCount = 200
            };

            var response = await chatClient.CompleteChatAsync(new ChatMessage[] { ChatMessage.CreateSystemMessage(systemPrompt),
                                                              ChatMessage.CreateUserMessage(studentsWholeChatPrompt) }, options);

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
                var evaluation = JsonSerializer.Deserialize<Models.AiEvaluation>(content);
                evaluation.SwapId = swap.Id;
                return evaluation;
            }
            catch (JsonException)
            {
                return new AiEvaluation
                {
                    SwapId = swap.Id,
                    ExplanationClarity = 0,
                    UnderstandingLevel = 0
                };
            }
        }
    }
}


// Тук ИИ-то ще анализира целия чат, ще оцени дали обясненията са били ясни, дали са били задавани въпроси и дали ученикът е разбрал.
// Ще се вземат предвид и токсичните съобщения, ако има такива.

// 1. ExplanationClarity

// 2. GenerateQuestions

// 3. UnderstandingLevel