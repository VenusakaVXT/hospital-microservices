# Ngày 6 — Desktop Client (WinForms) & Tư Duy Bất Đồng Bộ (Async/Await)

> **Mục tiêu:** Xây dựng phần mềm trạm (Client) chạy trên máy tính của nhân viên tiếp đón, giao tiếp với hệ thống backend thông qua HTTP API, sử dụng chuẩn mã hóa bất đồng bộ để chống "đơ" giao diện.

---

## 1. Lợi Ích Của Việc Dùng API Trung Gian Thay Vì Kết Nối Thẳng Database

Trong các phần mềm Windows Forms truyền thống cổ điển (Client-Server 2 lớp), ứng dụng thường chứa chuỗi kết nối (`ConnectionString`) và chọc thẳng vào SQL Server. Việc này tiềm ẩn rủi ro cực lớn:
- **Lộ Credentials:** Hacker dùng phần mềm decompile (như dotPeek, ILSpy) có thể bẻ khóa file `.exe` và lấy cắp mật khẩu Database. Toàn bộ dữ liệu bệnh viện sẽ bị đe dọa.
- **Khó Scale & Phụ Thuộc:** Mỗi khi cấu trúc bảng trong DB thay đổi, ta phải cập nhật lại phần mềm `.exe` trên tất cả các máy trạm.

**Giải pháp Microservices + API Gateway:**
- Ứng dụng WinForms (`ReceptionClient`) **chỉ biết URL của API** (VD: `http://localhost:5004/api/patients/register`). Nó hoàn toàn mù tịt về chuỗi kết nối Database.
- Nếu có lộ mã nguồn `.exe`, hacker cũng chỉ biết được địa chỉ API. Chúng ta có thể dễ dàng chặn đứng các luồng tấn công thông qua Authentication/Authorization (OAuth2, JWT) ở tầng API.
- Cập nhật logic phía server không làm ảnh hưởng đến Client.

---

## 2. Quản Lý `HttpClient` (Tránh Socket Exhaustion)

Một sai lầm kinh điển của lập trình viên .NET là dùng `using (var client = new HttpClient())` mỗi lần gọi API. 
Khi khối `using` kết thúc, connection bị đóng nhưng TCP port trên Windows vẫn bị treo ở trạng thái `TIME_WAIT` trong khoảng 2 phút. Nếu có hàng ngàn bệnh nhân đăng ký liên tục, ứng dụng sẽ dùng cạn kiệt cổng (Socket Exhaustion) và sập mạng cục bộ.

**Cách khắc phục chuẩn nhất (.NET Core / 8+):**
Khởi tạo `HttpClient` dưới dạng **Singleton** (Dùng chung 1 object duy nhất cho toàn bộ vòng đời ứng dụng). Trong bài này, tôi đã định nghĩa nó ở `Program.cs`:
```csharp
public static readonly HttpClient ApiClient = new HttpClient { BaseAddress = new Uri("http://localhost:5004/") };
```
Biến này sẽ an toàn luồng (Thread-safe) và tái sử dụng các socket TCP tối ưu.

---

## 3. Bản Chất Của `Async/Await` Và `SynchronizationContext` Trong WinForms

Khi nhân viên ấn nút **"Đăng Ký"**, app cần mất một khoảng thời gian chờ (latency) để bắn gói tin JSON qua mạng, chui vào API, API lưu vào DB, Kafka rẽ nhánh, rồi trả về `201 Created`.

- Nếu dùng hàm đồng bộ (như `.Result` hoặc `.Wait()`), **UI Thread** (Luồng vẽ giao diện chính) sẽ bị chặn cứng lại (Block). Giao diện ứng dụng sẽ chuyển sang trạng thái *(Not Responding)* trắng xóa, nhân viên không thể di chuyển chuột hay làm gì khác.
- Bằng cách dùng **`async/await`**:
  ```csharp
  var response = await ApiClient.PostAsJsonAsync(...);
  ```
  Hành động này mang ý nghĩa: *"Tôi ném tác vụ này cho hệ điều hành xử lý (I/O Bound). Khi nào xong hãy gọi tôi lại. Bây giờ tôi sẽ trả lại UI Thread để nó tiếp tục vẽ giao diện và phản hồi chuột."*

**ĐIỀU KỲ DIỆU TỪ `SynchronizationContext`:**
Trong C# WinForms, khi lệnh `await` chờ xong và nhảy xuống dòng code tiếp theo (Ví dụ: `_btnRegister.Enabled = true;`), nó phải tương tác lại với giao diện. 
Nếu code này chạy trên 1 luồng ngầm (Background Thread), nó sẽ văng ra lỗi kinh điển: *Cross-thread operation not valid*.

Nhưng nhờ có `SynchronizationContext` đặc thù của WinForms, trình biên dịch .NET tự động ghim (capture) lại luồng UI ban đầu. Ngay khi HTTP Request hoàn tất, nó tự động "trả" phần code còn lại về đúng luồng UI (UI Thread) để bạn tha hồ thay đổi Text, đổi màu Button mà không dính lỗi!

Đó chính là sự lợi hại của `async/await` — Viết code theo luồng tuần tự nhưng chạy bất đồng bộ hoàn hảo!
