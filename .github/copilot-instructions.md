# BaseMVC – Hướng dẫn cho Copilot

## Quy trình phát triển tính năng (bắt buộc)

Khi thêm, mở rộng hoặc thay đổi bất kỳ tính năng nào (phát triển tính năng, thêm chức năng, implement feature, sửa hành vi nghiệp vụ, thêm trang/endpoint/migration/permission), **phải nạp skill `basemvc-feature-development` trước khi viết mã** và tuân thủ toàn bộ quy trình trong [SKILL.md](./skills/basemvc-feature-development/SKILL.md).

Không bỏ qua skill này vì thay đổi trông nhỏ. Nếu một bước không áp dụng được, nêu rõ lý do trong phần tóm tắt cuối cùng. Nếu yêu cầu của người dùng mâu thuẫn với skill, hỏi lại người dùng trước khi làm.

Các nguyên tắc cốt lõi (chi tiết trong skill):

- Giữ ranh giới lớp Domain / Application / Infrastructure / WebUI; controller chỉ gửi Command/Query qua `ISender`, không gọi `DbContext`.
- Mọi endpoint cần phân quyền theo `PermissionCatalog` và `[Authorize(Policy = ...)]`; POST thay đổi dữ liệu có `[ValidateAntiForgeryToken]`.
- Thêm hoặc cập nhật test xUnit, chạy build và test Release, và chỉ báo cáo các kiểm tra đã thực sự chạy.
- Không commit secret, file database hoặc backup; chỉ commit khi người dùng yêu cầu, theo Conventional Commits.
