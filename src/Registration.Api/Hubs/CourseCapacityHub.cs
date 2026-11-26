using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Registration.Api.Hubs;

public static class CourseGroups
{
    public const string AllCourses = "courses:all";

    public static string ForCourse(int courseId) => $"course:{courseId}";
}

/// <summary>
/// Tracks connections subscribed to all courses so course-specific broadcasts do not deliver duplicates.
/// </summary>
public sealed class CourseWatcherRegistry
{
    private readonly ConcurrentDictionary<string, byte> _allCourseConnections = new();

    public void MarkWatchingAll(string connectionId) => _allCourseConnections.TryAdd(connectionId, 0);

    public void UnmarkWatchingAll(string connectionId) => _allCourseConnections.TryRemove(connectionId, out _);

    public IReadOnlyList<string> GetAllCourseConnectionIds() => _allCourseConnections.Keys.ToList();
}

/// <summary>
/// SignalR hub delivering real-time remaining-seat updates to student clients.
/// </summary>
public sealed class CourseCapacityHub(
    CourseWatcherRegistry watcherRegistry,
    ILogger<CourseCapacityHub> logger) : Hub
{
    public async Task WatchAllCourses()
    {
        watcherRegistry.MarkWatchingAll(Context.ConnectionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, CourseGroups.AllCourses);
        logger.LogInformation("Connection {ConnectionId} da theo doi tat ca lop hoc phan", Context.ConnectionId);
    }

    public async Task UnwatchAllCourses()
    {
        watcherRegistry.UnmarkWatchingAll(Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CourseGroups.AllCourses);
        logger.LogInformation("Connection {ConnectionId} da huy theo doi tat ca lop hoc phan", Context.ConnectionId);
    }

    public async Task WatchCourse(int courseId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, CourseGroups.ForCourse(courseId));
        logger.LogInformation("Connection {ConnectionId} da theo doi lop hoc phan {CourseId}", Context.ConnectionId, courseId);
    }

    public async Task UnwatchCourse(int courseId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CourseGroups.ForCourse(courseId));
        logger.LogInformation("Connection {ConnectionId} da huy theo doi lop hoc phan {CourseId}", Context.ConnectionId, courseId);
    }

    public override Task OnConnectedAsync()
    {
        logger.LogInformation("SignalR client ket noi: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        watcherRegistry.UnmarkWatchingAll(Context.ConnectionId);
        logger.LogInformation(
            exception,
            "SignalR client ngat ket noi: {ConnectionId}",
            Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
