using Demo.Data;
using Demo.Data.Entities;
using Demo.Infrastructure.Services.Notification;
using Demo.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Demo.Tests
{
    public class NotificationContextTests
    {
        [Fact]
        public async Task NotifyByTargetAsync_DevicesExist_ReturnsSuccess()
        {
            // Arrange
            var options = new DbContextOptionsBuilder<DemoDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // 每次一個新的DB
                .Options;
            using var db = new DemoDbContext(options);

            var deviceInfoId = Guid.NewGuid();
            db.DeviceInfos.Add(new DeviceInfo { DeviceId = "d1", DeviceInfoId = deviceInfoId, Status = true, Lang = "zh-TW" });
            db.NotificationTypes.Add(new NotificationType { DeviceInfoId = deviceInfoId, IsAppActive = true });
            var codeInfoId = Guid.NewGuid();
            db.CodeInfos.Add(new CodeInfo { Code = "C1", Lang = "zh-TW", CodeInfoId = codeInfoId, Title = "T", Body = "B" });
            db.NotificationMsgTemplates.Add(new NotificationMsgTemplate { NotificationMsgTemplateId = Guid.NewGuid(), CodeInfoId = codeInfoId, Gw = "" });
            db.SaveChanges();

            // Moq推播策略
            var appStrategy = new Mock<AppNotificationStrategy>(MockBehavior.Loose, (ILogger<AppNotificationStrategy>)null);
            appStrategy.Setup(x => x.SendAsync(It.IsAny<List<DeviceInfo>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<NotificationMsgTemplate>()))
                .Returns(Task.CompletedTask);

            var context = new NotificationContext(
                db,
                appStrategy.Object,
                Mock.Of<WebNotificationStrategy>(),
                Mock.Of<EmailNotificationStrategy>(),
                Mock.Of<LineNotificationStrategy>()
            );

            var request = new NotificationRequestDto
            {
                DeviceIds = new List<string> { "d1" },
                Code = "C1"
            };

            // Act
            var result = await context.NotifyByTargetAsync(request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.Contains("推播已送出", result.Message);
            appStrategy.Verify(x => x.SendAsync(It.IsAny<List<DeviceInfo>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<NotificationMsgTemplate>()), Times.Once);
        }
    }
}
