using Microsoft.AspNetCore.SignalR;

namespace BlazorApp6.Services
{
    public class ChatManager
    {

    }


    public interface IChatClient
    {
        Task ReceiveMessage(string username, string message);
    }
    public record UserConnection(string User, string ChatRoom);

    public class ChatMessages : Hub<IChatClient>
    {
        public async Task JoinChat(UserConnection connection)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, connection.ChatRoom);

            await Clients.Group(connection.ChatRoom).ReceiveMessage("Admin", $"{connection.User} е в разговора.");
        }
    }
}
