namespace Registration.Api.Models;

/// <summary>
/// Public representation of a course section returned to clients.
/// </summary>
public sealed record CourseDto(
    int Id,
    string Code,
    string Name,
    int Capacity,
    int EnrolledCount,
    int RemainingSeats,
    bool IsOpen);

/// <summary>
/// Request payload for enrolling a student into a course section.
/// </summary>
public sealed record CreateRegistrationRequest(
    string StudentId,
    int CourseId);

/// <summary>
/// Public representation of a course registration record.
/// </summary>
public sealed record RegistrationDto(
    Guid Id,
    string StudentId,
    int CourseId,
    string CourseCode,
    string CourseName,
    string Status,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? CancelledAt,
    int RemainingSeats);

/// <summary>
/// Standardized error contract returned for non-success HTTP responses.
/// </summary>
public sealed record ApiError(
    string Code,
    string Message);
