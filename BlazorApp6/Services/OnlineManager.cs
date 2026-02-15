using BlazorApp6.Services;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

public class OnlineUsersService
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _onlineUsers = new();

    public void Add(Guid studentId, string connectionId)
    {
        var connections = _onlineUsers.GetOrAdd(studentId, _ => new HashSet<string>());

        lock (connections)
        {
            connections.Add(connectionId);
        }
    }

    public bool Remove(Guid studentId, string connectionId)
    {
        if (_onlineUsers.TryGetValue(studentId, out var connections))
        {
            lock (connections)
            {
                connections.Remove(connectionId);
                if (connections.Count == 0)
                {
                    _onlineUsers.TryRemove(studentId, out _);
                    return true;
                }
            }
            return false;
        }
        return false;
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
            var student = await _studentManager.FindStudentByUsername(username);

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
            var student = await _studentManager.FindStudentByUsername(username);

            if (student != null)
            {
                if (_onlineUsers.Remove(student.Id, Context.ConnectionId))
                {
                    await _studentManager.UpdateLastSeenAsync(student.Id);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }
}
