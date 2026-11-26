using Registration.Api.Models;

namespace Registration.Api.Services;

public enum StoreErrorType
{
    Validation,
    NotFound,
    Conflict
}

public sealed record StoreError(StoreErrorType Type, string Code, string Message);

public sealed record StoreResult<T>(T? Value, StoreError? Error)
{
    public bool IsSuccess => Error is null && Value is not null;

    public static StoreResult<T> Ok(T value) => new(value, null);

    public static StoreResult<T> Fail(StoreErrorType type, string code, string message) =>
        new(default, new StoreError(type, code, message));
}

/// <summary>
/// Thread-safe in-memory store managing students, course sections, and registrations.
/// </summary>
public sealed class CourseRegistrationStore
{
    private const string StatusActive = "Active";
    private const string StatusCancelled = "Cancelled";

    private readonly Lock _syncLock = new();
    private readonly Dictionary<string, StudentEntity> _students = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CourseEntity> _courses = [];
    private readonly List<RegistrationEntity> _registrations = [];

    public CourseRegistrationStore()
    {
        SeedInitialData();
    }

    public IReadOnlyList<CourseDto> GetCourses()
    {
        lock (_syncLock)
        {
            return _courses.Select(ToCourseDto).ToList();
        }
    }

    public CourseDto? GetCourseById(int courseId)
    {
        lock (_syncLock)
        {
            var course = _courses.FirstOrDefault(c => c.Id == courseId);
            return course is null ? null : ToCourseDto(course);
        }
    }

