using Microsoft.EntityFrameworkCore;
using ClinicalAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────────────
// 1. ĐĂNG KÝ DbContext — kết nối Oracle DB
//    Sử dụng Oracle.EntityFrameworkCore (ODP.NET Managed)
// ─────────────────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ClinicalDbContext>(options =>
    options.UseOracle(
        builder.Configuration.GetConnectionString("OracleConnection"),
        oracleOptions =>
        {
            // Chỉ định Oracle DB version (tùy môi trường: 19c, 21c, XE...)
            oracleOptions.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19);
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
        Title = "Clinical API",
        Version = "v1",
        Description = "API quản lý hồ sơ bệnh án và danh mục thuốc — Module 2 (Oracle DB)"
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
    var db = scope.ServiceProvider.GetRequiredService<ClinicalDbContext>();
    db.Database.Migrate();
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. PIPELINE
// ─────────────────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Clinical API v1"));
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
