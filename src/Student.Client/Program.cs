using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

var apiBaseUrl = args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal)
    ? args[0].TrimEnd('/')
    : "http://localhost:5000";

var studentId = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal)
    ? args[1]
    : "SV001";

var isInteractive = args.Any(a =>
    string.Equals(a, "--interactive", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(a, "-i", StringComparison.OrdinalIgnoreCase));

using var http = new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
};

await using var connection = new HubConnectionBuilder()
    .WithUrl($"{apiBaseUrl}/hubs/courses")
    .WithAutomaticReconnect()
    .Build();

var createdUpdateReceived = new TaskCompletionSource<CourseCapacityUpdated>(TaskCreationOptions.RunContinuationsAsynchronously);
var cancelledUpdateReceived = new TaskCompletionSource<CourseCapacityUpdated>(TaskCreationOptions.RunContinuationsAsynchronously);

connection.On<CourseCapacityUpdated>("CourseCapacityUpdated", update =>
{
    Console.WriteLine(
        $"\n[SignalR {update.UpdatedAt:HH:mm:ss}] {update.EventType} | Lop [{update.CourseId}] {update.CourseCode}: " +
        $"da dang ky {update.EnrolledCount}/{update.Capacity}, con lai {update.RemainingSeats} cho");

    if (update.CourseId == 1 && update.EventType == "RegistrationCreated")
    {
        createdUpdateReceived.TrySetResult(update);
    }
    else if (update.CourseId == 1 && update.EventType == "RegistrationCancelled")
    {
        cancelledUpdateReceived.TrySetResult(update);
    }
});

connection.Reconnecting += ex =>
{
    Console.WriteLine($"[SignalR] Mat ket noi ({ex?.Message}), dang thu ket noi lai...");
    return Task.CompletedTask;
};

connection.Reconnected += async _ =>
{
    Console.WriteLine("[SignalR] Da ket noi lai thanh cong. Dang dong bo lai trang thai qua REST...");
    await connection.InvokeAsync("WatchAllCourses");
    await PrintCoursesAsync(http);
};

connection.Closed += ex =>
{
    Console.WriteLine(ex is null
        ? "[SignalR] Ket noi da dong."
        : $"[SignalR] Ket noi da dong do loi: {ex.Message}");
    return Task.CompletedTask;
};

await connection.StartAsync();
Console.WriteLine($"Da ket noi SignalR toi {apiBaseUrl}/hubs/courses (Sinh vien: {studentId}).");

await connection.InvokeAsync("WatchAllCourses");
await connection.InvokeAsync("WatchCourse", 1);

if (isInteractive)
{
    await RunInteractiveLoopAsync(http, connection, studentId);
    return;
}

await RunDemoFlowAsync(http, studentId, createdUpdateReceived, cancelledUpdateReceived);

static async Task RunDemoFlowAsync(
    HttpClient http,
    string studentId,
    TaskCompletionSource<CourseCapacityUpdated> createdUpdateReceived,
    TaskCompletionSource<CourseCapacityUpdated> cancelledUpdateReceived)
{
    await PrintCoursesAsync(http);

    var course1 = await http.GetFromJsonAsync<CourseDto>("/courses/1");
    Console.WriteLine($"\nChi tiet lop 1 (REST): {course1?.Code} - con lai {course1?.RemainingSeats} cho");

    Console.WriteLine($"\n[1] Sinh vien {studentId} gui yeu cau dang ky lop 1...");
    var createResponse = await http.PostAsJsonAsync("/registrations", new CreateRegistrationRequest(studentId, 1));
    if (createResponse.StatusCode != HttpStatusCode.Created)
    {
        await PrintApiErrorAsync("Dang ky lop 1", createResponse);
        return;
    }

    var created = await createResponse.Content.ReadFromJsonAsync<RegistrationDto>();
    Console.WriteLine($"REST 201 Created: ma dang ky {created?.Id} | Trang thai: {created?.Status}");

    await WaitForSignalRAsync("RegistrationCreated", createdUpdateReceived.Task);

    Console.WriteLine($"\n[2] Thu dang ky trung cung lop 1...");
    var duplicateResponse = await http.PostAsJsonAsync("/registrations", new CreateRegistrationRequest(studentId, 1));
    await PrintApiErrorAsync("Dang ky trung", duplicateResponse);

    Console.WriteLine("\n[3] Thu dang ky lop 3 da du cho...");
    var fullResponse = await http.PostAsJsonAsync("/registrations", new CreateRegistrationRequest(studentId, 3));
    await PrintApiErrorAsync("Lop du cho", fullResponse);

    Console.WriteLine("\n[4] Thu gui du lieu khong hop le...");
    var invalidResponse = await http.PostAsJsonAsync("/registrations", new CreateRegistrationRequest("", 0));
    await PrintApiErrorAsync("Du lieu khong hop le", invalidResponse);

    await PrintStudentRegistrationsAsync(http, studentId);

    if (created is not null)
    {
        Console.WriteLine($"\n[5] Huy dang ky {created.Id}...");
        var deleteResponse = await http.DeleteAsync($"/registrations/{created.Id}");
        if (deleteResponse.IsSuccessStatusCode)
        {
            var cancelled = await deleteResponse.Content.ReadFromJsonAsync<RegistrationDto>();
            Console.WriteLine($"REST 200 OK: da huy {cancelled?.Id} -> Trang thai: {cancelled?.Status}");
            await WaitForSignalRAsync("RegistrationCancelled", cancelledUpdateReceived.Task);
        }
        else
        {
            await PrintApiErrorAsync("Huy dang ky", deleteResponse);
        }
    }

    var latestCourse1 = await http.GetFromJsonAsync<CourseDto>("/courses/1");
    Console.WriteLine($"\nKiem tra lai qua REST /courses/1: {latestCourse1?.Code} - con lai {latestCourse1?.RemainingSeats}/{latestCourse1?.Capacity} cho.");
}

