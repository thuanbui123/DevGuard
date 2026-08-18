# 🛡️ DevGuard - Code Security Scanner

**DevGuard** là giải pháp quét mã nguồn tự động dành cho ứng dụng **.NET Core / C#**, tích hợp trí tuệ nhân tạo (Google Gemini AI) để phát hiện sớm các lỗ hổng bảo mật, rò rỉ khóa bí mật (Secret Leaks) và lỗi phần mềm trong quy trình phát triển.

---

## 🎯 Mục đích dự án (Project Purpose)

- **Phát hiện Rò rỉ Thông tin Bí mật (Secret Scanner):** Quét và cảnh báo hardcoded API Keys, Passwords, Connection Strings, JWT Tokens trong source code.
- **Phân tích Lỗ hổng bằng AI (AI Vulnerability Analysis):** Tích hợp Google Gemini Service để kiểm tra ngữ cảnh mã nguồn, phát hiện các lỗ hổng OWASP (SQL Injection, XSS, CSRF, Insecure Deserialization,...).
- **Kiểm tra Thư viện Phụ thuộc (SCA):** Cảnh báo các gói phụ thuộc chứa lỗ hổng bảo mật nổi tiếng (CVE / GHSA).
- **Quản lý Lịch sử Quét:** Lưu trữ toàn bộ lịch sử các đợt quét và chi tiết danh sách lỗi phát hiện vào CSDL.

---

## 🚀 Thành quả hiện tại (Current Achievements)

| Thành phần | Chi tiết đạt được | Trạng thái |
| :--- | :--- | :---: |
| **Database Architecture** | Thiết kế CSDL SQL Server với EF Core (`ScanHistories` 1-N `CodeIssues`). Khởi tạo Migration thành công. | ✅ Completed |
| **Core Pipeline Engine** | Xây dựng pipeline tích hợp đa dịch vụ (`FileFilterService`, `SecretScannerService`, `GeminiService` hỗ trợ điều phối luồng với `SemaphoreSlim`). | ✅ Completed |
| **RESTful API Endpoint** | Cung cấp API `POST /api/scan/start` nhận tham số `folderPath`, trả về thông tin lượt quét bất đồng bộ. | ✅ Completed |
| **Frontend Razor View** | Giao diện điều khiển trực quan, nhập đường dẫn dự án, hiệu ứng loading spinner và hiển thị kết quả quét đếm lỗi theo bảng. | ✅ Completed |

---

## 🔮 Tính năng trong tương lai (Future Roadmap)

- [ ] **Báo cáo Lịch sử Quét & Xuất File:** Cho phép truy xem lịch sử quét theo thời gian và xuất báo cáo PDF/Excel chi tiết.
- [ ] **Gợi ý Sửa lỗi tự động (AI Auto-Fix):** Tích hợp tính năng đề xuất đoạn code khắc phục lỗi trực tiếp từ Gemini AI.
- [ ] **Tích hợp Git & CI/CD Pipeline:** Hỗ trợ quét tự động khi push code lên GitHub/GitLab hoặc chạy trong GitHub Actions.
- [ ] **Dashboard Thống kê:** Hiệu chỉnh biểu đồ thống kê xu hướng lỗ hổng theo dự án/thời gian.

---

## 💻 Hướng dẫn Khởi chạy (Quick Start)

### 1. Cấu hình CSDL & Migration
Đảm bảo đã cấu hình `ConnectionString` trong `appsettings.json`, sau đó chạy các lệnh sau trong Package Manager Console hoặc Terminal:

```bash
dotnet ef migrations add AddScanHistoryTable
dotnet ef database update