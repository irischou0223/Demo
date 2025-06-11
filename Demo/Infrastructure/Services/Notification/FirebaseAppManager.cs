using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using System.Collections.Concurrent;

namespace Demo.Infrastructure.Services.Notification
{
    /// <summary>
    /// 動態管理多個 FirebaseApp 實例，支援同時多產品推播（每個產品/專案對應一個 FirebaseApp）。
    /// 流程說明：
    /// 1. 以 ProductInfoId 為 key 快取/管理 FirebaseApp 實例（確保同一產品只建立一個 App 實例）。
    /// 2. 取得 App 實例時，若不存在則用傳入的 serviceAccountJson 建立新的 FirebaseApp，並以 ProductInfoId.ToString() 作為唯一名稱（避免重複）。
    /// 3. 後續 APP/Web 推播服務直接共用此單例管理器取出對應的 FirebaseApp 實例進行推播。
    /// 
    /// 使用情境：
    /// - AppNotificationStrategy, WebNotificationStrategy 皆可安全並發呼叫本方法，確保跨專案/產品推播安全高效。
    /// </summary>
    public static class FirebaseAppManager
    {
        /// <summary>
        /// 產品對應 FirebaseApp 快取，避免重複建立（thread-safe）
        /// </summary>
        private static readonly ConcurrentDictionary<Guid, FirebaseApp> _apps = new();

        /// <summary>
        /// 取得或建立指定產品的 FirebaseApp 實例
        /// </summary>
        /// <param name="productInfoId">產品唯一識別（用於分辨不同 Firebase 專案）</param>
        /// <param name="serviceAccountJson">該產品對應的 Firebase Service Account JSON</param>
        /// <returns>對應產品的 FirebaseApp 實例</returns>
        public static FirebaseApp GetOrCreateApp(Guid productInfoId, string serviceAccountJson)
        {
            // 若已存在則直接回傳，否則建立新 FirebaseApp
            return _apps.GetOrAdd(productInfoId, id =>
            {
                var options = new AppOptions
                {
                    Credential = GoogleCredential.FromJson(serviceAccountJson)
                };
                // 使用產品 id 作為 appName，確保全域唯一
                return FirebaseApp.Create(options, id.ToString());
            });
        }
    }
}
