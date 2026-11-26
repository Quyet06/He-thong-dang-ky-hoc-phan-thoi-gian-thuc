using Registration.Api.Models;
using Registration.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<CourseRegistrationStore>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "Registration.Api",
    status = "running"
}));

app.MapGet("/courses", (CourseRegistrationStore store) =>
{
    return Results.Ok(store.GetCourses());
});

app.MapGet("/courses/{id:int}", (int id, CourseRegistrationStore store) =>
{
    var course = store.GetCourseById(id);
    return course is null
        ? Results.NotFound(new ApiError("COURSE_NOT_FOUND", $"Khong tim thay lop hoc phan co ma {id}."))
        : Results.Ok(course);
});

app.MapPost("/registrations", (
    CreateRegistrationRequest request,
    CourseRegistrationStore store,
    ILogger<Program> logger) =>
{
    var result = store.CreateRegistration(request.StudentId, request.CourseId);
    if (!result.IsSuccess)
    {
        logger.LogWarning(
            "Dang ky that bai cho sinh vien {StudentId} vao lop {CourseId}: {ErrorCode} - {ErrorMessage}",
            request.StudentId,
            request.CourseId,
            result.Error!.Code,
            result.Error.Message);

        return ToErrorResult(result.Error);
    }

    var registration = result.Value!;
    logger.LogInformation(
        "Dang ky thanh cong {RegistrationId}: sinh vien {StudentId} -> lop {CourseCode} (con lai {RemainingSeats} cho)",
        registration.Id,
        registration.StudentId,
        registration.CourseCode,
        registration.RemainingSeats);

    return Results.Created($"/registrations/{registration.Id}", registration);
});

app.MapDelete("/registrations/{id:guid}", (
    Guid id,
    CourseRegistrationStore store,
    ILogger<Program> logger) =>
{
    var result = store.CancelRegistration(id);
    if (!result.IsSuccess)
    {
        logger.LogWarning(
            "Huy dang ky that bai cho ma {RegistrationId}: {ErrorCode} - {ErrorMessage}",
            id,
            result.Error!.Code,
            result.Error.Message);

        return ToErrorResult(result.Error);
    }

    var cancelled = result.Value!;
    logger.LogInformation(
        "Da huy dang ky {RegistrationId}: sinh vien {StudentId} <- lop {CourseCode} (con lai {RemainingSeats} cho)",
        cancelled.Id,
        cancelled.StudentId,
        cancelled.CourseCode,
        cancelled.RemainingSeats);

    return Results.Ok(cancelled);
});

app.MapGet("/students/{studentId}/registrations", (
    string studentId,
    CourseRegistrationStore store) =>
{
    var result = store.GetStudentRegistrations(studentId);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : ToErrorResult(result.Error!);
});

app.Run();

static IResult ToErrorResult(StoreError error)
{
    var payload = new ApiError(error.Code, error.Message);
    return error.Type switch
    {
        StoreErrorType.Validation => Results.BadRequest(payload),
        StoreErrorType.NotFound => Results.NotFound(payload),
        StoreErrorType.Conflict => Results.Conflict(payload),
        _ => Results.BadRequest(payload)
    };
}
