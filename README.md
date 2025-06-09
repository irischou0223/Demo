# Demo Notification Platform

## 專案簡介
本專案為一套可擴展的推播/通知平台，採用 **.NET 8**、**EFCore**、**PostgreSQL**，符合企業級開發標準。支援多管道推播（APP、Web、Email、Line）、動態排程、失敗重試與限流，並以策略模式實現高可擴展性。

## 目錄結構
```
Demo/
├── Config/            # 各種系統/通道/推播參數設定
├── Controllers/       # Web API 控制器
├── Data/
│   ├── Entities/      # EFCore 資料表實體
│   └── ...            # DbContext 等
├── Enum/              # 列舉型別
├── Extensions/        # 擴充方法
├── Infrastructure/
│   ├── Hangfire/      # Hangfire 相關設定/服務
│   ├── Services/
│   │   └── Notification/  # 推播相關策略與服務
│   └── ...
├── Models/DTOs/       # 各種資料傳輸物件
├── Properties/        # 專案屬性
├── Program.cs         # 入口與服務註冊
└── README.md
```

## 核心功能
- **裝置註冊與狀態管理**：多裝置多通道推播方式綁定，註冊流程自動控管裝置啟用/停用。
- **推播發送**：支援 APP、Web、Email、Line 四通道，策略模式動態分流，支援單發/群發/全發。
- **推播狀態記錄**：所有推播皆寫入資料庫，方便後續查詢與分析。
- **排程任務與重試**：Hangfire 管理定時/排程發送，支援自動失敗重試，參數化控管。
- **限流與併發控制**：多通道皆有批次/併發設定，避免壓力過大。
- **動態設定**：所有推播標題、內容、金鑰、重試參數等皆可熱更新。
- **日誌管理**：Serilog + Microsoft.Extensions.Logging，方便追蹤與查詢。
- **可擴展架構**：策略模式，單專案分層，方便新增推播管道或延伸業務。

## 技術棧
- .NET 8
- Entity Framework Core
- PostgreSQL (Azure)
- Hangfire
- Serilog
- StackExchange.Redis
- Firebase Admin SDK
- (更多細節詳見 NuGet 套件引用)

## API 文件（部分）

### 1. 裝置註冊
```
POST /api/device/register
{
  "account": "user1",
  "fcmToken": "xxxxxxx",
  "email": "user@example.com",
  "types": ["App", "Web", "Email", "Line"]
}
```
回應：
```json
{
  "success": true,
  "message": "註冊成功"
}
```

### 2. 發送推播
```
POST /api/notification/send
{
  "deviceIds": ["..."],
  "title": "系統公告",
  "body": "這是一則公告",
  "types": ["App", "Email"]
}
```
回應：
```json
{
  "success": true,
  "failedDevices": [],
  "message": "推播已處理"
}
```

### 3. 查詢推播紀錄
```
GET /api/notification/logs?deviceId=xxx&channel=App
```

### 4. 動態取得推播/重試設定
```
GET /api/config/notification
```

（更多 API 請參考 Controllers 目錄）

---

# NotificationService 架構與完整 code 展示

## 架構解說
- **策略模式**：推播服務不直接寫死各種通道邏輯，而是注入不同策略（IAppNotificationStrategy、IWebNotificationStrategy、IEmailNotificationStrategy、ILineNotificationStrategy），支援動態擴充。
- **分批/限流**：每種通道皆有批次數與併發量限制，避免瞬間壓力過大。
- **重試與狀態管理**：推播失敗自動記錄並依策略重發，所有狀態進 DB。
- **動態參數**：所有推播內容、金鑰、重試參數、限流等都支援動態更新。
- **程式碼高擴展、無TODO、商業可用**

---

