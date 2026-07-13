# 🏥 Hướng Dẫn Setup Môi Trường Local — HospitalManagement

> Tài liệu này mô tả **toàn bộ các bước** để setup môi trường phát triển từ đầu.
> File `appsettings.Development.json` **KHÔNG có trong repo** (đã gitignore) — mỗi developer tự tạo theo hướng dẫn bên dưới.

---

## 🔑 Biến Cấu Hình Cần Điền Trước

> ⚠️ Thay thế tất cả các giá trị `<PLACEHOLDER>` bên dưới bằng giá trị thực tế của bạn
> trước khi chạy bất kỳ lệnh nào trong tài liệu này.

| Biến | Mô tả | Ví dụ (ĐỪNG dùng thật) |
|---|---|---|
| `<SA_PASSWORD>` | Mật khẩu admin SQL Server (SA) — phải có chữ hoa, số, ký tự đặc biệt | `Abc@12345` |
| `<ORACLE_ADMIN_PASSWORD>` | Mật khẩu admin Oracle (SYSTEM/SYS) | `OraAdmin99` |
| `<APP_USERNAME>` | Username dùng trong source code | `venusdev` |
| `<APP_PASSWORD>` | Mật khẩu user ứng dụng | `myapp_pass` |

---

## 📋 Yêu Cầu Phần Mềm

| Phần mềm | Mục đích | Link tải |
|---|---|---|
| **Docker Desktop** | Chạy SQL Server + Oracle trong container | https://www.docker.com/products/docker-desktop |
| **SSMS** | Query SQL Server | https://aka.ms/ssms |
| **DBeaver** | Query Oracle DB | https://dbeaver.io |
| **.NET 8 SDK** | Build và chạy API | https://dotnet.microsoft.com |
| **dotnet-ef tool** | Chạy database migration | `dotnet tool install --global dotnet-ef` |

---

## 🚀 PHẦN 1 — CÀI ĐẶT LẦN ĐẦU (Chỉ Làm 1 Lần)

### Bước 1 — Chạy 2 Container Database

> ⚠️ SQL Server Docker dùng port **1434** (không phải 1433) vì port 1433 thường bị chiếm bởi SQL Server cài sẵn trên máy Windows.

Mở **PowerShell** và chạy lần lượt:

```powershell
# SQL Server 2022 — port 1434
docker run -d `
  --name hospital-sqlserver `
  -e "ACCEPT_EULA=Y" `
  -e "SA_PASSWORD=<SA_PASSWORD>" `
  -p 1434:1433 `
  --restart unless-stopped `
  mcr.microsoft.com/mssql/server:2022-latest
```

```powershell
# Oracle XE 21c — port 1521
docker run -d `
  --name hospital-oracle `
  -e ORACLE_PASSWORD=<ORACLE_ADMIN_PASSWORD> `
  -p 1521:1521 `
  --restart unless-stopped `
  gvenzl/oracle-xe:21-slim
```

Kiểm tra container đang chạy:
```powershell
docker ps
```

---

### Bước 2 — Tạo User Ứng Dụng Trong SQL Server

> ⏳ Chờ **20 giây** sau khi container khởi động trước khi chạy lệnh bên dưới.

```powershell
Start-Sleep -Seconds 20
```

**Tạo Login và Database:**
```powershell
docker exec hospital-sqlserver `
  /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "<SA_PASSWORD>" -C `
  -Q "CREATE LOGIN [<APP_USERNAME>] WITH PASSWORD = N'<APP_PASSWORD>', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF; CREATE DATABASE [HmsReceptionDb];"
```

**Tạo User và phân quyền:**
```powershell
docker exec hospital-sqlserver `
  /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U SA -P "<SA_PASSWORD>" -C `
  -d HmsReceptionDb `
  -Q "CREATE USER [<APP_USERNAME>] FOR LOGIN [<APP_USERNAME>]; ALTER ROLE db_owner ADD MEMBER [<APP_USERNAME>]; PRINT 'Done!';"
```

✅ Thấy `Done!` là thành công.

**Xác nhận trong SSMS:**
```
Server name:    localhost,1434        ← dấu phẩy (không phải dấu chấm)
Authentication: SQL Server Authentication
Login:          SA
Password:       <SA_PASSWORD>
```
Expand **Databases** → phải thấy **HmsReceptionDb**.

---

### Bước 3 — Tạo Schema Ứng Dụng Trong Oracle

> ⏳ Oracle XE cần **60–90 giây** để khởi động lần đầu. Kiểm tra log trước:

```powershell
docker logs hospital-oracle --tail 10
```

Chờ thấy dòng: `Pluggable database XEPDB1 opened read write`

**Ghi file SQL vào container:**
```powershell
docker exec hospital-oracle bash -c "printf 'CREATE USER <APP_USERNAME> IDENTIFIED BY <APP_PASSWORD>;\nGRANT CONNECT, RESOURCE TO <APP_USERNAME>;\nGRANT UNLIMITED TABLESPACE TO <APP_USERNAME>;\nGRANT CREATE SESSION TO <APP_USERNAME>;\nEXIT;\n' > /tmp/setup_user.sql"
```

**Chạy file SQL:**
```powershell
docker exec hospital-oracle bash -c "sqlplus -s system/<ORACLE_ADMIN_PASSWORD>@//localhost:1521/XEPDB1 @/tmp/setup_user.sql"
```

✅ Thấy `User created. Grant succeeded.` là thành công.

---

### Bước 4 — Tạo File `appsettings.Development.json`

> File này chứa credential thật và **không được commit** lên git (đã có trong .gitignore).