static async Task RunInteractiveLoopAsync(HttpClient http, HubConnection connection, string studentId)
{
    await PrintCoursesAsync(http);

    while (true)
    {
        Console.WriteLine("\nChon thao tac:");
        Console.WriteLine("  1. Xem danh sach lop hoc phan");
        Console.WriteLine("  2. Dang ky lop hoc phan");
        Console.WriteLine("  3. Huy dang ky theo ma");
        Console.WriteLine("  4. Xem hoc phan da dang ky cua toi");
        Console.WriteLine("  5. Theo doi rieng mot lop qua SignalR");
        Console.WriteLine("  0. Thoat");
        Console.Write("Nhap lua chon: ");

        var choice = Console.ReadLine()?.Trim();
        switch (choice)
        {
            case "1":
                await PrintCoursesAsync(http);
                break;

            case "2":
                Console.Write("Nhap ma lop (CourseId): ");
                if (!int.TryParse(Console.ReadLine(), out var courseId))
                {
                    Console.WriteLine("CourseId khong hop le.");
                    break;
                }

                var postResp = await http.PostAsJsonAsync("/registrations", new CreateRegistrationRequest(studentId, courseId));
                if (postResp.StatusCode == HttpStatusCode.Created)
                {
                    var reg = await postResp.Content.ReadFromJsonAsync<RegistrationDto>();
                    Console.WriteLine($"Dang ky thanh cong! Ma dang ky: {reg?.Id}");
                }
                else
                {
                    await PrintApiErrorAsync("Dang ky", postResp);
                }
                break;

            case "3":
                Console.Write("Nhap ma dang ky (Guid): ");
                if (!Guid.TryParse(Console.ReadLine(), out var regId))
                {
                    Console.WriteLine("Ma dang ky khong hop le.");
                    break;
                }

                var delResp = await http.DeleteAsync($"/registrations/{regId}");
                if (delResp.IsSuccessStatusCode)
                {
                    var cancelled = await delResp.Content.ReadFromJsonAsync<RegistrationDto>();
                    Console.WriteLine($"Da huy dang ky {cancelled?.Id} ({cancelled?.CourseCode}).");
                }
                else
                {
                    await PrintApiErrorAsync("Huy dang ky", delResp);
                }
                break;

            case "4":
                await PrintStudentRegistrationsAsync(http, studentId);
                break;

            case "5":
                Console.Write("Nhap ma lop muon theo doi (CourseId): ");
                if (int.TryParse(Console.ReadLine(), out var watchCourseId))
                {
                    await connection.InvokeAsync("WatchCourse", watchCourseId);
                    Console.WriteLine($"Da dang ky theo doi lop {watchCourseId} qua SignalR.");
                }
                break;

            case "0":
                return;

            default:
                Console.WriteLine("Lua chon khong hop le.");
                break;
        }
    }
}

static async Task WaitForSignalRAsync(string eventName, Task<CourseCapacityUpdated> task)
{
    var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
    if (completed != task)
    {
        Console.WriteLine($"Qua thoi gian cho thong bao SignalR '{eventName}'.");
    }
}

static async Task PrintCoursesAsync(HttpClient http)
{
    var courses = await http.GetFromJsonAsync<List<CourseDto>>("/courses");
    Console.WriteLine("\n=== Danh sach lop hoc phan ===");
    foreach (var c in courses ?? [])
    {
        Console.WriteLine($"- [{c.Id}] {c.Code} ({c.Name}): da dang ky {c.EnrolledCount}/{c.Capacity}, con lai {c.RemainingSeats}");
    }
}

static async Task PrintStudentRegistrationsAsync(HttpClient http, string studentId)
{
    var registrations = await http.GetFromJsonAsync<List<RegistrationDto>>($"/students/{studentId}/registrations");
    Console.WriteLine($"\n=== Hoc phan da dang ky cua {studentId} ===");
    foreach (var r in registrations ?? [])
    {
        Console.WriteLine($"- {r.Id} | {r.CourseCode} ({r.CourseName}) | {r.Status}");
    }
}

static async Task PrintApiErrorAsync(string action, HttpResponseMessage response)
{
    if (response.IsSuccessStatusCode)
    {
        return;
    }

    var error = await response.Content.ReadFromJsonAsync<ApiError>();
    Console.WriteLine($"[{action}] {(int)response.StatusCode} {response.StatusCode}: {error?.Code} - {error?.Message}");
}

record CourseDto(
    int Id,
    string Code,
    string Name,
    int Capacity,
    int EnrolledCount,
    int RemainingSeats,
    bool IsOpen);

record CreateRegistrationRequest(string StudentId, int CourseId);

record RegistrationDto(
    Guid Id,
    string StudentId,
    int CourseId,
    string CourseCode,
    string CourseName,
    string Status,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? CancelledAt,
    int RemainingSeats);

record CourseCapacityUpdated(
    int CourseId,
    string CourseCode,
    string CourseName,
    int Capacity,
    int EnrolledCount,
    int RemainingSeats,
    string EventType,
    DateTimeOffset UpdatedAt);

record ApiError(string Code, string Message);
