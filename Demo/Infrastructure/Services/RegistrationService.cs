using Demo.Data;
using Demo.Data.Entities;
using Demo.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Xunit.Sdk;

namespace Demo.Infrastructure.Services
{
    /// <summary>
    /// 裝置註冊/狀態管理服務
    /// </summary>
    public class RegistrationService
    {
        private readonly DemoDbContext _db;
        private readonly ILogger<RegistrationService> _logger;

        public RegistrationService(DemoDbContext db, ILogger<RegistrationService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 裝置註冊流程：同產品同裝置存在時將原紀錄設為不啟用，並新增新啟用裝置
        /// </summary>
        public async Task<(DeviceInfo?, string ErrorMessage)> RegisterDeviceAsync(RegisterDeviceRequest req)
        {
            try
            {
                _logger.LogInformation("開始註冊 DeviceId={DeviceId}, Account={UserAccount}", req.DeviceId, req.UserAccount);

                // 1. 查詢 ProductInfo
                var product = await _db.ProductInfos.FirstOrDefaultAsync(p => p.FirebaseProjectId == req.FirebaseProjectId);

                if (product == null)
                {
                    _logger.LogWarning("找不到對應Product: {FirebaseProjectId}", req.FirebaseProjectId);
                    return (null, "找不到對應的產品資訊。");
                }

                // 2. 關閉舊裝置（同DeviceId、ProductInfoId 且為啟用狀態的紀錄）
                var oldActiveDeviceInfo = await _db.DeviceInfos
                    .FirstOrDefaultAsync(d => d.DeviceId == req.DeviceId && d.ProductInfoId == product.ProductInfoId && d.Status);

                Guid? oldDeviceInfoId = oldActiveDeviceInfo?.DeviceInfoId;
                Guid newDeviceInfoId = Guid.NewGuid();

                if (oldActiveDeviceInfo != null)
                {
                    oldActiveDeviceInfo.Status = false;
                    oldActiveDeviceInfo.UpdateAtUtc = DateTime.UtcNow;
                    _logger.LogInformation("舊裝置已設為不啟用 DeviceId={DeviceId}, ProductInfoId={ProductInfoId}, OldDeviceInfoId={OldDeviceInfoId}", req.DeviceId, product.ProductInfoId, oldDeviceInfoId);
                }

                // 3. 新增新裝置資料
                var newDeviceInfo = new DeviceInfo
                {
                    DeviceInfoId = newDeviceInfoId,
                    DeviceId = req.DeviceId,
                    ProductInfoId = product.ProductInfoId,
                    AppVersion = req.AppVersion,
                    FcmToken = req.FcmToken,
                    NotificationGroup = req.NotificationGroup,
                    Mobile = req.Mobile,
                    Gw = req.Gw,
                    Email = req.Email,
                    LineId = req.UserAccount,
                    MsgArm = req.MsgArm,
                    MsgDisArm = req.MsgDisarm,
                    Alarm = req.Alarm,
                    Panic = req.Panic,
                    Status = true,
                    Lang = req.Lang,
                    CreateAtUtc = DateTime.UtcNow,
                    UpdateAtUtc = null
                };
                _db.DeviceInfos.Add(newDeviceInfo);

                // 4. 新增/更新通知啟用設定
                NotificationType? notificationType = null;
                if (oldDeviceInfoId.HasValue)
                {
                    notificationType = await _db.NotificationTypes.FirstOrDefaultAsync(x => x.DeviceInfoId == oldDeviceInfoId.Value);
                }

                if (notificationType == null)
                {
                    notificationType = new NotificationType
                    {
                        NotificationTypeId = Guid.NewGuid(),
                        DeviceInfoId = newDeviceInfoId,
                        IsAppActive = req.IsAppActive,
                        IsWebActive = req.IsWebActive,
                        IsEmailActive = req.IsEmailActive,
                        IsLineActive = req.IsLineActive
                    };
                    _db.NotificationTypes.Add(notificationType);
                    _logger.LogInformation("為新裝置建立新的通知設定。");
                }
                else
                {
                    notificationType.DeviceInfoId = newDeviceInfoId;
                    notificationType.IsAppActive = req.IsAppActive;
                    notificationType.IsWebActive = req.IsWebActive;
                    notificationType.IsEmailActive = req.IsEmailActive;
                    notificationType.IsLineActive = req.IsLineActive;
                    _logger.LogInformation("更新舊裝置的通知設定並將其關聯到新裝置（從 {OldDeviceInfoId} 變更為新的 DeviceInfoId）", oldDeviceInfoId);
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("裝置註冊流程完成。 新裝置 ID={NewDeviceInfoId}", newDeviceInfo.DeviceInfoId);

                return (newDeviceInfo, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "裝置註冊失敗 DeviceId={DeviceId}, Account={UserAccount}", req.DeviceId, req.UserAccount);
                throw;
            }
        }
    }
}
