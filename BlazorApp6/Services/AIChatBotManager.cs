using Azure;
using Azure.AI.OpenAI;
using BlazorApp6.Models;
using BlazorApp6.Services;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using OpenAI.Chat;
using System.ClientModel;
using System.Collections.Concurrent;
using System.Text;

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
(""Id"", ""StudentId"", ""SenderId"", ""SenderName"", ""Content"", ""IsFile"", ""FileName"", ""ReplyToMessageId"", ""Timestamp"")
VALUES
(@Id, @StudentId, @SenderId, @SenderName, @Content, @IsFile, @FileName, @ReplyToMessageId, @Timestamp)";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", message.Id);
            cmd.Parameters.AddWithValue("@StudentId", message.StudentId);
            cmd.Parameters.AddWithValue("@SenderId", message.SenderId);
            cmd.Parameters.AddWithValue("@SenderName", message.SenderName);
            cmd.Parameters.AddWithValue("@Content", message.Content);
            cmd.Parameters.AddWithValue("@IsFile", message.IsFile);
            cmd.Parameters.AddWithValue("@FileName", (object?)message.FileName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ReplyToMessageId", message.ReplyToMessageId == Guid.Empty ? Guid.Empty : message.ReplyToMessageId);
            cmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<AiMessage>> GetMessagesAsync(Guid studentId)
        {
            var list = new List<AiMessage>();
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
SELECT ""Id"", ""StudentId"", ""SenderId"", ""SenderName"", ""Content"", ""IsFile"", ""FileName"", ""ReplyToMessageId"", ""Timestamp""
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
                    ReplyToMessageId = reader.GetGuid(7),
                    Timestamp = reader.GetDateTime(8)
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

        public async Task DeleteMessageAsync(Guid messageId)
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"DELETE FROM ""AiMessages"" WHERE ""Id"" = @Id";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", messageId);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    public interface IAiChatClient
    {
        Task ReceiveMessage(Guid id, Guid senderId, string senderName, string message);
        Task DeleteMessage(Guid messageId);
        Task EditMessage(Guid messageId, string newContent);
        Task AiTypingStarted(Guid tempMessageId);
        Task AiTypingChunk(Guid tempMessageId, string chunk);
        Task AiTypingFinished(Guid tempMessageId, Guid finalMessageId);
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
                IsFile = false,
                ReplyToMessageId = Guid.Empty
            });

            var tempId = Guid.NewGuid();
            await Clients.Group(studentId.ToString()).AiTypingStarted(tempId);

            await foreach (var token in ai.StreamAskAsync(studentId, message.content))
            {
                await Clients.Group(studentId.ToString()).AiTypingChunk(tempId, token);
            }

            var aiFullContent = await ai.GetFullResponseAsync(studentId);
            if (aiFullContent == null)
            {
                throw new HubException("Грешка при генериране на отговор от AI.");
            }

            if (!string.IsNullOrWhiteSpace(aiFullContent))
            {
                var aiMessage = new AiMessage
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    SenderId = AI_ID,
                    SenderName = "AI Учител",
                    Content = aiFullContent,
                    IsFile = false,
                    ReplyToMessageId = message.Id
                };

                await db.AddMessageAsync(aiMessage);
                await Clients.Group(studentId.ToString()).AiTypingFinished(tempId, aiMessage.Id);
            }
            else await Clients.Group(studentId.ToString()).AiTypingFinished(tempId, Guid.Empty);
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
                FileName = fileName,
                ReplyToMessageId = Guid.Empty
            });
        }

        public async Task EditMessage(UserConnection connection, Guid messageId, string newContent)
        {
            var messages = await db.GetMessagesAsync(connection.Student.Id);
            var msg = messages.FirstOrDefault(m => m.Id == messageId);

            if (msg == null)
                throw new HubException("Сообщението не е намерено.");

            if (msg.SenderId != connection.Student.Id)
                throw new HubException("Може да се редактират само свои съобщения.");

            var aiMsg = messages.FirstOrDefault(m => m.SenderId == AI_ID && m.ReplyToMessageId == messageId);

            if (aiMsg != null)
            {
                await db.DeleteMessageAsync(aiMsg.Id);
                await Clients.Group(connection.Student.Id.ToString()).DeleteMessage(aiMsg.Id);
            }

            var lastMessage = messages
                .Where(m => m.SenderId == connection.Student.Id)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault();

            if (msg.Id != lastMessage?.Id)
                throw new HubException("Редакцията е позволена само на последното съобщение.");

            await db.UpdateMessageContentAsync(messageId, newContent);
            await Clients.Group(connection.Student.Id.ToString())
                .EditMessage(messageId, newContent);

            await ai.RestoreHistoryAsync(connection.Student.Id);

            if (connection.SwapId == Guid.Empty)
            {
                var tempId = Guid.NewGuid();
                await Clients.Group(connection.Student.Id.ToString()).AiTypingStarted(tempId);

                await foreach (var token in ai.StreamAskAsync(connection.Student.Id, newContent))
                {
                    await Clients.Group(connection.Student.Id.ToString()).AiTypingChunk(tempId, token);
                }

                var aiResponse = await ai.GetFullResponseAsync(connection.Student.Id);
                if (aiResponse == null)
                {
                    throw new HubException("Грешка при генериране на отговор от AI.");
                } 

                var newAiId = Guid.NewGuid();

                await db.AddMessageAsync(new AiMessage
                {
                    Id = newAiId,
                    StudentId = connection.Student.Id,
                    SenderId = AI_ID,
                    SenderName = "AI Учител",
                    Content = aiResponse,
                    ReplyToMessageId = messageId
                });

                await Clients.Group(connection.Student.Id.ToString())
                    .ReceiveMessage(newAiId, AI_ID, "AI Учител", aiResponse);
            }
        }

    }
}

    public class AiChatService
{
    private readonly ChatClient chatClient;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> histories = new();
    private readonly AiChatManager aiDb;
    private readonly StudentManager studentManager;
    public AiChatService(IConfiguration config, AiChatManager aiDb, StudentManager studentManager)
    {
        var token = Environment.GetEnvironmentVariable("EDUSWAPS_AI_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
            throw new Exception("Токен AI не задан. EDUSWAPS_AI_TOKEN");

        var endpoint = new Uri("https://models.inference.ai.azure.com");
        var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(token));

        chatClient = client.GetChatClient("gpt-4o");
        this.aiDb = aiDb;
        this.studentManager = studentManager;
    }

