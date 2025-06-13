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

// ------------------------ �]�w Logging�]Serilog�^ ------------------------
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
    // Info: Ū�� appsettings.json �� Serilog �]�w�A�g�J Console �P File�A�å[�J������T�� Enrich�C
});

// ------------------------ �]�w��Ʈw�]PostgreSQL�^ ------------------------
// Info: �s�u�r��q appsettings.json �� DefaultConnection Ū��
builder.Services.AddDbContext<DemoDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Info: ���U Redis �s�u
builder.Services.AddSingleton<IConnectionMultiplexer>(sp => ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));

// ------------------------ ���U Hangfire �A�� ------------------------
// Info: �ϥ� PostgreSQL �x�s Hangfire ����
builder.Services.AddHangfire(cfg =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    cfg.UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(conn));
});
builder.Services.AddHangfireServer();

// ------------------------ MemoryCache ���U�]�ѨM IMemoryCache ���D�^ ------------------------
builder.Services.AddMemoryCache();

// ------------------------ �̿�`�J DI ���U ------------------------
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

// ------------------------ �s�W�G�{�� (Authentication) �]�w ------------------------
// Info: �t�m JWT Bearer �{��
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, // ���ҵo���
            ValidateAudience = true, // ���Ҩ���
            ValidateLifetime = true, // ���� Token ���ͩR�g�� (�L���ɶ�)
            ValidateIssuerSigningKey = true, // ����ñ�����_ (�T�O Token �S�Q�y��)

            // �q appsettings.json Ū���t�m
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// ------------------------ �s�W�G���v (Authorization) �]�w ------------------------
// Info: �w�q���v�����A�Ҧp "AdminPolicy" �n�D Admin ����
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
    // �i�X�R�۩w�q�h�� Policy
    // options.AddPolicy("ManagerPolicy", policy => policy.RequireRole("Manager", "Admin"));
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ------------------------ �T�O��Ʈw�w�إߡ]�� demo/���եΡ^ ------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
    var created = db.Database.EnsureCreated();
    app.Logger.LogInformation("EnsureCreated called, result: {Created}", created);
}

// ------------------------ �ҥ� Hangfire Dashboard ------------------------
// Info: �u���\�����s���A�p�ݶ}��Ч�� AuthorizationFilter
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

// ------------------------ ���U�Ƶ{���ȡ]RecurringJob�^ ------------------------
// Info: �o�ӱƵ{�|�u�C�����v�۰ʩI�s�@�� ScheduleService.ExecuteScheduledJobsAsync()
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

// ------------------------ �s�W�G����ҥ~�B�z�����n�� (Global Exception Handling Middleware) ------------------------
// Info: ��ĳ��b UseHttpsRedirection ����A���b UseAuthentication/UseAuthorization ���e
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";

        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature?.Error;

        // �ϥ� Serilog �O�����~��x
        app.Logger.LogError(exception, "An unhandled exception occurred at {Path}: {Message}",
            exceptionHandlerPathFeature?.Path, exception?.Message);

        await context.Response.WriteAsJsonAsync(new
        {
            StatusCode = context.Response.StatusCode,
            Message = "An unexpected error occurred. Please try again later.",
            // �b�}�o���ҤU�A�i�H�]�t�Բӿ��~��T�A���Ͳ����Ҥ���ĳ
            // Details = app.Environment.IsDevelopment() ? exception?.StackTrace : null
        });
    });
});

app.UseHttpsRedirection();

// ------------------------ �s�W�G�{�Ҥ����n�� (Authentication Middleware) ------------------------
// ������b UseAuthorization ���e
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Logger.LogInformation("[ Program ] ���ε{���Ұʧ����A����: {Environment}", app.Environment.EnvironmentName);

app.Run();
