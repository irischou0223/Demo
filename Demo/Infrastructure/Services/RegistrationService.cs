using Demo.Data;
using Demo.Data.Entities;
using Demo.Models.DTOs;
using Medo;
using Microsoft.EntityFrameworkCore;

namespace Demo.Infrastructure.Services
{
    /// <summary>
    /// 裝置註冊服務
    /// 流程說明：
    /// 1. 驗證產品資訊（ProductInfo）。
    /// 2. 關閉同 device/product 下舊啟用裝置。
    /// 3. 新增新裝置資料。
    /// 4. 新增/維護 使用者資訊(UserInfo) 資料。
    /// 5. 同步通知設定（新建或複用舊設定，並指向新裝置）。
    /// 6. 儲存所有異動。
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
        /// 註冊裝置主流程
        /// </summary>
        /// <param name="request">裝置註冊資訊</param>
        /// <returns>成功：新 DeviceInfo，失敗：null 及錯誤訊息</returns>
        public async Task<(DeviceInfo?, string ErrorMessage)> RegisterDeviceAsync(RegisterRequestDto req)
        {
            try
            {
                _logger.LogInformation("Device registration started. DeviceId={DeviceId}, UserAccount={UserAccount}", req.DeviceId, req.UserAccount);

                // 1. 查詢 ProductInfo
                var product = await _db.ProductInfos.FirstOrDefaultAsync(p => p.FirebaseProjectId == req.FirebaseProjectId);

                if (product == null)
                {
                    _logger.LogWarning("No matching product found. FirebaseProjectId={FirebaseProjectId}", req.FirebaseProjectId);
                    return (null, "No matching product info found.");
                }

                // 2. 關閉舊裝置（同DeviceId、ProductInfoId 且為啟用狀態的紀錄）
                var oldActiveDeviceInfo = await _db.DeviceInfos
                    .FirstOrDefaultAsync(d => d.DeviceId == req.DeviceId && d.ProductInfoId == product.ProductInfoId && d.Status);

                Guid? oldDeviceInfoId = oldActiveDeviceInfo?.DeviceInfoId;
                Guid newDeviceInfoId = Uuid7.NewUuid7();

                if (oldActiveDeviceInfo != null)
                {
                    oldActiveDeviceInfo.Status = false;
                    oldActiveDeviceInfo.UpdateAtUtc = DateTime.UtcNow;
                    _logger.LogInformation("Old device deactivated. DeviceId={DeviceId}, ProductInfoId={ProductInfoId}, OldDeviceInfoId={OldDeviceInfoId}", req.DeviceId, product.ProductInfoId, oldDeviceInfoId);
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

                // 4. 新增/維護 使用者資訊UserInfo 資料
                var userInfoExists = await _db.UserInfos.AnyAsync(u => u.UserAccount == req.UserAccount && u.DeviceInfoId == newDeviceInfoId && u.ProductInfoId == product.ProductInfoId);

                if (!userInfoExists)
                {
                    var userInfo = new UserInfo
                    {
                        UserAccount = req.UserAccount,
                        DeviceInfoId = newDeviceInfoId,
                        ProductInfoId = product.ProductInfoId,
                        DeviceId = req.DeviceId
                    };
                    _db.UserInfos.Add(userInfo);
                    _logger.LogInformation("UserInfo created. UserAccount={UserAccount}, DeviceInfoId={DeviceInfoId}, ProductInfoId={ProductInfoId}", req.UserAccount, newDeviceInfoId, product.ProductInfoId);
                }
                else
                {
                    _logger.LogInformation("UserInfo already exists. UserAccount={UserAccount}, DeviceInfoId={DeviceInfoId}, ProductInfoId={ProductInfoId}", req.UserAccount, newDeviceInfoId, product.ProductInfoId);
                }

                // 5. 同步通知啟用設定
                NotificationType? notificationType = null;
                if (oldDeviceInfoId.HasValue)
                {
                    notificationType = await _db.NotificationTypes.FirstOrDefaultAsync(x => x.DeviceInfoId == oldDeviceInfoId.Value);
                }

                if (notificationType == null)
                {
                    // 新裝置建立新通知設定
                    notificationType = new NotificationType
                    {
                        NotificationTypeId = Uuid7.NewUuid7(),
                        DeviceInfoId = newDeviceInfoId,
                        IsAppActive = req.IsAppActive,
                        IsWebActive = req.IsWebActive,
                        IsEmailActive = req.IsEmailActive,
                        IsLineActive = req.IsLineActive
                    };
                    _db.NotificationTypes.Add(notificationType);
                    _logger.LogInformation("Created new notification type for device. DeviceInfoId={DeviceInfoId}", newDeviceInfoId);
                }
                else
                {
                    // 複用舊設定，指向新裝置，並更新推播方式
                    notificationType.DeviceInfoId = newDeviceInfoId;
                    notificationType.IsAppActive = req.IsAppActive;
                    notificationType.IsWebActive = req.IsWebActive;
                    notificationType.IsEmailActive = req.IsEmailActive;
                    notificationType.IsLineActive = req.IsLineActive;
                    _logger.LogInformation("Updated notification type and reassigned to new device. OldDeviceInfoId={OldDeviceInfoId}, NewDeviceInfoId={NewDeviceInfoId}", oldDeviceInfoId, newDeviceInfoId);
                }

                await _db.SaveChangesAsync();
                _logger.LogInformation("Device registration completed. NewDeviceInfoId={NewDeviceInfoId}", newDeviceInfo.DeviceInfoId);

                return (newDeviceInfo, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Device registration failed. DeviceId={DeviceId}, UserAccount={UserAccount}", req.DeviceId, req.UserAccount);
                return (null, "Unexpected error occurred during device registration. Please try again later.");
            }
        }
    }
}
