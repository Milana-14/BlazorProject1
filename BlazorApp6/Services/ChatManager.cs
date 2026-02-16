using BlazorApp6.Models;
using BlazorApp6.Services;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using NpgsqlTypes;
using System.Data;
using System.Text.RegularExpressions;

namespace BlazorApp6.Services
{
    public class ChatManager
    {
        private readonly string connectionString;
        private readonly SwapManager swapManager;

        public ChatManager(IConfiguration config, SwapManager swapManager)
        {
            connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string missing.");

            this.swapManager = swapManager;
        }

        private static Message ReadMessage(NpgsqlDataReader reader)
        {
            return new Message
            {
                Id = reader.GetGuid(0),
                SwapId = reader.GetGuid(1),
                SenderId = reader.GetGuid(2),
                Content = reader.GetString(3),
                Timestamp = reader.GetDateTime(4),
                IsRead = reader.GetBoolean(5),
                IsEdited = reader.GetBoolean(6),
                ReplyToMessageId = reader.IsDBNull(7) ? null : reader.GetGuid(7)
            };
        }

        public Message? GetMessageById(Guid messageId)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"
                SELECT ""Id"", ""SwapId"", ""SenderId"", ""Content"",
                       ""Timestamp"", ""IsRead"", ""IsEdited"", ""ReplyToMessageId""
                FROM ""Messages""
                WHERE ""Id"" = @id", connection);

            cmd.Parameters.Add("@id", NpgsqlDbType.Uuid).Value = messageId;
            cmd.Prepare();

            using var reader = cmd.ExecuteReader(CommandBehavior.SingleRow);

            return reader.Read() ? ReadMessage(reader) : null;
        }

