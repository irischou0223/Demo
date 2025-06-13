using Demo.Data;
using Demo.Infrastructure.Hangfire;
using Demo.Infrastructure.Services;
using Demo.Infrastructure.Services.Notification;
using Demo.Infrastructure.Workers;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ------------------------ 設定 Logging（Serilog） ------------------------
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
    // Info: 讀取 appsettings.json 的 Serilog 設定，寫入 Console 與 File，並加入機器資訊等 Enrich。
});

// ------------------------ 設定資料庫（PostgreSQL） ------------------------
// Info: 連線字串從 appsettings.json 的 DefaultConnection 讀取
builder.Services.AddDbContext<DemoDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Info: 註冊 Redis 連線
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));

// ------------------------ 註冊 Hangfire 服務 ------------------------
// Info: 使用 PostgreSQL 儲存 Hangfire 任務
builder.Services.AddHangfire(cfg =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    cfg.UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(conn));
});
builder.Services.AddHangfireServer();

// ------------------------ MemoryCache 註冊（解決 IMemoryCache 問題） ------------------------
builder.Services.AddMemoryCache();

// ------------------------ 依賴注入 DI 註冊 ------------------------
builder.Services.AddScoped<ConfigCacheService>();
builder.Services.AddScoped<NotificationQueueService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<AppNotificationStrategy>();
builder.Services.AddScoped<WebNotificationStrategy>();
builder.Services.AddScoped<EmailNotificationStrategy>();
builder.Services.AddScoped<LineNotificationStrategy>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<RetryService>();
builder.Services.AddScoped<NotificationLogQueueService>();
builder.Services.AddHostedService<NotificationQueueWorker>();
builder.Services.AddHostedService<NotificationLogQueueWorker>();

// ------------------------ 新增：認證 (Authentication) 設定 ------------------------
// Info: 配置 JWT Bearer 認證
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, // 驗證發行者
            ValidateAudience = true, // 驗證受眾
            ValidateLifetime = true, // 驗證 Token 的生命週期 (過期時間)
            ValidateIssuerSigningKey = true, // 驗證簽章金鑰 (確保 Token 沒被篡改)

            // 從 appsettings.json 讀取配置
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// ------------------------ 新增：授權 (Authorization) 設定 ------------------------
// Info: 定義授權策略，例如 "AdminPolicy" 要求 Admin 角色
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
    // 可擴充自定義多個 Policy
    // options.AddPolicy("ManagerPolicy", policy => policy.RequireRole("Manager", "Admin"));
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ------------------------ 確保資料庫已建立（僅 demo/測試用） ------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
    var created = db.Database.EnsureCreated();
    app.Logger.LogInformation("EnsureCreated called, result: {Created}", created);
}

// ------------------------ 啟用 Hangfire Dashboard ------------------------
// Info: 只允許本機存取，如需開放請更改 AuthorizationFilter
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

// ------------------------ 註冊排程任務（RecurringJob） ------------------------
// Info: 這個排程會「每分鐘」自動呼叫一次 ScheduleService.ExecuteScheduledJobsAsync()
RecurringJob.AddOrUpdate<ScheduleService>(
    recurringJobId: "Schedule-Service",
    methodCall: service => service.ExecuteScheduledJobsAsync(),
    cronExpression: Cron.Minutely
);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ------------------------ 新增：全域例外處理中介軟體 (Global Exception Handling Middleware) ------------------------
// Info: 建議放在 UseHttpsRedirection 之後，但在 UseAuthentication/UseAuthorization 之前
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";

        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        // 使用 Serilog 記錄錯誤日誌
        app.Logger.LogError(exception, "An unhandled exception occurred at {Path}: {Message}",
            exceptionHandlerPathFeature?.Path, exception?.Message);

        await context.Response.WriteAsJsonAsync(new
        {
            StatusCode = context.Response.StatusCode,
            Message = "An unexpected error occurred. Please try again later.",
            // 在開發環境下，可以包含詳細錯誤資訊，但生產環境不建議
            // Details = app.Environment.IsDevelopment() ? exception?.StackTrace : null
        });
    });
});

app.UseHttpsRedirection();

// ------------------------ 新增：認證中介軟體 (Authentication Middleware) ------------------------
// 必須放在 UseAuthorization 之前
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Logger.LogInformation("[ Program ] 應用程式啟動完成，環境: {Environment}", app.Environment.EnvironmentName);

app.Run();