    public StoreResult<RegistrationDto> CreateRegistration(string? studentId, int courseId)
    {
        var normalizedStudentId = studentId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedStudentId) || courseId <= 0)
        {
            return StoreResult<RegistrationDto>.Fail(
                StoreErrorType.Validation,
                "INVALID_REGISTRATION",
                "Ma sinh vien va ma lop hoc phan phai hop le.");
        }

        lock (_syncLock)
        {
            var course = _courses.FirstOrDefault(c => c.Id == courseId);
            if (course is null)
            {
                return StoreResult<RegistrationDto>.Fail(
                    StoreErrorType.NotFound,
                    "COURSE_NOT_FOUND",
                    $"Khong tim thay lop hoc phan co ma {courseId}.");
            }

            if (!course.IsOpen)
            {
                return StoreResult<RegistrationDto>.Fail(
                    StoreErrorType.Conflict,
                    "COURSE_CLOSED",
                    $"Lop hoc phan {course.Code} da dong dang ky.");
            }

            var hasActiveRegistration = _registrations.Any(r =>
                string.Equals(r.StudentId, normalizedStudentId, StringComparison.OrdinalIgnoreCase) &&
                r.CourseId == courseId &&
                r.Status == StatusActive);

            if (hasActiveRegistration)
            {
                return StoreResult<RegistrationDto>.Fail(
                    StoreErrorType.Conflict,
                    "DUPLICATE_REGISTRATION",
                    $"Sinh vien {normalizedStudentId} da dang ky lop {course.Code}.");
            }

            if (course.EnrolledCount >= course.Capacity)
            {
                return StoreResult<RegistrationDto>.Fail(
                    StoreErrorType.Conflict,
                    "COURSE_FULL",
                    $"Lop hoc phan {course.Code} da du cho ({course.EnrolledCount}/{course.Capacity}).");
            }

            if (!_students.ContainsKey(normalizedStudentId))
            {
                _students[normalizedStudentId] = new StudentEntity(normalizedStudentId, $"Sinh vien {normalizedStudentId}");
            }

            course.EnrolledCount++;
            var entity = new RegistrationEntity(
                Guid.NewGuid(),
                _students[normalizedStudentId].StudentId,
                course.Id,
                StatusActive,
                DateTimeOffset.UtcNow,
                null);

            _registrations.Add(entity);
            return StoreResult<RegistrationDto>.Ok(ToRegistrationDto(entity, course));
        }
    }

    public StoreResult<RegistrationDto> CancelRegistration(Guid registrationId)
    {
        lock (_syncLock)
        {
            var registration = _registrations.FirstOrDefault(r => r.Id == registrationId);
            if (registration is null)
            {
                return StoreResult<RegistrationDto>.Fail(
                    StoreErrorType.NotFound,
                    "REGISTRATION_NOT_FOUND",
                    $"Khong tim thay dang ky co ma {registrationId}.");
            }

            var course = _courses.FirstOrDefault(c => c.Id == registration.CourseId);
            if (course is null)
            {
                return StoreResult<RegistrationDto>.Fail(
                    StoreErrorType.NotFound,
                    "COURSE_NOT_FOUND",
                    $"Khong tim thay lop hoc phan co ma {registration.CourseId}.");
            }

            if (registration.Status == StatusCancelled)
            {
                return StoreResult<RegistrationDto>.Fail(
                    StoreErrorType.Conflict,
                    "REGISTRATION_ALREADY_CANCELLED",
                    $"Dang ky {registrationId} da duoc huy truoc do.");
            }

            registration.Status = StatusCancelled;
            registration.CancelledAt = DateTimeOffset.UtcNow;
            if (course.EnrolledCount > 0)
            {
                course.EnrolledCount--;
            }

            return StoreResult<RegistrationDto>.Ok(ToRegistrationDto(registration, course));
        }
    }

    public StoreResult<IReadOnlyList<RegistrationDto>> GetStudentRegistrations(string? studentId)
    {
        var normalizedStudentId = studentId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedStudentId))
        {
            return StoreResult<IReadOnlyList<RegistrationDto>>.Fail(
                StoreErrorType.Validation,
                "INVALID_STUDENT_ID",
                "Ma sinh vien khong duoc de trong.");
        }

        lock (_syncLock)
        {
            var courseMap = _courses.ToDictionary(c => c.Id);
            var list = _registrations
                .Where(r => string.Equals(r.StudentId, normalizedStudentId, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.RegisteredAt)
                .Select(r => ToRegistrationDto(r, courseMap[r.CourseId]))
                .ToList();

            return StoreResult<IReadOnlyList<RegistrationDto>>.Ok(list);
        }
    }

    private void SeedInitialData()
    {
        _students["SV001"] = new StudentEntity("SV001", "Nguyen Van An");
        _students["SV002"] = new StudentEntity("SV002", "Tran Thi Binh");
        _students["SV003"] = new StudentEntity("SV003", "Le Hoang Cuong");

        var course1 = new CourseEntity(1, "INT3306-01", "Phat trien ung dung phan tan", 3, 1, true);
        var course2 = new CourseEntity(2, "INT3307-01", "He quan tri co so du lieu", 2, 1, true);
        var course3 = new CourseEntity(3, "INT3308-01", "Mang may tinh nang cao", 1, 1, true);

        _courses.AddRange([course1, course2, course3]);

        _registrations.AddRange([
            new RegistrationEntity(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "SV002",
                course1.Id,
                StatusActive,
                DateTimeOffset.UtcNow.AddMinutes(-30),
                null),
            new RegistrationEntity(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                "SV003",
                course2.Id,
                StatusActive,
                DateTimeOffset.UtcNow.AddMinutes(-20),
                null),
            new RegistrationEntity(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "SV003",
                course3.Id,
                StatusActive,
                DateTimeOffset.UtcNow.AddMinutes(-10),
                null)
        ]);
    }

    private static CourseDto ToCourseDto(CourseEntity course) =>
        new(
            course.Id,
            course.Code,
            course.Name,
            course.Capacity,
            course.EnrolledCount,
            Math.Max(0, course.Capacity - course.EnrolledCount),
            course.IsOpen);

    private static RegistrationDto ToRegistrationDto(RegistrationEntity entity, CourseEntity course) =>
        new(
            entity.Id,
            entity.StudentId,
            entity.CourseId,
            course.Code,
            course.Name,
            entity.Status,
            entity.RegisteredAt,
            entity.CancelledAt,
            Math.Max(0, course.Capacity - course.EnrolledCount));

    private sealed record StudentEntity(string StudentId, string FullName);

    private sealed class CourseEntity(int id, string code, string name, int capacity, int enrolledCount, bool isOpen)
    {
        public int Id { get; } = id;
        public string Code { get; } = code;
        public string Name { get; } = name;
        public int Capacity { get; } = capacity;
        public int EnrolledCount { get; set; } = enrolledCount;
        public bool IsOpen { get; } = isOpen;
    }

    private sealed class RegistrationEntity(
        Guid id,
        string studentId,
        int courseId,
        string status,
        DateTimeOffset registeredAt,
        DateTimeOffset? cancelledAt)
    {
        public Guid Id { get; } = id;
        public string StudentId { get; } = studentId;
        public int CourseId { get; } = courseId;
        public string Status { get; set; } = status;
        public DateTimeOffset RegisteredAt { get; } = registeredAt;
        public DateTimeOffset? CancelledAt { get; set; } = cancelledAt;
    }
}
