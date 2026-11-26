var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var courses = new List<CourseSummary>
{
    new(1, "INT3306-01", "Phat trien ung dung phan tan", 3, 1),
    new(2, "INT3307-01", "He quan tri co so du lieu", 40, 38),
    new(3, "INT3308-01", "Mang may tinh nang cao", 2, 2)
};

app.MapGet("/", () => Results.Ok(new
{
    service = "Registration.Api",
    status = "running"
}));

app.MapGet("/courses", () => Results.Ok(courses));

app.MapPost("/registrations", (CreateRegistrationRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.StudentId) || request.CourseId <= 0)
    {
        return Results.BadRequest(new { message = "Du lieu dang ky khong hop le." });
    }

    var registration = new RegistrationSummary(
        Guid.NewGuid(),
        request.StudentId.Trim(),
        request.CourseId,
        "Active",
        DateTimeOffset.UtcNow);

    return Results.Created($"/registrations/{registration.Id}", registration);
});

app.Run();

record CourseSummary(int Id, string Code, string Name, int Capacity, int EnrolledCount);

record CreateRegistrationRequest(string StudentId, int CourseId);

record RegistrationSummary(
    Guid Id,
    string StudentId,
    int CourseId,
    string Status,
    DateTimeOffset RegisteredAt);
