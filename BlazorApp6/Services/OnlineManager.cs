using BlazorApp6.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

public class OnlineUsersService
{
    // StudentId -> ConnectionId
    private readonly ConcurrentDictionary<Guid, string> _onlineUsers = new();

    public void Add(Guid studentId, string connectionId)
    {
        _onlineUsers[studentId] = connectionId;
    }

    public void Remove(Guid studentId)
    {
        _onlineUsers.TryRemove(studentId, out _);
    }

    public bool IsOnline(Guid studentId)
    {
        return _onlineUsers.ContainsKey(studentId);
    }

    public IEnumerable<Guid> GetOnlineUsers()
    {
        return _onlineUsers.Keys;
    }
}


public class OnlineHub : Hub
{
    private readonly OnlineUsersService _onlineUsers;
    private readonly StudentManager _studentManager;

    public OnlineHub(
        OnlineUsersService onlineUsers,
        StudentManager studentManager)
    {
        _onlineUsers = onlineUsers;
        _studentManager = studentManager;
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var username = user.Identity.Name;
            var student = _studentManager.FindStudent(s => s.Username == username);

            if (student != null)
            {
                _onlineUsers.Add(student.Id, Context.ConnectionId);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var user = Context.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var username = user.Identity.Name;
            var student = _studentManager.FindStudent(s => s.Username == username);

            if (student != null)
            {
                _onlineUsers.Remove(student.Id);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