        public List<Message> GetMessagesFromDb(Guid swapId)
        {
            var messages = new List<Message>(32);

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"
                SELECT ""Id"", ""SwapId"", ""SenderId"", ""Content"",
                       ""Timestamp"", ""IsRead"", ""IsEdited"", ""ReplyToMessageId""
                FROM ""Messages""
                WHERE ""SwapId"" = @swapId
                ORDER BY ""Timestamp""", connection);

            cmd.Parameters.Add("@swapId", NpgsqlDbType.Uuid).Value = swapId;
            cmd.Prepare();

            using var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess);

            while (reader.Read())
                messages.Add(ReadMessage(reader));

            return messages;
        }

        public void AddMessageToDb(Guid id, Guid swapId, Guid senderId, string content, Guid? replyToMessageId)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"
                INSERT INTO ""Messages""
                (""Id"", ""SwapId"", ""SenderId"", ""Content"",
                 ""Timestamp"", ""IsRead"", ""IsEdited"", ""ReplyToMessageId"")
                VALUES (@id,@swap,@sender,@content,@time,false,false,@reply)", connection);

            cmd.Parameters.Add("@id", NpgsqlDbType.Uuid).Value = id;
            cmd.Parameters.Add("@swap", NpgsqlDbType.Uuid).Value = swapId;
            cmd.Parameters.Add("@sender", NpgsqlDbType.Uuid).Value = senderId;
            cmd.Parameters.Add("@content", NpgsqlDbType.Text).Value = content;
            cmd.Parameters.Add("@time", NpgsqlDbType.Timestamp).Value = DateTime.Now;
            cmd.Parameters.Add("@reply", NpgsqlDbType.Uuid).Value =
                (object?)replyToMessageId ?? DBNull.Value;

            cmd.Prepare();
            cmd.ExecuteNonQuery();
        }

        public void DeleteMessageById(Guid messageId)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(
                @"DELETE FROM ""Messages"" WHERE ""Id"" = @id", connection);

            cmd.Parameters.Add("@id", NpgsqlDbType.Uuid).Value = messageId;
            cmd.Prepare();
            cmd.ExecuteNonQuery();
        }

        public void UpdateMessageContent(Guid messageId, string newContent)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"
                UPDATE ""Messages""
                SET ""Content"" = @content, ""IsEdited"" = true
                WHERE ""Id"" = @id", connection);

            cmd.Parameters.Add("@id", NpgsqlDbType.Uuid).Value = messageId;
            cmd.Parameters.Add("@content", NpgsqlDbType.Text).Value = newContent;

            cmd.Prepare();
            cmd.ExecuteNonQuery();
        }

        public void UpdateMessageReply(Guid messageId, Guid replyToMessageId)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"
                UPDATE ""Messages""
                SET ""ReplyToMessageId"" = @reply
                WHERE ""Id"" = @id", connection);

            cmd.Parameters.Add("@id", NpgsqlDbType.Uuid).Value = messageId;
            cmd.Parameters.Add("@reply", NpgsqlDbType.Uuid).Value = (object?)replyToMessageId ?? DBNull.Value;

            cmd.Prepare();
            cmd.ExecuteNonQuery();
        }

        public void MarkMessagesAsRead(Guid swapId, Guid readerId)
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
                UPDATE ""Messages""
                SET ""IsRead"" = true
                WHERE ""SwapId"" = @swap
                  AND ""SenderId"" <> @reader
                  AND ""IsRead"" = false", conn);

            cmd.Parameters.Add("@swap", NpgsqlDbType.Uuid).Value = swapId;
            cmd.Parameters.Add("@reader", NpgsqlDbType.Uuid).Value = readerId;

            cmd.Prepare();
            cmd.ExecuteNonQuery();
        }

        public int GetUnreadCount(Guid swapId, Guid studentId)
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(1)
                FROM ""Messages""
                WHERE ""SwapId""=@swap
                  AND ""SenderId""<>@student
                  AND ""IsRead""=false", conn);

            cmd.Parameters.Add("@swap", NpgsqlDbType.Uuid).Value = swapId;
            cmd.Parameters.Add("@student", NpgsqlDbType.Uuid).Value = studentId;

            cmd.Prepare();

            return (int)(long)cmd.ExecuteScalar()!;
        }

        public int GetUnreadChatsCount(Guid currentStudentId)
        {
            int count = 0;

            foreach (var swap in swapManager.FindSwapsByStudentId(currentStudentId))
            {
                if (GetUnreadCount(swap.Id, currentStudentId) > 0)
                    count++;
            }

            return count;
        }
    }
}


public interface IChatClient
{
    Task ReceiveMessage(Guid id, Guid studentId, string username, string message, DateTime time, Guid? replyToMessageId);
    Task UserJoined(string username);
    Task DeleteMessage(Guid messageId);
    Task EditMessage(Guid messageId, string newContent);
    Task NewUnread(Guid swapId);
    Task SwapUpdated(Swap swap);
}
public record StudentToConnect(Guid Id, string FirstName, string SecName);
public record UserConnection(Guid SwapId, StudentToConnect Student);
public record MessageToSend(Guid Id, string Content, DateTime Time, Guid? ReplyToMessage);



public class ChatMessages : Hub<IChatClient>
{
    private readonly ChatManager chatManager;
    private readonly SwapManager swapManager;
    public ChatMessages(ChatManager chatManager, SwapManager swapManager)
    {
        this.chatManager = chatManager;
        this.swapManager = swapManager;
    }

    public async Task JoinChat(UserConnection connection)
    {
        var swap = swapManager.FindSwapById(connection.SwapId);
        if (swap.Student1Id != connection.Student.Id && swap.Student2Id != connection.Student.Id)
        {
            throw new HubException("Нямаш достъп до този чат. Наявно ти не състоиш в дадения свап.");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, connection.SwapId.ToString());
        await Clients.Group(connection.SwapId.ToString()).UserJoined($"{connection.Student.FirstName} {connection.Student.SecName}");
    }

    public async Task SendMessage(UserConnection connection, MessageToSend message)
    {
        await Clients.Group(connection.SwapId.ToString()).ReceiveMessage(message.Id, connection.Student.Id, $"{connection.Student.FirstName} {connection.Student.SecName}", message.Content, message.Time, message.ReplyToMessage);
        this.chatManager.AddMessageToDb(message.Id, connection.SwapId, connection.Student.Id, message.Content, message.ReplyToMessage);

        await Clients.OthersInGroup(connection.SwapId.ToString()).NewUnread(connection.SwapId);
    }

    public async Task SendFile(UserConnection connection, string fileName, byte[] fileBytes)
    {
        var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, fileName);
        await File.WriteAllBytesAsync(filePath, fileBytes);

        var fileUrl = $"/files/{fileName}";
        MessageToSend message = new MessageToSend(Guid.NewGuid(), $"[Файл] <a href='{fileUrl}' target='_blank'>{fileName}</a>", DateTime.Now, null);

        await Clients.Group(connection.SwapId.ToString()).ReceiveMessage(message.Id, connection.Student.Id, $"{connection.Student.FirstName} {connection.Student.SecName}", message.Content, DateTime.Now, message.ReplyToMessage);

        chatManager.AddMessageToDb(message.Id, connection.SwapId, connection.Student.Id, message.Content, message.ReplyToMessage);
    }

    public async Task MarkAsRead(UserConnection connection)
    {
        chatManager.MarkMessagesAsRead(connection.SwapId, connection.Student.Id);

        await Clients.OthersInGroup(connection.SwapId.ToString()).NewUnread(connection.SwapId);

    }


    public async Task DeleteMessage(UserConnection connection, Guid messageId)
    {
        var messages = chatManager.GetMessagesFromDb(connection.SwapId);
        var msg = messages.FirstOrDefault(m => m.Id == messageId);

        var swap = swapManager.FindSwapById(connection.SwapId);
        if (swap.Student1Id != connection.Student.Id && swap.Student2Id != connection.Student.Id)
        {
            throw new HubException("Нямаш достъп до този чат.");
        }

        if (msg == null)
            throw new HubException("Сообщението не е намерено.");

        if (msg.SenderId != connection.Student.Id)
            throw new HubException("Можете да изтривате само свои съобщения.");

        await Clients.Group(connection.SwapId.ToString()).DeleteMessage(messageId);
        this.chatManager.DeleteMessageById(messageId);
    }

    public async Task EditMessage(UserConnection connection, Guid messageId, string newContent)
    {
        var messages = chatManager.GetMessagesFromDb(connection.SwapId);
        var msg = messages.FirstOrDefault(m => m.Id == messageId);

        if (msg == null)
            throw new HubException("Съобщението не е намерено.");

        if (msg.SenderId != connection.Student.Id)
            throw new HubException("Можете да редактирате само свои съобщения.");

        chatManager.UpdateMessageContent(messageId, newContent);
        await Clients.Group(connection.SwapId.ToString()).EditMessage(messageId, newContent);
    }


    public async Task LeaveChat(UserConnection connection)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, connection.SwapId.ToString());
    }


    public async Task ProposeCompletion(Guid swapId, Guid studentId)
    {
        var swap = swapManager.FindSwapById(swapId);
        if (swap == null) return;
        if (swap.Student1Id != studentId && swap.Student2Id != studentId)
            throw new HubException("Нямаш право");

        swapManager.ProposeCompletingSwap(swap, studentId);

        await Clients.Group(swapId.ToString()).SwapUpdated(swap);
    }

    public async Task AcceptCompletion(Guid swapId, Guid studentId)
    {
        var swap = swapManager.FindSwapById(swapId);
        if (swap == null) return;
        if (swap.Student1Id != studentId && swap.Student2Id != studentId)
            throw new HubException("Нямаш право");

        swapManager.AcceptCompletion(swap);

        await Clients.Group(swapId.ToString()).SwapUpdated(swap);
    }

    public async Task RejectCompletion(Guid swapId, Guid studentId)
    {
        var swap = swapManager.FindSwapById(swapId);
        if (swap == null) return;
        if (swap.Student1Id != studentId && swap.Student2Id != studentId)
            throw new HubException("Нямаш право");

        swapManager.RejectCompletion(swap);

        await Clients.Group(swapId.ToString()).SwapUpdated(swap);
    }
}