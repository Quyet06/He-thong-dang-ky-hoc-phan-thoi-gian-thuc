# Hệ thống đăng ký học phần thời gian thực (Đề tài 02)

> **Trạng thái hiện tại:** Đã hoàn thiện đến **Buổi 3** (Chương 1–3: Client–Server HTTP, RESTful API, và giao tiếp thời gian thực với SignalR).

## 1. Cấu trúc solution hiện tại

```text
CourseRegistrationSystem.slnx
src/
  Registration.Api/        # ASP.NET Core Minimal API (.NET 10) + SignalR Hub
  Student.Client/          # Console Client gọi REST API & nhận cập nhật SignalR
```

## 2. Chức năng đã triển khai (đến Buổi 3)

### REST API (`Registration.Api`)
- `GET /` : Kiểm tra trạng thái dịch vụ và địa chỉ SignalR hub.
- `GET /courses` : Tra cứu danh sách lớp học phần và số chỗ còn lại.
- `GET /courses/{id}` : Tra cứu chi tiết một lớp học phần.
- `POST /registrations` : Đăng ký lớp học phần (`400 BadRequest`, `404 NotFound`, `409 Conflict` khi trùng hoặc hết chỗ, `201 Created` khi thành công).
- `DELETE /registrations/{id}` : Hủy đăng ký và hoàn trả chỗ về lớp học phần.
- `GET /students/{studentId}/registrations` : Tra cứu danh sách học phần đã đăng ký của sinh viên.

### SignalR Hub (`/hubs/courses`)
- `WatchAllCourses()` : Đăng ký nhận thông báo cập nhật chỗ cho tất cả lớp học phần (`courses:all`).
- `WatchCourse(courseId)` / `UnwatchCourse(courseId)` : Theo dõi riêng một lớp (`course:{courseId}`).
- Server đẩy sự kiện `CourseCapacityUpdated` mỗi khi đăng ký hoặc hủy đăng ký thành công.

## 3. Hướng dẫn build & chạy thử

### Build solution
```bash
dotnet build CourseRegistrationSystem.slnx
```

### Chạy API (Terminal 1)
```bash
dotnet run --project src/Registration.Api --urls http://localhost:5000
```

### Chạy Client chế độ demo tự động (Terminal 2)
```bash
dotnet run --project src/Student.Client -- http://localhost:5000 SV001
```

### Chạy nhiều Client tương tác để xem cập nhật SignalR thời gian thực (Terminal 2 & 3)
```bash
dotnet run --project src/Student.Client -- http://localhost:5000 SV001 --interactive
dotnet run --project src/Student.Client -- http://localhost:5000 SV002 --interactive
```
