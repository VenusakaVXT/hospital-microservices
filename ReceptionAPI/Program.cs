using Microsoft.EntityFrameworkCore;
using ReceptionAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────────────
// 1. ĐĂNG KÝ DbContext — kết nối MS SQL Server
//    Connection string được đọc từ appsettings.json (hoặc biến môi trường)
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ReceptionDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("SqlServerConnection"),
        sqlOptions =>
        {
            // Tự động retry khi SQL Server tạm thời không phản hồi (transient errors)
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

// ─────────────────────────────────────────────────────────────────────────────
// 2. CÁC DỊCH VỤ TIÊU CHUẨN
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Reception API",
        Version = "v1",
        Description = "API quản lý thông tin hành chính bệnh nhân — Module 1 (MS SQL)"
    });
});

// ─────────────────────────────────────────────────────────────────────────────
// 3. LOGGING
// ─────────────────────────────────────────────────────────────────────────────
builder.Logging.AddConsole();

var app = builder.Build();

// ─────────────────────────────────────────────────────────────────────────────
// 4. TỰ ĐỘNG MIGRATE DATABASE KHI KHỞI ĐỘNG (Development only)
// ─────────────────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ReceptionDbContext>();
    db.Database.Migrate(); // Chạy toàn bộ migration chưa được áp dụng
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. PIPELINE
// ─────────────────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Reception API v1"));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
