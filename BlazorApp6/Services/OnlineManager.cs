using BlazorApp6.Models;
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
        }
        return false;
    }

    public bool IsStudentOnline(Student s)
    {
        if (_onlineUsers.ContainsKey(s.Id)) return true;
        if (s.LastOnline.HasValue && s.LastOnline.Value > DateTime.Now.AddMinutes(-5)) return true;
        return false;
    }
}

public class OnlineHub : Hub
{
    private readonly OnlineUsersService _onlineUsers;
    private readonly StudentManager _studentManager;

    public OnlineHub(OnlineUsersService onlineUsers, StudentManager studentManager)
    {
        _onlineUsers = onlineUsers;
        _studentManager = studentManager;
    }

    public override async Task OnConnectedAsync()
    {
        var student = GetAuthenticatedStudent();
        if (student != null)
        {
            _onlineUsers.Add(student.Id, Context.ConnectionId);
            await _studentManager.UpdateLastSeenAsync(student.Id);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var student = GetAuthenticatedStudent();
        if (student != null)
        {
            if (_onlineUsers.Remove(student.Id, Context.ConnectionId))
            {
                await _studentManager.UpdateLastSeenAsync(student.Id);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task Ping()
    {
        var student = GetAuthenticatedStudent();
        if (student != null)
        {
            await _studentManager.UpdateLastSeenAsync(student.Id);
        }
    }

    private Student? GetAuthenticatedStudent()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var username = user.Identity.Name;
            return _studentManager.FindStudent(s => s.Username == username);
        }
        return null;
    }
}