**`ReceptionAPI/appsettings.Development.json`**
```json
{
  "ConnectionStrings": {
    "SqlServerConnection": "Server=localhost,1434;Database=HmsReceptionDb;User Id=<APP_USERNAME>;Password=<APP_PASSWORD>;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**`ClinicalAPI/appsettings.Development.json`**
```json
{
  "ConnectionStrings": {
    "OracleConnection": "User Id=<APP_USERNAME>;Password=<APP_PASSWORD>;Data Source=localhost:1521/XEPDB1"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

### Bước 5 — Kết Nối Tool GUI

#### SSMS — Dùng Cho SQL Server
```
Server name:    localhost,1434        ← dấu phẩy (quan trọng)
Authentication: SQL Server Authentication
Login:          <APP_USERNAME>
Password:       <APP_PASSWORD>
```

> 💡 Dùng **SSMS** thay vì DBeaver cho SQL Server. DBeaver JDBC driver không tương thích tốt với SQL Server Docker trên Windows (lỗi "Login failed" dù credentials đúng).

#### DBeaver — Dùng Cho Oracle
```
Host:           localhost
Port:           1521
Database:       XEPDB1
Type:           Service Name          ← chọn Service Name, không phải SID
Username:       <APP_USERNAME>
Password:       <APP_PASSWORD>
```
Tick ✅ **"Trust Server Certificate"** nếu có yêu cầu.

> 💡 Lần đầu kết nối, DBeaver hỏi tải **Oracle JDBC Driver** → nhấn **Download**.

---

### Bước 6 — Chạy Migration (Tạo Bảng Trong DB)

Cài `dotnet-ef` nếu chưa có:
```powershell
dotnet tool install --global dotnet-ef
```

**ReceptionAPI → tạo bảng `Patients` và `OutboxMessages` trong SQL Server:**
```powershell
cd d:\.NET\HospitalMicroservices\HospitalManagement\ReceptionAPI
dotnet ef migrations add InitialCreate
dotnet ef database update
```

✅ Vào SSMS → HmsReceptionDb → Tables → thấy `Patients` và `OutboxMessages`.

**ClinicalAPI → tạo bảng `MEDICAL_RECORDS` và `MEDICINES` trong Oracle:**
```powershell
cd d:\.NET\HospitalMicroservices\HospitalManagement\ClinicalAPI
dotnet ef migrations add InitialCreate
dotnet ef database update
```

✅ Vào DBeaver → Oracle → schema `<APP_USERNAME>` → Tables → thấy `MEDICAL_RECORDS` và `MEDICINES`.

---

### Bước 7 — Chạy API và Test Swagger

**Terminal 1 — ReceptionAPI:**
```powershell
cd d:\.NET\HospitalMicroservices\HospitalManagement\ReceptionAPI
dotnet run
```

**Terminal 2 — ClinicalAPI:**
```powershell
cd d:\.NET\HospitalMicroservices\HospitalManagement\ClinicalAPI
dotnet run
```

Swagger UI mở tại địa chỉ in ra trong terminal, ví dụ: `https://localhost:7xxx/swagger`.

---

## 🔄 PHẦN 2 — LÀM VIỆC HÀNG NGÀY

### Mỗi Sáng Bật Máy

```powershell
# Container có --restart unless-stopped nên tự khởi động khi reboot
# Nếu cần khởi động thủ công:
docker start hospital-sqlserver
docker start hospital-oracle
```

### Chạy API

```powershell
# Terminal 1 — ReceptionAPI
cd d:\.NET\HospitalMicroservices\HospitalManagement\ReceptionAPI
dotnet run

# Terminal 2 — ClinicalAPI
cd d:\.NET\HospitalMicroservices\HospitalManagement\ClinicalAPI
dotnet run
```

### Thêm Migration Mới

```powershell
cd d:\.NET\HospitalMicroservices\HospitalManagement\ReceptionAPI
dotnet ef migrations add <TenMigration>
dotnet ef database update

cd d:\.NET\HospitalMicroservices\HospitalManagement\ClinicalAPI
dotnet ef migrations add <TenMigration>
dotnet ef database update
```

---

## 📊 Thông Tin Kết Nối Tóm Tắt

| | SQL Server | Oracle |
|---|---|---|
| **Container** | `hospital-sqlserver` | `hospital-oracle` |
| **Port** | `1434` (ngoài) → `1433` (trong) | `1521` |
| **Admin user** | `SA` / `<SA_PASSWORD>` | `system` / `<ORACLE_ADMIN_PASSWORD>` |
| **App user** | `<APP_USERNAME>` / `<APP_PASSWORD>` | `<APP_USERNAME>` / `<APP_PASSWORD>` |
| **Database** | `HmsReceptionDb` | PDB: `XEPDB1`, Schema: `<APP_USERNAME>` |
| **Query tool** | **SSMS** | **DBeaver** |

---

## ⚠️ Lưu Ý Quan Trọng

- **Không commit** `appsettings.Development.json` lên GitHub (đã gitignore).
- `appsettings.json` chỉ chứa placeholder — an toàn để commit.
- SQL Server dùng port **1434** để tránh conflict với SQL Server cài sẵn trên máy (port 1433).
- Container có `--restart unless-stopped` → tự khởi động lại khi reboot máy.
- `<SA_PASSWORD>` phải có ít nhất 1 chữ hoa, 1 số và 1 ký tự đặc biệt (policy SQL Server).

---

*Cập nhật lần cuối: Ngày 1 — Database setup hoàn chỉnh ✅*
