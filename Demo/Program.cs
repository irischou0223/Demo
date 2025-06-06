using Demo.Config;
using Demo.Data;
using Demo.Infrastructure.Services;
using Demo.Infrastructure.Services.Notification;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 設定通知服務的資料庫連線
// 使用 PostgreSQL 作為資料庫提供者
// 連線字串從 appsettings.json 的 DefaultConnection 讀取
builder.Services.AddDbContext<DemoDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 郵件設定
builder.Services.Configure<SmtpConfig>(builder.Configuration.GetSection("SmtpConfig"));

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));

// DI 註冊推播策略與 Context
builder.Services.AddSingleton<ConfigCacheService>();

builder.Services.AddScoped<AppNotificationStrategy>();
builder.Services.AddScoped<WebNotificationStrategy>();
builder.Services.AddScoped<EmailNotificationStrategy>();
builder.Services.AddScoped<LineNotificationStrategy>();
builder.Services.AddScoped<NotificationContext>();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
