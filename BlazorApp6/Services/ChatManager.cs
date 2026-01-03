using BlazorApp6.Models;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace BlazorApp6.Services
{
    public class ChatManager
    {
        private readonly string connectionString;
        public ChatManager(IConfiguration config)
        {
            connectionString = config.GetConnectionString("DefaultConnection");
        }
        public List<Message> GetMessagesFromDb(Guid swapId)
        {
            List<Message> messages = new List<Message>();
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var cmd = new NpgsqlCommand(@"SELECT * FROM ""Messages"" WHERE ""SwapId"" = @swapId", connection);
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

                messages.Add(message);
            }

            return messages;
        }
        public void AddMessageToDb(Guid Id, Guid swapId, Guid senderId, string content)
        {
            Message message = new Message(Id, swapId, senderId, content);
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                string sql = @"INSERT INTO ""Messages""
                        (""Id"", ""SwapId"", ""SenderId"", ""Content"", ""Timestamp"", ""IsRead"")
                        VALUES (@Id, @SwapId, @SenderId, @Content, @Timestamp, @IsRead)";



                using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);

                cmd.Parameters.AddWithValue("@Id", message.Id);
                cmd.Parameters.AddWithValue("@SwapId", message.SwapId);
                cmd.Parameters.AddWithValue("@SenderId", message.SenderId);
                cmd.Parameters.AddWithValue("@Content", message.Content);
                cmd.Parameters.AddWithValue("@Timestamp", message.Timestamp);
                cmd.Parameters.AddWithValue("@IsRead", false);

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
    }


    public interface IChatClient
    {
        Task ReceiveMessage(Guid id, Guid studentId, string username, string message);
        Task UserJoined(string username);
        Task DeleteMessage(Guid messageId);
        Task NewUnread(Guid swapId);
    }
    public record StudentToConnect(Guid Id, string FirstName, string SecName);
    public record UserConnection(Guid SwapId, StudentToConnect Student);
    public record MessageToSend(Guid Id, string content);

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
            await Clients.Group(connection.SwapId.ToString()).ReceiveMessage(message.Id, connection.Student.Id, $"{connection.Student.FirstName} {connection.Student.SecName}", message.content);
            this.chatManager.AddMessageToDb(message.Id, connection.SwapId, connection.Student.Id, message.content);

            await Clients.OthersInGroup(connection.SwapId.ToString()).NewUnread(connection.SwapId);
        }

        public async Task SendFile(UserConnection connection, string fileName, byte[] fileBytes)
        {
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);
            await File.WriteAllBytesAsync(filePath, fileBytes);

            var fileUrl = $"/files/{fileName}";
            MessageToSend message = new MessageToSend(Guid.NewGuid(), $"[Файл] <a href='{fileUrl}' target='_blank'>{fileName}</a>");

            await Clients.Group(connection.SwapId.ToString()).ReceiveMessage(message.Id, connection.Student.Id, $"{connection.Student.FirstName} {connection.Student.SecName}", message.content);

            chatManager.AddMessageToDb(message.Id, connection.SwapId, connection.Student.Id, message.content);
        }

        public async Task MarkAsRead(UserConnection connection)
        {
            chatManager.MarkMessagesAsRead(connection.SwapId, connection.Student.Id);

            await Clients.Group(connection.SwapId.ToString()).NewUnread(connection.SwapId);
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


        public async Task LeaveChat(UserConnection connection)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, connection.SwapId.ToString());
        }

    }
}