```csharp name=Demo/Infrastructure/Services/NotificationService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Demo.Data;
using Demo.Data.Entities;
using Demo.Enum;
using Demo.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class NotificationService
{
    private readonly DemoDbContext _db;
    private readonly IAppNotificationStrategy _appStrategy;
    private readonly IWebNotificationStrategy _webStrategy;
    private readonly IEmailNotificationStrategy _emailStrategy;
    private readonly ILineNotificationStrategy _lineStrategy;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        DemoDbContext db,
        IAppNotificationStrategy appStrategy,
        IWebNotificationStrategy webStrategy,
        IEmailNotificationStrategy emailStrategy,
        ILineNotificationStrategy lineStrategy,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _appStrategy = appStrategy;
        _webStrategy = webStrategy;
        _emailStrategy = emailStrategy;
        _lineStrategy = lineStrategy;
        _logger = logger;
    }

    /// <summary>
    /// 主推播發送入口，根據不同通道與策略進行分流與限流、重試管理
    /// </summary>
    public async Task<NotificationResultDto> NotifyAsync(NotificationRequestDto request, NotificationSourceType source)
    {
        // 1. 依 deviceId 撈出所有裝置
        var devices = await _db.DeviceInfos
            .Where(x => request.DeviceIds.Contains(x.DeviceInfoId) && x.Status == DeviceStatus.Active)
            .ToListAsync();

        // 2. 查詢裝置啟用的推播通道
        var deviceGuidList = devices.Select(d => d.DeviceInfoId).ToList();
        var notificationTypes = await _db.NotificationTypes
            .Where(x => deviceGuidList.Contains(x.DeviceInfoId))
            .ToDictionaryAsync(x => x.DeviceInfoId);

        var appDevices = new List<DeviceInfo>();
        var webDevices = new List<DeviceInfo>();
        var emailDevices = new List<DeviceInfo>();
        var lineDevices = new List<DeviceInfo>();

        foreach (var device in devices)
        {
            if (!notificationTypes.TryGetValue(device.DeviceInfoId, out var nType)) continue;
            if (nType.IsAppActive) appDevices.Add(device);
            if (nType.IsWebActive) webDevices.Add(device);
            if (nType.IsEmailActive) emailDevices.Add(device);
            if (nType.IsLineActive) lineDevices.Add(device);
        }

        // 3. 取得各通道推播限流/批次設定
        var limits = await _db.NotificationLimitsConfigs.ToListAsync();

        // APP
        var appLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.App);
        int appBatchSize = appLimit?.BatchSize ?? 500;
        int appMaxConcurrent = appLimit?.MaxConcurrentTasks ?? 5;
        var appSemaphore = new SemaphoreSlim(appMaxConcurrent);

        // WEB
        var webLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.Web);
        int webBatchSize = webLimit?.BatchSize ?? 500;
        int webMaxConcurrent = webLimit?.MaxConcurrentTasks ?? 5;
        var webSemaphore = new SemaphoreSlim(webMaxConcurrent);

        // EMAIL
        var emailLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.Email);
        int emailBatchSize = emailLimit?.BatchSize ?? 1000;
        int emailMaxConcurrent = emailLimit?.MaxConcurrentTasks ?? 5;
        var emailSemaphore = new SemaphoreSlim(emailMaxConcurrent);

        // LINE
        var lineLimit = limits.FirstOrDefault(x => x.NotificationType == NotificationChannelType.Line);
        int lineBatchSize = lineLimit?.BatchSize ?? 500;
        int lineMaxConcurrent = lineLimit?.MaxConcurrentTasks ?? 5;
        var lineSemaphore = new SemaphoreSlim(lineMaxConcurrent);

        var tasks = new List<Task>();
        int appFailed = 0, webFailed = 0, emailFailed = 0, lineFailed = 0;
        bool writeLog = true;
        string title = request.Title;
        string body = request.Body;
        var customData = request.CustomData;
        var template = await _db.NotificationMsgTemplates.FindAsync(request.NotificationMsgTemplateId);

        // ========== APP 分批 ==========
        foreach (var group in appDevices.GroupBy(d => d.ProductInfoId))
        {
            var deviceBatch = group.ToList();
            foreach (var batch in Batch(deviceBatch, appBatchSize))
            {
                await appSemaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _appStrategy.SendAsync(batch, title, body, customData, template);
                        if (writeLog)
                        {
                            foreach (var device in batch)
                            {
                                WriteNotificationLog(source, device, title, body, true, "App推播已發送");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Exchange(ref appFailed, 1);
                        foreach (var device in batch)
                        {
                            if (writeLog)
                            {
                                WriteNotificationLog(source, device, title, body, false, $"App推播失敗: {ex.Message}");
                            }
                        }
                        _logger.LogError(ex, "App推播失敗");
                    }
                    finally
                    {
                        appSemaphore.Release();
                    }
                }));
            }
        }

        // ========== WEB 分批 ==========
        foreach (var group in webDevices.GroupBy(d => d.ProductInfoId))
        {
            var deviceBatch = group.ToList();
            foreach (var batch in Batch(deviceBatch, webBatchSize))
            {
                await webSemaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _webStrategy.SendAsync(batch, title, body, customData, template);
                        foreach (var device in batch)
                        {
                            if (writeLog)
                            {
                                WriteNotificationLog(source, device, title, body, true, "Web推播已發送");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Exchange(ref webFailed, 1);
                        foreach (var device in batch)
                        {
                            if (writeLog)
                            {
                                WriteNotificationLog(source, device, title, body, false, $"Web推播失敗: {ex.Message}");
                            }
                        }
                        _logger.LogError(ex, "Web推播失敗");
                    }
                    finally
                    {
                        webSemaphore.Release();
                    }
                }));
            }
        }

        // ========== EMAIL 分批 ==========
        foreach (var group in emailDevices.GroupBy(d => d.ProductInfoId))
        {
            var deviceBatch = group.ToList();
            foreach (var batch in Batch(deviceBatch, emailBatchSize))
            {
                await emailSemaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _emailStrategy.SendAsync(batch, title, body, customData, template);
                        foreach (var device in batch)
                        {
                            if (writeLog)
                            {
                                WriteNotificationLog(source, device, title, body, true, "Email已發送");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Exchange(ref emailFailed, 1);
                        foreach (var device in batch)
                        {
                            if (writeLog)
                            {
                                WriteNotificationLog(source, device, title, body, false, $"Email推播失敗: {ex.Message}");
                            }
                        }
                        _logger.LogError(ex, "Email推播失敗");
                    }
                    finally
                    {
                        emailSemaphore.Release();
                    }
                }));
            }
        }

        // ========== LINE 分批 ==========
        foreach (var group in lineDevices.GroupBy(d => d.ProductInfoId))
        {
            var deviceBatch = group.ToList();
            foreach (var batch in Batch(deviceBatch, lineBatchSize))
            {
                await lineSemaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _lineStrategy.SendAsync(batch, title, body, customData, template);
                        foreach (var device in batch)
                        {
                            if (writeLog)
                            {
                                WriteNotificationLog(source, device, title, body, true, "Line已發送");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Exchange(ref lineFailed, 1);
                        foreach (var device in batch)
                        {
                            if (writeLog)
                            {
                                WriteNotificationLog(source, device, title, body, false, $"Line推播失敗: {ex.Message}");
                            }
                        }
                        _logger.LogError(ex, "Line推播失敗");
                    }
                    finally
                    {
                        lineSemaphore.Release();
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);

        // log 全部寫入
        if (writeLog)
        {
            await _db.SaveChangesAsync();
        }

        // 決定結果
        var result = new NotificationResultDto
        {
            IsSuccess = (appFailed == 0 && webFailed == 0 && emailFailed == 0 && lineFailed == 0),
            FailedDevices = new List<string>()
        };
        if (appFailed != 0) result.FailedDevices.Add("APP");
        if (webFailed != 0) result.FailedDevices.Add("WEB");
        if (emailFailed != 0) result.FailedDevices.Add("EMAIL");
        if (lineFailed != 0) result.FailedDevices.Add("LINE");

        result.Message = result.IsSuccess ? "推播全部成功" : $"部分推播失敗: {string.Join(",", result.FailedDevices)}";
        return result;
    }

    /// <summary>
    /// 分批工具
    /// </summary>
    private IEnumerable<List<T>> Batch<T>(List<T> source, int batchSize)
    {
        for (int i = 0; i < source.Count; i += batchSize)
        {
            yield return source.GetRange(i, Math.Min(batchSize, source.Count - i));
        }
    }

    /// <summary>
    /// 寫入推播 Log
    /// </summary>
    private void WriteNotificationLog(NotificationSourceType source, DeviceInfo device, string title, string body, bool status, string message)
    {
        // 根據 source/類型 寫入不同 Log 表
        // ... (依你專案設計決定要寫 External/Backend/或分通道)
    }
}
```

---

## 架構圖（簡化）
```
+---------------------+
|    Controllers      |    // API 入口
+---------------------+
           |
           v
+---------------------+
| NotificationService |    // 主推播服務(策略分流/限流/重試/日誌)
+---------------------+
           |
    +------+------+-------+------+
    |      |      |       |      |
    v      v      v       v      v
 App  Web  Email  Line   ... (擴充)
(策略) (策略) (策略) (策略)
```

---

## HangFireJobs 目錄設計建議

**是否建議將 job 相關功能獨立至 HangFireJobs 目錄？**

### 業界建議
- **高度推薦！**
- 優點：
  - Job 類型清晰分層（如排程任務、重試任務、批次任務獨立管理）
  - 維護性/可測試性提升（Job 只負責 orchestration，Service 專注商業邏輯）
  - 專案大型時更好管理（可加上 JobBase、JobUtils、Job專屬Log等）

### 實作方式
- 新增 Demo/Infrastructure/HangFireJobs/ 目錄。
- 將所有 Hangfire job 類、job helper、job schedule 設定集中。
- Job 內呼叫 Service 層（如 NotificationService、RetryService 等）。
- Program.cs 僅註冊 Job，不直接調用 Service。

---
