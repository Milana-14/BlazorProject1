using Microsoft.AspNetCore.SignalR;
using BlazorApp6.Models;

namespace BlazorApp6.Services
{
    public class ChatManager
    {

    }


    public interface IChatClient
    {
        Task ReceiveMessage(string username, string message);
        Task UserJoined(string username);
    }
    public record StudentToConnect(Guid Id, string FirstName, string SecName);
    public record UserConnection(Guid SwapId, StudentToConnect Student);

    public class ChatMessages : Hub<IChatClient>
    {
        public async Task JoinChat(UserConnection connection)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, connection.SwapId.ToString());
            await Clients.Group(connection.SwapId.ToString()).UserJoined($"{connection.Student.FirstName} {connection.Student.SecName} е в разговора.");
        }

        public async Task SendMessage(UserConnection connection, string message)
        {
            await Clients.Group(connection.SwapId.ToString()).ReceiveMessage($"{connection.Student.FirstName} {connection.Student.SecName}", message);
        }

        public async Task LeaveChat(UserConnection connection)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, connection.SwapId.ToString());
        }

    }
}
