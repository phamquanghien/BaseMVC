---
name: basemvc-feature-development
description: 'Use when adding, extending, or changing a feature in this BaseMVC project; includes phát triển tính năng, thêm chức năng, implement feature, and feature development.'
argument-hint: 'Mô tả tính năng, người dùng liên quan và kết quả mong muốn'
---

# BaseMVC Feature Development

## Mục tiêu

Thực hiện thay đổi tính năng hoàn chỉnh, đúng kiến trúc hiện tại (Clean Architecture + MediatR Command/Query), có kiểm thử phù hợp và không làm ảnh hưởng dữ liệu hoặc hành vi ngoài phạm vi yêu cầu.

## Quy trình

1. **Nắm trạng thái và quy ước**
    - Kiểm tra `git status` trước khi sửa; giữ nguyên thay đổi chưa commit của người dùng.
    - Đọc `README.md` và lần theo luồng tính năng tương tự từ controller/view tới Application, Infrastructure và kiểm thử.
    - Chỉ đọc các file liên quan; tìm helper, kiểu dữ liệu, quy tắc phân quyền, cách đăng ký DI và test fixture có sẵn trước khi thêm cách làm mới.

2. **Làm rõ kết quả cần đạt**
    - Chuyển yêu cầu thành các hành vi và tiêu chí chấp nhận có thể kiểm chứng.
    - Nếu có lựa chọn ảnh hưởng đáng kể tới phạm vi, UX, dữ liệu, phân quyền hoặc tương thích, hỏi người dùng trước khi quyết định. Với chi tiết nhỏ, theo mặc định và mẫu sẵn có của dự án.
    - Tóm tắt phạm vi triển khai và các trường hợp biên quan trọng trước khi sửa.

3. **Thiết kế theo kiến trúc và mẫu Command/Query hiện có**
    - **Phân ranh giới các lớp:** `Domain` (hiện gần như trống; chỉ thêm khi có mô hình nghiệp vụ thuần thật sự); `Application` (Command/Query, handler MediatR, DTO, interface dịch vụ); `Infrastructure` (EF Core, `ApplicationDbContext`, Identity, SQLite, triển khai dịch vụ, ghi audit); `WebUI` (controller, request/view model, Razor view, Tailwind).
    - **Mẫu Command/Query (tham khảo `src/BaseMVC.Application/Roles/`):**
        - Mỗi feature là một thư mục trong `Application` (vd. `Users`, `Roles`, `Permissions`, `Auditing`) gồm `<Feature>Requests.cs` (record `IRequest<TResponse>`: `XxxCommand` cho thao tác thay đổi trạng thái, `XxxQuery` cho thao tác đọc), `<Feature>Handlers.cs`, `<Feature>Dtos.cs` và `I<Feature>Service.cs` (hoặc `I...ReadStore`).
        - Handler mỏng, chỉ ủy quyền cho interface; triển khai thật nằm ở `Infrastructure` và được đăng ký trong `src/BaseMVC.Infrastructure/DependencyInjection.cs`. Handler tự được MediatR quét từ assembly `Application`, không cần đăng ký thủ công.
        - Thao tác thay đổi dữ liệu truyền `ActorUserId` (từ `ClaimTypes.NameIdentifier`) và ghi nhật ký qua `IAuditLogWriter` với `AuditEventType`/`AuditOutcome` phù hợp (thêm giá trị enum mới nếu cần).
        - Kết quả nghiệp vụ trả về kiểu `...OperationResult` thay vì ném exception cho lỗi dự kiến.
        - **Không gọi `DbContext` trong controller.** Controller chỉ inject `ISender` và gửi Command/Query.
        - Dự án chưa dùng FluentValidation; không thêm thư viện mới nếu không được yêu cầu. Xác thực hình thức đầu vào bằng DataAnnotations ở request model trong `WebUI/Models/<Feature>/`, còn quy tắc nghiệp vụ (trùng tên, ràng buộc Identity...) kiểm tra trong dịch vụ ở Infrastructure.
    - **Controller và giao diện:** các trang quản trị nằm ở `WebUI/Controllers/Admin/` (route `Admin/<Feature>`), view ở `Views/Admin/<Feature>/Index.cshtml`; trang trả về view, dữ liệu qua endpoint JSON (`[FromQuery]`/`[FromBody]`). Thêm liên kết điều hướng vào `Views/Shared/_Layout.cshtml` (cả menu desktop và mobile) kèm kiểm tra quyền như các mục hiện có. Văn bản hiển thị cho người dùng viết bằng tiếng Việt.
    - **Phân quyền:** thêm permission mới vào `PermissionCatalog.All` (dạng `Module.Action`, ví dụ `Permissions.Reports.Export`; tên phải qua `PermissionClaim.IsValid`), cân nhắc `PermissionCatalog.ForRole` cho `SuperAdmin`/`Administrator`, rồi bảo vệ endpoint bằng `[Authorize(Policy = "Permissions.<Module>.<Action>")]`. Kiểm tra xem permission có được đồng bộ/seed cho role hiện hữu hay không và nêu rõ trong tóm tắt.
    - Khi thay đổi schema, tạo EF Core migration trong `src/BaseMVC.Infrastructure/Persistence/Migrations`, rà soát migration và model snapshot. Không tự áp dụng migration lên môi trường production.
    - Nếu thay đổi giao diện, giữ quy ước Razor/Tailwind hiện có; chỉ biên dịch CSS khi sửa nguồn CSS hoặc cấu hình Tailwind.

