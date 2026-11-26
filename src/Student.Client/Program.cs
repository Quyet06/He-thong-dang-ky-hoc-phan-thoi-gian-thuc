using System.Net;
using System.Net.Http.Json;

var apiBaseUrl = args.Length > 0 ? args[0].TrimEnd('/') : "http://localhost:5000";
var studentId = args.Length > 1 ? args[1] : "SV001";

using var http = new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
};

// Buoc 1: Tra cuu danh sach lop hoc phan va so cho con lai
var courses = await http.GetFromJsonAsync<List<CourseDto>>("/courses");
Console.WriteLine("=== Danh sach lop hoc phan ===");
foreach (var c in courses ?? [])
{
    Console.WriteLine($"- [{c.Id}] {c.Code} ({c.Name}): da dang ky {c.EnrolledCount}/{c.Capacity}, con lai {c.RemainingSeats}");
}

// Buoc 2: Xem chi tiet lop hoc phan so 1
var courseDetail = await http.GetFromJsonAsync<CourseDto>("/courses/1");
Console.WriteLine($"\nChi tiet lop 1: {courseDetail?.Code} - con lai {courseDetail?.RemainingSeats} cho");

// Buoc 3: Dang ky lop hoc phan so 1
var createResponse = await http.PostAsJsonAsync("/registrations", new CreateRegistrationRequest(studentId, 1));
if (createResponse.StatusCode != HttpStatusCode.Created)
{
    await PrintApiErrorAsync("Dang ky lop 1", createResponse);
    return;
}

var created = await createResponse.Content.ReadFromJsonAsync<RegistrationDto>();
Console.WriteLine($"\nDa dang ky thanh cong: {created?.Id} | Trang thai: {created?.Status} | Cho con lai: {created?.RemainingSeats}");

// Buoc 4: Thu dang ky trung cung lop de quan sat 409 Conflict
var duplicateResponse = await http.PostAsJsonAsync("/registrations", new CreateRegistrationRequest(studentId, 1));
await PrintApiErrorAsync("Thu dang ky trung lop 1", duplicateResponse);

// Buoc 5: Thu dang ky lop da du cho (lop 3) de quan sat 409 Conflict
var fullResponse = await http.PostAsJsonAsync("/registrations", new CreateRegistrationRequest(studentId, 3));
await PrintApiErrorAsync("Thu dang ky lop 3 da du cho", fullResponse);

// Buoc 6: Thu gui du lieu sai de quan sat 400 BadRequest
var invalidResponse = await http.PostAsJsonAsync("/registrations", new CreateRegistrationRequest("", 0));
await PrintApiErrorAsync("Thu gui du lieu khong hop le", invalidResponse);

// Buoc 7: Tra cuu danh sach hoc phan da dang ky cua sinh vien
var studentRegistrations = await http.GetFromJsonAsync<List<RegistrationDto>>($"/students/{studentId}/registrations");
Console.WriteLine($"\n=== Hoc phan da dang ky cua {studentId} ===");
foreach (var r in studentRegistrations ?? [])
{
    Console.WriteLine($"- {r.Id} | {r.CourseCode} | {r.Status}");
}

// Buoc 8: Huy dang ky vua tao va kiem tra lai so cho con lai
if (created is not null)
{
    var cancelResponse = await http.DeleteAsync($"/registrations/{created.Id}");
    if (cancelResponse.IsSuccessStatusCode)
    {
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<RegistrationDto>();
        Console.WriteLine($"\nDa huy dang ky {cancelled?.Id} -> Trang thai: {cancelled?.Status} | Cho con lai: {cancelled?.RemainingSeats}");
    }
    else
    {
        await PrintApiErrorAsync("Huy dang ky", cancelResponse);
    }
}

static async Task PrintApiErrorAsync(string action, HttpResponseMessage response)
{
    if (response.IsSuccessStatusCode)
    {
        return;
    }

    var error = await response.Content.ReadFromJsonAsync<ApiError>();
    Console.WriteLine($"\n[{action}] {(int)response.StatusCode} {response.StatusCode}: {error?.Code} - {error?.Message}");
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

record ApiError(string Code, string Message);
