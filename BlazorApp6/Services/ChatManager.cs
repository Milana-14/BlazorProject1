using BlazorApp6.Models;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using System.Threading;

namespace BlazorApp6.Services
{
    public class ChatManager
    {
        private readonly string connectionString;
        private readonly SwapManager swapManager;
        public ChatManager(IConfiguration config, SwapManager swapManager)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
            this.swapManager = swapManager;
        }

        public Message? GetMessageById(Guid messageId)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Messages"" WHERE ""Id"" = @messageId", connection);
            cmd.Parameters.AddWithValue("@messageId", messageId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                Message message = new Message();
                message.Id = reader.GetGuid(reader.GetOrdinal("Id"));
                message.SwapId = reader.GetGuid(reader.GetOrdinal("SwapId"));
                message.SenderId = reader.GetGuid(reader.GetOrdinal("SenderId"));
                message.Content = reader.GetString(reader.GetOrdinal("Content"));
                message.Timestamp = reader.GetDateTime(reader.GetOrdinal("Timestamp"));
                message.IsRead = reader.GetBoolean(reader.GetOrdinal("IsRead"));
                message.IsEdited = reader.GetBoolean(reader.GetOrdinal("IsEdited"));
                message.ReplyToMessageId = reader.IsDBNull(reader.GetOrdinal("ReplyToMessageId")) ? null : reader.GetGuid(reader.GetOrdinal("ReplyToMessageId"));
                return message;
            }
            return null;
        }

        public List<Message> GetMessagesFromDb(Guid swapId)
        {
            List<Message> messages = new List<Message>();
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Messages"" WHERE ""SwapId"" = @swapId ORDER BY ""Timestamp""", connection);
            cmd.Parameters.AddWithValue("@swapId", swapId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Message message = new Message();
                message.Id = reader.GetGuid(0);
                message.SwapId = reader.GetGuid(1);
                message.SenderId = reader.GetGuid(2);
                message.Content = reader.GetString(3);
                message.Timestamp = reader.GetDateTime(4);
                message.IsRead = reader.GetBoolean(5);
                message.IsEdited = reader.GetBoolean(6);
                message.ReplyToMessageId = reader.IsDBNull(7) ? null : reader.GetGuid(7);

                messages.Add(message);
            }

            return messages;
        }
        public void AddMessageToDb(Guid Id, Guid swapId, Guid senderId, string content, Guid? replyToMessageId)
        {
            Message message = new Message(Id, swapId, senderId, content);
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                string sql = @"INSERT INTO ""Messages""
                        (""Id"", ""SwapId"", ""SenderId"", ""Content"", ""Timestamp"", ""IsRead"", ""IsEdited"", ""ReplyToMessageId"")
                        VALUES (@Id, @SwapId, @SenderId, @Content, @Timestamp, @IsRead, @IsEdited, @ReplyToMessageId)";



                using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);

                cmd.Parameters.AddWithValue("@Id", message.Id);
                cmd.Parameters.AddWithValue("@SwapId", message.SwapId);
                cmd.Parameters.AddWithValue("@SenderId", message.SenderId);
                cmd.Parameters.AddWithValue("@Content", message.Content);
                cmd.Parameters.AddWithValue("@Timestamp", message.Timestamp);
                cmd.Parameters.AddWithValue("@IsRead", false);
                cmd.Parameters.AddWithValue("@IsEdited", false);
                cmd.Parameters.AddWithValue("@ReplyToMessageId", (object?)replyToMessageId ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }

        public void DeleteMessageById(Guid messageId)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            string sql = @"DELETE FROM ""Messages"" WHERE ""Id"" = @Id";
            using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", messageId);
            cmd.ExecuteNonQuery();
        }

        public void UpdateMessageContent(Guid messageId, string newContent)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            string sql = @"UPDATE ""Messages"" SET ""Content"" = @Content, ""IsEdited"" = @IsEdited WHERE ""Id"" = @Id";
            using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", messageId);
            cmd.Parameters.AddWithValue("@Content", newContent);
            cmd.Parameters.AddWithValue("@IsEdited", true);
            cmd.ExecuteNonQuery();
        }

        public void UpdateMessageReply(Guid messageId, Guid replyToMessageId)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            string sql = @"UPDATE ""Messages"" SET ""ReplyToMessageId"" = @ReplyToMessageId WHERE ""Id"" = @Id";
            using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@Id", messageId);
            cmd.Parameters.AddWithValue("@ReplyToMessageId", (object?)replyToMessageId ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public void MarkMessagesAsRead(Guid swapId, Guid readerId)
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            var sql = @"
        UPDATE ""Messages""
        SET ""IsRead"" = true
        WHERE ""SwapId"" = @swapId
          AND ""SenderId"" != @readerId
          AND ""IsRead"" = false";

            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@swapId", swapId);
            cmd.Parameters.AddWithValue("@readerId", readerId);
            cmd.ExecuteNonQuery();
        }

        public int GetUnreadCount(Guid swapId, Guid studentId)
        {
            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*)
            FROM ""Messages""
            WHERE ""SwapId"" = @swapId
              AND ""SenderId"" != @studentId
              AND ""IsRead"" = false", conn);

            cmd.Parameters.AddWithValue("@swapId", swapId);
            cmd.Parameters.AddWithValue("@studentId", studentId);

            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        public async Task<int> GetUnreadChatsCount(Guid currentStudentId)
        {
            List<Swap> swaps = await swapManager.FindSwapsByStudentId(currentStudentId);
            return swaps.Count(swap => GetUnreadCount(swap.Id, currentStudentId) > 0);
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
            var swap = await swapManager.FindSwapById(connection.SwapId);
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

            var swap = await swapManager.FindSwapById(connection.SwapId);
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
            var swap = await swapManager.FindSwapById(swapId);
            if (swap == null) return;
            if (swap.Student1Id != studentId && swap.Student2Id != studentId)
                throw new HubException("Нямаш право");

            swapManager.ProposeCompletingSwap(swap, studentId);

            await Clients.Group(swapId.ToString()).SwapUpdated(swap);
        }

        public async Task AcceptCompletion(Guid swapId, Guid studentId)
        {
            var swap = await swapManager.FindSwapById(swapId);
            if (swap == null) return;
            if (swap.Student1Id != studentId && swap.Student2Id != studentId)
                throw new HubException("Нямаш право");

            swapManager.AcceptCompletion(swap);

            await Clients.Group(swapId.ToString()).SwapUpdated(swap);
        }

        public async Task RejectCompletion(Guid swapId, Guid studentId)
        {
            var swap = await swapManager.FindSwapById(swapId);
            if (swap == null) return;
            if (swap.Student1Id != studentId && swap.Student2Id != studentId)
                throw new HubException("Нямаш право");

            swapManager.RejectCompletion(swap);

            await Clients.Group(swapId.ToString()).SwapUpdated(swap);
        }
    }
}
