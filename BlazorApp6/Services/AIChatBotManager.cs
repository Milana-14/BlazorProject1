using Azure.AI.OpenAI;
using BlazorApp6.Services;
using Microsoft.AspNetCore.SignalR;
using OpenAI.Chat;
using System.ClientModel;
using System.Collections.Concurrent;
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

            var response = await ai.AskAsync(Context.ConnectionId, message);

            await Clients.Caller.ReceiveMessage(
                Guid.NewGuid(),
                BotId,
                "AI Учител",
                response
            );
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            ai.ClearHistory(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }
}

namespace BlazorApp6.Services
{
    public class AiChatService
    {
        private readonly ChatClient chatClient;

        private readonly ConcurrentDictionary<string, List<ChatMessage>> histories = new();

        private List<ChatMessage> GetHistory(string connectionId)
        {
            return histories.GetOrAdd(connectionId, _ => new List<ChatMessage>
            {
                new SystemChatMessage(
                "Ти си внимателен и търпелив гимназиален учител." +
                "Твоята цел е да помагаш на ученика да разбере учебния материал." +
                "Обяснявай стъпка по стъпка, с примери и насочващи въпроси." +
                "Опитвай се да насочиш ученика към правилния отговор, без да му го даващ"
                )
            });
        }

        public AiChatService(IConfiguration config)
        {
            var token = Environment.GetEnvironmentVariable("EDUSWAPS_AI_TOKEN");

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("Токенът за искуственния интелект не е конфигуриран. Задайте променлива на средата EDUSWAPS_AI_TOKEN.");
            }

            var endpoint = new Uri("https://models.inference.ai.azure.com");
            var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(token));

            chatClient = client.GetChatClient("gpt-4o");
        }

        public async Task<string> AskAsync(string connectionId, string userMessage)
        {
            var history = GetHistory(connectionId);

            history.Add(new UserChatMessage(userMessage));

            var updates = chatClient.CompleteChatStreamingAsync(history);

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
            history.Add(new AssistantChatMessage(fullResponse));

            if (history.Count > 11)
                history.RemoveAt(1);

            return fullResponse;
        }

        public void ClearHistory(string connectionId)
        {
            histories.TryRemove(connectionId, out _);
        }
    }
}