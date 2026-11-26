using System.Net.Http.Json;

var apiBaseUrl = args.Length > 0 ? args[0].TrimEnd('/') : "http://localhost:5000";

using var http = new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
};

var courses = await http.GetFromJsonAsync<List<CourseSummary>>("/courses");
Console.WriteLine("Danh sach lop hoc phan:");
foreach (var course in courses ?? [])
{
    Console.WriteLine($"- [{course.Id}] {course.Code}: {course.Name} ({course.EnrolledCount}/{course.Capacity})");
}

var request = new CreateRegistrationRequest("SV001", 1);
var response = await http.PostAsJsonAsync("/registrations", request);
response.EnsureSuccessStatusCode();

var created = await response.Content.ReadFromJsonAsync<RegistrationSummary>();
Console.WriteLine($"Da dang ky thanh cong. Ma dang ky: {created?.Id}");
Console.WriteLine($"Trang thai: {created?.Status}");

record CourseSummary(int Id, string Code, string Name, int Capacity, int EnrolledCount);

record CreateRegistrationRequest(string StudentId, int CourseId);

record RegistrationSummary(
    Guid Id,
    string StudentId,
    int CourseId,
    string Status,
    DateTimeOffset RegisteredAt);
