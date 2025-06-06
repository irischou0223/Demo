# Demo
通知訊息服務Demo


API開發，提供開發通知訊息的做法，使用.NET 8開發，搭配EFCore，DB使用PostgreSQL(Azure)不需要用到Migration，ID欄位都用GUID型態，開發上可擴展性要高，且使用策略模式進行開發。

註冊流程: 使用者安裝並啟動 App(iOS/Android/Web) -> 呼叫 註冊API -> 將使用者帳號、FCM Token、Email及推播方式(多選)傳遞給API -> API則判斷裝置存在與否，不存在則
新增一筆新的裝置(Status為啟用)，存在則更新既有裝置的Status(為不啟用)，再新增一筆新的裝置(Status為啟用)。

API Server啟動
1. 先讀取資料庫裡面的設定(Firebase的金鑰內容、推播通知的標題與內容、Retry機制的參數、APP及WEB推播的設定)，將資料放到memory或是redis，設定檔可動態更新

推播服務
1. 有四種:Line、Web、Email、APP(iOS、Android)，APP及Web透過 Firebase Admin SDK發送
2. 推播行為: 發送到指定裝置、群發(多個裝置)或是全部裝置
3. 推播狀態追蹤與記錄管理儲存到資料庫，以利後續追蹤分析
4. Firebase Credential Json檔整份內容存在於資料庫
5. 使用queue做任務等待

排程發布訊息
1. 使用Hangfire框架
2. 定時選項: 立即、每日、每月
3. 排程設定存於資料庫，可動態選擇推播方式、時間
4. 可於後臺管理設定(未規劃)

Retry機制
1. 發送失敗時，會記錄哪些 token 失敗，並執行重發機制
2. 參數設定存在於資料庫
3. 預設為失敗後延遲 5 分鐘再試一次；重試上限 3 次
4. 參數可於後臺管理設定(未規劃)

限流處理 Hangfire
1. 避免瞬間壓力過大，可以加上節流與併發控制

Log紀錄
1. 使用預設Microsoft.Extensions.Logging 的內建日誌系統
2. 日誌記錄則使用 Serilog，以利後續追蹤分析

測試工具
1. Postman
