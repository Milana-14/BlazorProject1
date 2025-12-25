using Azure.AI.OpenAI;
using BlazorApp6.Services;
using Microsoft.AspNetCore.SignalR;
using OpenAI.Chat;
using System.ClientModel;
using System.Collections.Concurrent;
using System.Text;
using BlazorApp6.Models;
using Npgsql;

namespace BlazorApp6.Services
{
    public class AiChatManager
    {
        private readonly string connectionString;

        public AiChatManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
        }

        public async Task AddMessageAsync(AiMessage message)
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            string sql = @"INSERT INTO ""AiMessages"" (""Id"", ""SenderId"", ""SenderName"", ""Content"", ""IsFile"", ""FileName"", ""Timestamp"")
                           VALUES (@Id, @SenderId, @SenderName, @Content, @IsFile, @FileName, @Timestamp)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", message.Id);
            cmd.Parameters.AddWithValue("@SenderId", message.SenderId);
            cmd.Parameters.AddWithValue("@SenderName", message.SenderName);
            cmd.Parameters.AddWithValue("@Content", message.Content);
            cmd.Parameters.AddWithValue("@IsFile", message.IsFile);
            cmd.Parameters.AddWithValue("@FileName", (object?)message.FileName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Timestamp", message.Timestamp);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<AiMessage>> GetMessagesAsync()
        {
            var list = new List<AiMessage>();
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            string sql = @"SELECT ""Id"", ""SenderId"", ""SenderName"", ""Content"", ""IsFile"", ""FileName"", ""Timestamp"" FROM ""AiMessages"" ORDER BY ""Timestamp"" ASC";

            using var cmd = new NpgsqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                list.Add(new AiMessage
                {
                    Id = reader.GetGuid(0),
                    SenderId = reader.GetGuid(1),
                    SenderName = reader.GetString(2),
                    Content = reader.GetString(3),
                    IsFile = reader.GetBoolean(4),
                    FileName = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Timestamp = reader.GetDateTime(6)
                });
            }

            return list;
        }
    }

    public interface IAiChatClient
    {
        Task ReceiveMessage(Guid id, Guid senderId, string senderName, string message);
    }

    public class AiChatHub : Hub<IAiChatClient>
    {
        private readonly AiChatService ai;
        private readonly AiChatManager aiDb;

        private static readonly Guid BotId = Guid.Empty;

        public AiChatHub(AiChatService ai, AiChatManager aiDb)
        {
            this.ai = ai;
            this.aiDb = aiDb;
        }
        public override async Task OnConnectedAsync()
        {
            var messages = await aiDb.GetMessagesAsync();
            foreach (var m in messages)
            {
                string content = m.IsFile
                    ? $"[Файл] <a href='/ai-files/{m.FileName}' target='_blank'>{m.FileName}</a>"
                    : m.Content;

                await Clients.Caller.ReceiveMessage(m.Id, m.SenderId, m.SenderName, content);
            }

            await ai.AskAsync(Context.ConnectionId, "");

            await base.OnConnectedAsync();
        }


        public async Task SendMessage(string userName, Guid userId, string message)
        {
            var aiMessage = new AiMessage
            {
                Id = Guid.NewGuid(),
                SenderId = userId,
                SenderName = userName,
                Content = message,
                IsFile = false,
                FileName = null,
                Timestamp = DateTime.Now
            };
            await aiDb.AddMessageAsync(aiMessage);

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

        public async Task SendFile(string userName, Guid userId, string fileName, byte[] fileBytes)
        {
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "ai-files");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);
            await File.WriteAllBytesAsync(filePath, fileBytes);

            var fileUrl = $"/ai-files/{fileName}";
            var aiMessage = new AiMessage
            {
                Id = Guid.NewGuid(),
                SenderId = userId,
                SenderName = userName,
                Content = fileUrl,
                IsFile = true,
                FileName = fileName,
                Timestamp = DateTime.Now
            };

            await aiDb.AddMessageAsync(aiMessage);

            var messageContent = $"[Файл] <a href='{fileUrl}' target='_blank'>{fileName}</a>";

            await Clients.Caller.ReceiveMessage(Guid.NewGuid(), userId, userName, messageContent);

            var userMessage = $"Потребителят е качил файл: {fileName}";
            var aiResponse = await ai.AskAsync(Context.ConnectionId, userMessage);

            await Clients.Caller.ReceiveMessage(Guid.NewGuid(), Guid.Empty, "AI Учител", aiResponse);
        }


        public override Task OnDisconnectedAsync(Exception? exception)
        {
            ai.ClearHistory(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }

    public class AiChatService
    {
        private readonly ChatClient chatClient;

        private readonly ConcurrentDictionary<string, List<ChatMessage>> histories = new();

        private readonly AiChatManager aiDb;
        public AiChatService(IConfiguration config, AiChatManager aiDb)
        {
            var token = Environment.GetEnvironmentVariable("EDUSWAPS_AI_TOKEN");

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("Токенът за искуствения интелект не е конфигуриран. Задайте EDUSWAPS_AI_TOKEN.");
            }

            var endpoint = new Uri("https://models.inference.ai.azure.com");
            var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(token));

            chatClient = client.GetChatClient("gpt-4o");

            this.aiDb = aiDb;
        }

        private async Task<List<ChatMessage>> GetHistoryAsync(string connectionId)
        {
            if (histories.TryGetValue(connectionId, out var memHistory))
                return memHistory;

            var history = new List<ChatMessage>
            {
                new SystemChatMessage(
                    "Ти си внимателен и търпелив гимназиален учител." +
                    "Твоята цел е да помагаш на ученика да разбере учебния материал." +
                    "Обяснявай стъпка по стъпка, с примери и насочващи въпроси." +
                    "Опитвай се да насочиш ученика към правилния отговор, без да му го даващ"
                )
            };

            var messages = await aiDb.GetMessagesAsync();
            foreach (var msg in messages)
            {
                if (msg.IsFile)
                    history.Add(new UserChatMessage($"[Файл] {msg.FileName}"));
                else
                    history.Add(new UserChatMessage(msg.Content));
            }

            histories[connectionId] = history;
            return history;
        }

        public async Task<string> AskAsync(string connectionId, string userMessage)
        {
            var history = await GetHistoryAsync(connectionId);

            history.Add(new UserChatMessage(userMessage));

            var sb = new StringBuilder();

            await foreach (var update in chatClient.CompleteChatStreamingAsync(history))
            {
                foreach (var part in update.ContentUpdate)
                    if (!string.IsNullOrEmpty(part.Text))
                        sb.Append(part.Text);
            }

            var fullResponse = sb.ToString();
            history.Add(new AssistantChatMessage(fullResponse));

            if (history.Count > 50)
                history.RemoveAt(1);

            return fullResponse;
        }

        public void ClearHistory(string connectionId)
        {
            histories.TryRemove(connectionId, out _);
        }
    }
}