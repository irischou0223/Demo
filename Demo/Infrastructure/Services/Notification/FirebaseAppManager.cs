using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using System.Collections.Concurrent;

namespace Demo.Infrastructure.Services.Notification
{
    // 用於動態管理多個 FirebaseApp(每個產品一個)
    public static class FirebaseAppManager
    {
        private static readonly ConcurrentDictionary<Guid, FirebaseApp> _apps = new();

        public static FirebaseApp GetOrCreateApp(Guid productInfoId, string serviceAccountJson)
        {
            return _apps.GetOrAdd(productInfoId, id =>
            {
                var options = new AppOptions
                {
                    Credential = GoogleCredential.FromJson(serviceAccountJson)
                };
                // app name 唯一性
                return FirebaseApp.Create(options, id.ToString());
            });
        }
    }
}