4. **Triển khai an toàn, tối thiểu**
    - Thực hiện thay đổi end-to-end cho các bề mặt có liên quan (request/handler, dịch vụ Infrastructure, xác thực đầu vào, quyền truy cập, dữ liệu, giao diện và thông báo lỗi).
    - Giữ nguyên hợp đồng và hành vi hiện tại trừ khi yêu cầu chủ ý thay đổi chúng. Không tạo fallback thành công giả, nuốt lỗi, hoặc trả về dữ liệu không hợp lệ một cách im lặng.
    - Với tính năng cần đăng nhập hoặc quyền quản trị, kiểm tra cách dùng authorization/permission hiện có ở cả endpoint (`[Authorize(Policy = ...)]`) và giao diện (`IAuthorizationService` trong view). Không coi việc ẩn nút trên UI là kiểm soát truy cập.
    - Mọi action POST thay đổi dữ liệu phải có `[ValidateAntiForgeryToken]` (client gửi token như các view admin hiện có), xác thực dữ liệu phía server và không đưa bí mật, thông tin cá nhân nhạy cảm, file database hay backup runtime vào source control.
    - Không thay đổi cấu hình bảo mật, seed dữ liệu, hoặc quy tắc deploy ngoài phạm vi tính năng.

5. **Kiểm thử theo tiêu chí chấp nhận**
    - Bổ sung hoặc cập nhật test cho hành vi mới: trường hợp hợp lệ, không hợp lệ và quyền truy cập (chưa đăng nhập, thiếu quyền, đủ quyền). Dự án hiện chưa có test unit riêng cho handler; test tập trung vào endpoint và phân quyền.
    - Dùng xUnit trong `tests/BaseMVC.Infrastructure.Tests`: `WebUI/UserManagementEndpointTests.cs` (luồng HTTP qua `UserManagementWebApplicationFactory` với SQLite) và `Authorization/PermissionAuthorizationTests.cs` (policy động, SQLite in-memory). Tái sử dụng các factory/helper này; giữ test cô lập, xác định và không phụ thuộc database runtime (`basemvc.db`, `App_Data`).
    - Chạy test tập trung trước nếu có thể, sau đó build solution và chạy test project:
        ```sh
        dotnet build BaseMVC.sln --configuration Release
        dotnet test tests/BaseMVC.Infrastructure.Tests/BaseMVC.Infrastructure.Tests.csproj --configuration Release
        ```
    - Nếu sửa Tailwind input hoặc cấu hình CSS, chạy:
        ```sh
        npm --prefix src/BaseMVC.WebUI run build:css
        ```
    - Nếu lệnh kiểm tra thất bại, xác định lỗi do thay đổi hay lỗi nền; sửa lỗi do thay đổi và chạy lại kiểm tra phù hợp. Nêu rõ mọi kiểm tra chưa chạy hoặc còn thất bại.

6. **Rà soát trước khi kết thúc**
    - Xem diff để xác nhận thay đổi nằm trong phạm vi, không mất sửa đổi của người dùng, không để lại mã/debug thừa và không chứa secret, database hoặc backup.
    - Kiểm tra request/handler, dịch vụ và đăng ký DI, migration, permission, audit, menu và test đã đầy đủ, đúng vị trí. Cập nhật `README.md` nếu có cấu hình hoặc hành vi vận hành mới.
    - Chỉ commit khi người dùng yêu cầu; khi đó dùng Conventional Commits (`feat`, `fix`, `refactor`, `test`, `docs`, `chore`).
    - Tóm tắt tính năng đã hoàn thành, các phần chính đã sửa, lệnh kiểm tra cùng kết quả; ghi rõ giới hạn hoặc quyết định còn cần người dùng xác nhận. Không nói đã chạy kiểm tra nếu thực tế chưa chạy.

## Đặc điểm dự án cần giữ

- Solution: `BaseMVC.sln`; ứng dụng chính: `src/BaseMVC.WebUI`; test project duy nhất: `tests/BaseMVC.Infrastructure.Tests` (thư mục `Authorization`, `WebUI`).
- MediatR 12 trong `BaseMVC.Application`, tổ chức theo feature folder; ASP.NET Core Identity + EF Core SQLite; UI Razor + Tailwind/daisyUI (`src/BaseMVC.WebUI/Styles/site.css` → `wwwroot/css/site.css`).
- Các project nhắm tới `net10.0`, bật nullable reference types và implicit usings.
- Persistence mặc định là SQLite; migration/model đặt trong `src/BaseMVC.Infrastructure`.
- CI hiện restore, build Release và test `BaseMVC.Infrastructure.Tests`; thay đổi phải tiếp tục tương thích với quy trình này.
