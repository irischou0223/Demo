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
