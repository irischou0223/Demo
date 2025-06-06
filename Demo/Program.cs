using Demo.Config;
using Demo.Data;
using Demo.Infrastructure.Services;
using Demo.Infrastructure.Services.Notification;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StackExchange.Redis;

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