    private async Task<List<ChatMessage>> GetOrCreateHistoryAsync(Guid studentId)
    {
        if (histories.TryGetValue(studentId.ToString(), out var memHistory))
            return memHistory;

        Student student = studentManager.FindStudent(s => s.Id == studentId);

        var history = new List<ChatMessage>
        {
            new SystemChatMessage(
                    $"Ти си внимателен и търпелив гимназиален учител за {student.Grade} клас.\n" +
                    $"Имай предвид, че ученикът има затруднения в следните теми: {string.Join(", ", student.NeedsHelpWith).ToLower()}\n\n" +

                    "Твоята цел е да помагаш на ученика да разбере учебния материал.\n" +
                    "Обяснявай стъпка по стъпка, с примери и насочващи въпроси.\n" +
                    "Опитвай се да насочиш ученика към правилния отговор, без да му го даваш.\n" +
                    "Отговорите да бъдат ясни и стегнати.\n" +
                    "Ако ученикът греши, помогни му да стигне сам до правилния отговор.\n\n" +

                    "При обяснение на нов урок или нова тема – структурирай отговора така:\n" +
                    "1) Кратко въведение\n" +
                    "2) Основна част със списъци и примери\n" +
                    "3) Кратко обобщение в края и/или въпрос към ученика\n\n" +

                    "Когато ученикът задава уточняващ или конкретен въпрос:\n" +
                    "- отговаряй директно по същество\n" +
                    "- фокусирай се само върху зададения въпрос\n" +
                    "- използвай примери, ако помагат за разбирането\n" +
                    "- НЕ е задължително да следваш пълната структура по-горе\n\n" +

                    "Отговаряй винаги във формат Markdown.\n" +
                    "Използвай:\n" +
                    "- заглавия (##, ###)\n" +
                    "- **удебелен текст** за важни понятия\n" +
                    "- *курсив* за пояснения\n" +
                    "- списъци\n" +
                    "- > цитати при нужда\n" +
                    "Не използвай HTML, само Markdown."
            )
        };

        var messages = await aiDb.GetMessagesAsync(studentId);
        foreach (var msg in messages)
        {
            if (msg.IsFile)
            {
                var text = $"[Файл] {msg.FileName}";
                if (msg.SenderId == studentId)
                    history.Add(new UserChatMessage(text));
                else
                    history.Add(new AssistantChatMessage(text));
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

    public async IAsyncEnumerable<string> StreamAskAsync(Guid studentId, string userMessage)
    {
        var history = await GetOrCreateHistoryAsync(studentId);
        history.Add(new UserChatMessage(userMessage));

        var sb = new StringBuilder();

        await foreach (var update in chatClient.CompleteChatStreamingAsync(history))
        {
            foreach (var part in update.ContentUpdate)
                if (!string.IsNullOrEmpty(part.Text))
                {
                    sb.Append(part.Text);
                    yield return part.Text;
                }
        }

        var fullResponse = sb.ToString();
        history.Add(new AssistantChatMessage(fullResponse));

        if (history.Count > 50)
            history.RemoveRange(1, history.Count - 50);
    }

    public async Task<string> GetFullResponseAsync(Guid studentId)
    {
        var history = await GetOrCreateHistoryAsync(studentId);
        var last = history.LastOrDefault() as UserChatMessage;
        if (last == null) return "";

        var sb = new StringBuilder();
        await foreach (var update in chatClient.CompleteChatStreamingAsync(history))
        {
            foreach (var part in update.ContentUpdate)
                if (!string.IsNullOrEmpty(part.Text))
                    sb.Append(part.Text);
        }

        var full = sb.ToString();
        history.Add(new AssistantChatMessage(full));

        if (history.Count > 50)
            history.RemoveRange(1, history.Count - 50);

        return full;
    }

    public async Task RestoreHistoryAsync(Guid studentId)
    {
        var history = await GetOrCreateHistoryAsync(studentId);
    }
}