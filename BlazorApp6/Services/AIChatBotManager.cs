using Azure.AI.OpenAI;
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

            var sql = @"
INSERT INTO ""AiMessages""
(""Id"", ""StudentId"", ""SenderId"", ""SenderName"", ""Content"", ""IsFile"", ""FileName"", ""Timestamp"")
VALUES
(@Id, @StudentId, @SenderId, @SenderName, @Content, @IsFile, @FileName, @Timestamp)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", message.Id);
            cmd.Parameters.AddWithValue("@StudentId", message.StudentId);
            cmd.Parameters.AddWithValue("@SenderId", message.SenderId);
            cmd.Parameters.AddWithValue("@SenderName", message.SenderName);
            cmd.Parameters.AddWithValue("@Content", message.Content);
            cmd.Parameters.AddWithValue("@IsFile", message.IsFile);
            cmd.Parameters.AddWithValue("@FileName", (object?)message.FileName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Timestamp", message.Timestamp);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<AiMessage>> GetMessagesAsync(Guid studentId)
        {
            var list = new List<AiMessage>();
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
SELECT ""Id"", ""StudentId"", ""SenderId"", ""SenderName"", ""Content"", ""IsFile"", ""FileName"", ""Timestamp""
FROM ""AiMessages""
WHERE ""StudentId"" = @StudentId
ORDER BY ""Timestamp""";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@StudentId", studentId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new AiMessage
                {
                    Id = reader.GetGuid(0),
                    StudentId = reader.GetGuid(1),
                    SenderId = reader.GetGuid(2),
                    SenderName = reader.GetString(3),
                    Content = reader.GetString(4),
                    IsFile = reader.GetBoolean(5),
                    FileName = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Timestamp = reader.GetDateTime(7)
                });
            }

            return list;
        }

        public async Task UpdateMessageContentAsync(Guid messageId, string newContent)
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"UPDATE ""AiMessages"" SET ""Content"" = @Content WHERE ""Id"" = @Id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", messageId);
            cmd.Parameters.AddWithValue("@Content", newContent);
            await cmd.ExecuteNonQueryAsync();
        }

    }

    public interface IAiChatClient
    {
        Task ReceiveMessage(Guid id, Guid senderId, string senderName, string message);
    }

    public class AiChatHub : Hub<IAiChatClient>
    {
        private readonly AiChatManager db;
        private readonly AiChatService ai;

        private static readonly Guid AI_ID = Guid.Empty;

        public AiChatHub(AiChatManager db, AiChatService ai)
        {
            this.db = db;
            this.ai = ai;
        }

        public async Task JoinChat(UserConnection connection)
        {
            var studentId = connection.Student.Id;

            await Groups.AddToGroupAsync(Context.ConnectionId, studentId.ToString());

            var history = await db.GetMessagesAsync(studentId);
            foreach (var m in history)
            {
                await Clients.Caller.ReceiveMessage(
                    m.Id, m.SenderId, m.SenderName, m.Content);
            }
        }

        public async Task SendMessage(UserConnection connection, MessageToSend message)
        {
            var studentId = connection.Student.Id;
            var senderName = $"{connection.Student.FirstName} {connection.Student.SecName}";

            await Clients.Group(studentId.ToString())
                .ReceiveMessage(message.Id, studentId, senderName, message.content);

            await db.AddMessageAsync(new AiMessage
            {
                Id = message.Id,
                StudentId = studentId,
                SenderId = studentId,
                SenderName = senderName,
                Content = message.content,
                IsFile = false
            });

            var aiResponse = await ai.AskAsync(studentId, message.content);

            var aiMsgId = Guid.NewGuid();
            await Clients.Group(studentId.ToString())
                .ReceiveMessage(aiMsgId, AI_ID, "AI Учител", aiResponse);

            await db.AddMessageAsync(new AiMessage
            {
                Id = aiMsgId,
                StudentId = studentId,
                SenderId = AI_ID,
                SenderName = "AI Учител",
                Content = aiResponse,
                IsFile = false
            });
        }

        public async Task SendFile(UserConnection connection, string fileName, byte[] fileBytes)
        {
            var studentId = connection.Student.Id;

            var folder = Path.Combine("wwwroot", "ai-files");
            Directory.CreateDirectory(folder);

            var path = Path.Combine(folder, $"{Guid.NewGuid()}_{fileName}");
            await File.WriteAllBytesAsync(path, fileBytes);

            var content = $"<a href='/ai-files/{Path.GetFileName(path)}' target='_blank'>{fileName}</a>";
            var msgId = Guid.NewGuid();

            await Clients.Group(studentId.ToString())
                .ReceiveMessage(msgId, studentId,
                    $"{connection.Student.FirstName} {connection.Student.SecName}",
                    content);

            await db.AddMessageAsync(new AiMessage
            {
                Id = msgId,
                StudentId = studentId,
                SenderId = studentId,
                SenderName = $"{connection.Student.FirstName} {connection.Student.SecName}",
                Content = content,
                IsFile = true,
                FileName = fileName
            });
        }

        public async Task EditMessage(UserConnection connection, Guid messageId, string newContent)
        {
            var messages = await db.GetMessagesAsync(connection.Student.Id);
            var msg = messages.FirstOrDefault(m => m.Id == messageId);

            if (msg == null)
                throw new HubException("Сообщение не найдено.");

            if (msg.SenderId != connection.Student.Id)
                throw new HubException("Можно редактировать только свои сообщения.");

            var lastMessage = messages
                .Where(m => m.SenderId == connection.Student.Id)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();

            if (msg.Id != lastMessage?.Id)
                throw new HubException("Можно редактировать только своё последнее сообщение.");

            await db.UpdateMessageContentAsync(messageId, newContent);

            if (connection.SwapId == Guid.Empty)
            {
                var aiResponse = await ai.AskAsync(connection.Student.Id, newContent);
                var aiMessage = new MessageToSend(Guid.NewGuid(), aiResponse, DateTime.UtcNow);
                await Clients.Group(connection.Student.Id.ToString())
                    .ReceiveMessage(aiMessage.Id, Guid.Empty, "AI Учител", aiMessage.content);
            }
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

        private async Task<List<ChatMessage>> GetHistoryAsync(Guid studentId)
        {
            if (histories.TryGetValue(studentId.ToString(), out var memHistory))
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

            var messages = await aiDb.GetMessagesAsync(studentId);
            foreach (var msg in messages)
            {
                if (msg.IsFile)
                {
                    if (msg.SenderId == studentId)
                        history.Add(new UserChatMessage($"[Файл] {msg.FileName}"));
                    else
                        history.Add(new AssistantChatMessage($"[Файл] {msg.FileName}"));
                }
                else
                {
                    if (msg.SenderId == studentId)
                        history.Add(new UserChatMessage(msg.Content));
                    else
                        history.Add(new AssistantChatMessage(msg.Content));
                }
            }

            histories[studentId.ToString()] = history;
            return history;
        }

        public async Task<string> AskAsync(Guid studentId, string userMessage)
        {
            var history = await GetHistoryAsync(studentId);

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

        public void ClearHistory(Guid studentId)
        {
            histories.TryRemove(studentId.ToString(), out _);
        }
    }
}