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

// ------------------------ �]�w Logging�]Serilog�^ ------------------------
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// ------------------------ �]�w��Ʈw�]PostgreSQL�^ ------------------------
// �s�u�r��q appsettings.json �� DefaultConnection Ū��
builder.Services.AddDbContext<DemoDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ------------------------ ��L�A�ȳ]�w ------------------------
// �N appsettings.json �̪� SmtpConfig �Ϭq�`�J SmtpConfig ����]�� Email �A�ȨϥΡ^
builder.Services.Configure<SmtpConfig>(builder.Configuration.GetSection("SmtpConfig"));

// ���U Redis �s�u�A�Ѿ�����ε{���@�Τ@�� ConnectionMultiplexer ��ҡ]�O����֨��B�����굥�^
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")));

// ------------------------ ���U Hangfire �A�� ------------------------
// �ϥ� PostgreSQL �x�s Hangfire ����
builder.Services.AddHangfire(configuration =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    configuration.UsePostgreSqlStorage(options =>
    {
        options.UseNpgsqlConnection(connectionString);
    });
});
builder.Services.AddHangfireServer();

// ------------------------ �̿�`�J DI ���U ------------------------
builder.Services.AddSingleton<ConfigCacheService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<AppNotificationStrategy>();
builder.Services.AddScoped<WebNotificationStrategy>();
builder.Services.AddScoped<EmailNotificationStrategy>();
builder.Services.AddScoped<LineNotificationStrategy>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<RetryService>();

// ------------------------ �s�W�G�{�� (Authentication) �]�w ------------------------
// �t�m JWT Bearer �{��
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
// �w�q���v�����A�Ҧp "AdminPolicy" �n�D�ϥΪ֦̾� "Admin" ����
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
    // �z�]�i�H�w�q��L�����A�Ҧp�G
    // options.AddPolicy("ManagerPolicy", policy => policy.RequireRole("Manager", "Admin"));
});

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ------------------------ �ҥ� Hangfire Dashboard ------------------------
// �`�N�G�w�]�u���\�����s�� /hangfire�A�p�ݶ}��Ч�� AuthorizationFilter
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

// ------------------------ ���U�Ƶ{���ȡ]RecurringJob�^ ------------------------
// �o�ӱƵ{�|�u�C�����v�۰ʩI�s�@�� ScheduleService.ExecuteScheduledJobsAsync()
// �o��k�̭��|�ˬd notification_scheduled_job ��ƪ��S������n�o�e������
// ���O�Y�ɱ����A�ȩw���ˬd�P����
RecurringJob.AddOrUpdate<ScheduleService>(
    recurringJobId: "Schedule-Service",
    methodCall: service => service.ExecuteScheduledJobsAsync(),
    cronExpression: Cron.Minutely
);

// ------------------------ ���U���ե��ȡ]RecurringJob�^ ------------------------
using (var scope = app.Services.CreateScope())
{
    var retryService = scope.ServiceProvider.GetRequiredService<RetryService>();
    // �إߤ@�� recurring job�A�C5��������@��
    RecurringJob.AddOrUpdate(
        "ProcessAllRetriesJob", // job id
        () => retryService.ProcessAllRetriesAsync(), // �n���檺��k
        "*/5 * * * *" // Cron ��F���A�C5����
    );
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ------------------------ �s�W�G����ҥ~�B�z�����n�� (Global Exception Handling Middleware) ------------------------
// ��ĳ��b UseHttpsRedirection ����A���n��b UseAuthentication �M UseAuthorization ���e�A
// �o�˥i�H�����j�������B�z�� API �޿���~�C
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

app.Run();
