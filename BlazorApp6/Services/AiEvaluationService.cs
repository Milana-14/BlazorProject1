using Azure.AI.OpenAI;
using BlazorApp6.Models;
using OpenAI.Chat;
using System.ClientModel;
using System.Collections.Concurrent;

namespace BlazorApp6.Services
{
    public class AiEvaluationService // Когато свапът се завършва, то ИИ-то ще анализира чата, ще генерира въпроси и ще оцени нивото на обяснение и разбиране
    {
        private readonly ChatClient chatClient;
        private readonly ConcurrentDictionary<string, List<ChatMessage>> histories = new();
        private readonly AiChatManager aiDb;
        private readonly StudentManager studentManager;
        private readonly ChatManager chatManager;
        private readonly SwapManager swapManager;

        public AiEvaluationService(IConfiguration config, AiChatManager aiDb, StudentManager studentManager, ChatManager chatManager, SwapManager swapManager)
        {
            var token = config["EDUSWAPS_AI_TOKEN"];
            if (string.IsNullOrWhiteSpace(token))
                throw new Exception("AI токенът не е зададен. EDUSWAPS_AI_TOKEN");

            var endpoint = new Uri("https://models.inference.ai.azure.com");
            var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(token));

            chatClient = client.GetChatClient("gpt-4o");
            this.aiDb = aiDb;
            this.studentManager = studentManager;
            this.chatManager = chatManager;
            this.swapManager = swapManager;
        }

        //public async Task<> HandleChatChecking()
        //{

        //}

        //public async Task<> CheckChat()
        //{
        //    // Тук ИИ-то ще анализира целия чат, ще оцени дали обясненията са били ясни, дали са били задавани въпроси и дали ученикът е разбрал. Ще се вземат предвид и токсичните съобщения, ако има такива.
        //}

        //public async Task<> GenerateQuestions()
        //{
        //}

        //public async Task<> EvaluateUnderstanding()
        //{
    }
}
