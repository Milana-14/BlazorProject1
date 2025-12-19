using Microsoft.AspNetCore.SignalR;
using BlazorApp6.Services;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text;

namespace BlazorApp6.Hubs
{
    public interface IAiChatClient
    {
        Task ReceiveMessage(Guid id, Guid senderId, string senderName, string message);
    }

    public class AiChatHub : Hub<IAiChatClient>
    {
        private readonly AiChatService ai;

        private static readonly Guid BotId = Guid.Empty;

        public AiChatHub(AiChatService ai)
        {
            this.ai = ai;
        }

        public async Task SendMessage(string userName, Guid userId, string message)
        {
            await Clients.Caller.ReceiveMessage(
                Guid.NewGuid(),
                userId,
                userName,
                message
            );

            var aiResponse = await ai.AskAsync(message);

            await Clients.Caller.ReceiveMessage(
                Guid.NewGuid(),
                BotId,
                "AI Учител",
                aiResponse
            );
        }
    }
}

namespace BlazorApp6.Services
{
    public class AiChatService
    {
        private readonly ChatClient chatClient;

        private readonly List<ChatMessage> conversationHistory = new()
        {
            new SystemChatMessage(
                "Ти си внимателен и търпелив гимназиален учител." +
                "Твоята цел е да помагаш на ученика да разбере учебния материал." +
                "Обяснявай стъпка по стъпка, с примери и насочващи въпроси."
            )
        };

        public AiChatService(IConfiguration config)
        {
            var token = config[""];

            var endpoint = new Uri("https://models.inference.ai.azure.com");
            var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(token));

            chatClient = client.GetChatClient("gpt-4o");
        }

        public async Task<string> AskAsync(string userMessage)
        {
            conversationHistory.Add(new UserChatMessage(userMessage));

            var updates = chatClient.CompleteChatStreamingAsync(conversationHistory);

            var sb = new StringBuilder();

            await foreach (var update in updates)
            {
                foreach (var part in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(part.Text))
                        sb.Append(part.Text);
                }
            }

            var fullResponse = sb.ToString();

            conversationHistory.Add(new AssistantChatMessage(fullResponse));

            if (conversationHistory.Count > 11)
                conversationHistory.RemoveAt(1);

            return fullResponse;
        }
    }
}