using Demo.Config;
using Demo.Data;
using Demo.Infrastructure.Hangfire;
using Demo.Infrastructure.Services;
using Demo.Infrastructure.Services.Notification;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
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
});

// ------------------------ 設定資料庫（PostgreSQL） ------------------------
// 連線字串從 appsettings.json 的 DefaultConnection 讀取
builder.Services.AddDbContext<DemoDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ------------------------ 其他服務設定 ------------------------
// 將 appsettings.json 裡的 SmtpConfig 區段注入 SmtpConfig 物件（供 Email 服務使用）
builder.Services.Configure<SmtpConfig>(builder.Configuration.GetSection("SmtpConfig"));

// 註冊 Redis 連線，供整個應用程式共用一個 ConnectionMultiplexer 實例（記憶體快取、分散鎖等）
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));

// ------------------------ 註冊 Hangfire 服務 ------------------------
// 使用 PostgreSQL 儲存 Hangfire 任務
builder.Services.AddHangfire(configuration =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    configuration.UsePostgreSqlStorage(options =>
    {
        options.UseNpgsqlConnection(connectionString);
    });
});
builder.Services.AddHangfireServer();

// ------------------------ 依賴注入 DI 註冊 ------------------------
builder.Services.AddSingleton<ConfigCacheService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<AppNotificationStrategy>();
builder.Services.AddScoped<WebNotificationStrategy>();
builder.Services.AddScoped<EmailNotificationStrategy>();
builder.Services.AddScoped<LineNotificationStrategy>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<RetryService>();

// ------------------------ 新增：認證 (Authentication) 設定 ------------------------
// 配置 JWT Bearer 認證
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
// 定義授權策略，例如 "AdminPolicy" 要求使用者擁有 "Admin" 角色
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
    // 您也可以定義其他策略，例如：
    // options.AddPolicy("ManagerPolicy", policy => policy.RequireRole("Manager", "Admin"));
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ------------------------ 啟用 Hangfire Dashboard ------------------------
// 注意：預設只允許本機存取 /hangfire，如需開放請更改 AuthorizationFilter
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

// ------------------------ 註冊排程任務（RecurringJob） ------------------------
// 這個排程會「每分鐘」自動呼叫一次 ScheduleService.ExecuteScheduledJobsAsync()
// 這方法裡面會檢查 notification_scheduled_job 資料表有沒有到期要發送的任務
// 不是即時推播，僅定時檢查與執行
RecurringJob.AddOrUpdate<ScheduleService>(
    recurringJobId: "Schedule-Service",
    methodCall: service => service.ExecuteScheduledJobsAsync(),
    cronExpression: Cron.Minutely
);

// ------------------------ 註冊重試任務（RecurringJob） ------------------------
using (var scope = app.Services.CreateScope())
{
    var retryService = scope.ServiceProvider.GetRequiredService<RetryService>();
    // 建立一個 recurring job，每5分鐘執行一次
    RecurringJob.AddOrUpdate(
        "ProcessAllRetriesJob", // job id
        () => retryService.ProcessAllRetriesAsync(), // 要執行的方法
        "*/5 * * * *" // Cron 表達式，每5分鐘
    );
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ------------------------ 新增：全域例外處理中介軟體 (Global Exception Handling Middleware) ------------------------
// 建議放在 UseHttpsRedirection 之後，但要放在 UseAuthentication 和 UseAuthorization 之前，
// 這樣可以捕獲到大部分未處理的 API 邏輯錯誤。
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

app.Run();
