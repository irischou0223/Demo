using Demo.Data;
using Demo.Data.Entities;
using Demo.Models.DTOs;
using Microsoft.EntityFrameworkCore;

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
        public async Task<DeviceInfo?> RegisterDeviceAsync(RegisterDeviceRequest req)
        {
            try
            {
                _logger.LogInformation("開始註冊 DeviceId={DeviceId}, Account={UserAccount}", req.DeviceId, req.UserAccount);

                // 1. 查詢ProductInfo
                var product = await _db.ProductInfos .FirstOrDefaultAsync(p => p.FirebaseProjectId == req.FirebaseProjectId);

                if (product == null)
                {
                    _logger.LogWarning("找不到對應Product: {FirebaseProjectId}", req.FirebaseProjectId);
                    return null;
                }

                // 2. 關閉舊裝置（同DeviceId、ProductInfoId）
                var oldActive = await _db.DeviceInfos
                    .FirstOrDefaultAsync(d => d.DeviceId == req.DeviceId && d.ProductInfoId == product.ProductInfoId && d.Status);

                if (oldActive != null)
                {
                    oldActive.Status = false;
                    oldActive.UpdateAtUtc = DateTime.UtcNow;
                    _logger.LogInformation("舊裝置已設為不啟用 DeviceId={DeviceId}, ProductInfoId={ProductInfoId}", req.DeviceId, product.ProductInfoId);
                }

                // 3. 新增新裝置資料
                var device = new DeviceInfo
                {
                    DeviceInfoId = Guid.NewGuid(),
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
                _db.DeviceInfos.Add(device);

                // 4. 新增/更新通知啟用設定
                var notifyType = await _db.NotificationTypes.FirstOrDefaultAsync(x => x.DeviceInfoId == device.DeviceInfoId);

                if (notifyType == null)
                {
                    notifyType = new NotificationType
                    {
                        NotificationTypeId = Guid.NewGuid(),
                        DeviceInfoId = device.DeviceInfoId,
                        IsAppActive = req.IsAppActive,
                        IsWebActive = req.IsWebActive,
                        IsEmailActive = req.IsEmailActive,
                        IsLineActive = req.IsLineActive
                    };
                    _db.NotificationTypes.Add(notifyType);
                }
                else
                {
                    notifyType.IsAppActive = req.IsAppActive;
                    notifyType.IsWebActive = req.IsWebActive;
                    notifyType.IsEmailActive = req.IsEmailActive;
                    notifyType.IsLineActive = req.IsLineActive;
                }

                await _db.SaveChangesAsync();

                _logger.LogInformation("註冊完成 DeviceId={DeviceId}", req.DeviceId);

                return device;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "裝置註冊失敗 DeviceId={DeviceId}, Account={UserAccount}", req.DeviceId, req.UserAccount);
                throw;
            }
        }
    }
}
