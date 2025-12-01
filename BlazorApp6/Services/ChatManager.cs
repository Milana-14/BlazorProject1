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

                messages.Add(message);
            }

            return messages;
        }
        public void AddMessageToDb(Guid swapId, Guid senderId, string content)
        {
            Message message = new Message(swapId, senderId, content);
            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                string sql = @"INSERT INTO ""Messages"" (""Id"", ""SwapId"", ""SenderId"", ""Content"", ""Timestamp"") 
                                VALUES (@Id, @SwapId, @SenderId, @Content, @Timestamp)";


                using NpgsqlCommand cmd = new NpgsqlCommand(sql, connection);

                cmd.Parameters.AddWithValue("@Id", message.Id);
                cmd.Parameters.AddWithValue("@SwapId", message.SwapId);
                cmd.Parameters.AddWithValue("@SenderId", message.SenderId);
                cmd.Parameters.AddWithValue("@Content", message.Content);
                cmd.Parameters.AddWithValue("@Timestamp", message.Timestamp);

                cmd.ExecuteNonQuery();
            }
        }
    }


    public interface IChatClient
    {
        Task ReceiveMessage(Guid studentId, string username, string message);
        Task UserJoined(string username);
    }
    public record StudentToConnect(Guid Id, string FirstName, string SecName);
    public record UserConnection(Guid SwapId, StudentToConnect Student);

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

        public async Task SendMessage(UserConnection connection, string message)
        {
            await Clients.Group(connection.SwapId.ToString()).ReceiveMessage(connection.Student.Id, $"{connection.Student.FirstName} {connection.Student.SecName}", message);
            this.chatManager.AddMessageToDb(connection.SwapId, connection.Student.Id, message);
        }

        public async Task SendFile(UserConnection connection, string fileName, byte[] fileBytes)
        {
            var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "files");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);
            await File.WriteAllBytesAsync(filePath, fileBytes);

            var fileUrl = $"/files/{fileName}";
            var message = $"[Файл] <a href='{fileUrl}' target='_blank'>{fileName}</a>";

            await Clients.Group(connection.SwapId.ToString()).ReceiveMessage(connection.Student.Id, $"{connection.Student.FirstName} {connection.Student.SecName}", message);

            chatManager.AddMessageToDb(connection.SwapId, connection.Student.Id, message);
        }


        public async Task LeaveChat(UserConnection connection)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, connection.SwapId.ToString());
        }

    }
}
