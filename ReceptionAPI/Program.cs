using Microsoft.EntityFrameworkCore;
using ReceptionAPI.BackgroundServices;
using ReceptionAPI.Data;
using ReceptionAPI.Services;

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
// 3. ĐĂNG KÝ SERVICES — NGÀY 2: OUTBOX PATTERN
// ─────────────────────────────────────────────────────────────────────────────
// PatientService: Scoped — tạo mới mỗi HTTP request (an toàn với DbContext)
builder.Services.AddScoped<IPatientService, PatientService>();

// OutboxPublisherWorker: Hosted Service (Singleton) chạy ngầm suốt vòng đời app
// Dùng IServiceScopeFactory bên trong để tạo scope cho DbContext
builder.Services.AddHostedService<OutboxPublisherWorker>();

// ─────────────────────────────────────────────────────────────────────────────
// 4. LOGGING
// ─────────────────────────────────────────────────────────────────────────────
builder.Logging.AddConsole();

var app = builder.Build();

// ─────────────────────────────────────────────────────────────────────────────
// 5. TỰ ĐỘNG MIGRATE DATABASE KHI KHỞI ĐỘNG (Development only)
//    LƯU Ý: Nếu dùng restricted user (venusdev), DB phải được tạo trước bằng SA.
//    → Lần đầu setup, chạy thủ công: dotnet ef database update
// ─────────────────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ReceptionDbContext>();
    try
    {
        db.Database.Migrate();
        app.Logger.LogInformation("Database migration applied successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(
            "Auto-migration skipped: {Message}. Run 'dotnet ef database update' manually.",
            ex.Message);
    }
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
