using Demo.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Demo.Extensions
{
    /// <summary>
    /// 設定所有實體中 GUID 型別主鍵的預設行為，使其在新增資料時由資料庫產生 UUID。
    /// 此擴充方法會為每個符合條件的實體，將其主鍵屬性設定為使用 'gen_random_uuid()'
    /// 作為資料庫的預設值，並標記為 ValueGeneratedOnAdd，表示該值由資料庫在新增時自動生成。
    /// </summary>
    /// <param name="modelBuilder">EF Core 的 ModelBuilder 實例，用於設定資料庫模型。</param>
    public static class ModelBuilderExtensions
    {
        public static void ConfigureUuidDefaults(this ModelBuilder modelBuilder)
        {
            // ProductInfo
            modelBuilder.Entity<ProductInfo>()
                .Property(p => p.ProductInfoId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            // NotificationType
            modelBuilder.Entity<NotificationType>()
                .Property(d => d.NotificationTypeId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            // DeviceInfo
            modelBuilder.Entity<DeviceInfo>()
                .Property(d => d.DeviceInfoId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            // ExternalNotificationLog
            modelBuilder.Entity<ExternalNotificationLog>()
                .Property(d => d.ExternalNotificationLogId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            // BackendNotificationLog
            modelBuilder.Entity<BackendNotificationLog>()
                .Property(d => d.BackendNotificationLogId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            // JobNotificationLog
            modelBuilder.Entity<JobNotificationLog>()
                .Property(d => d.JobNotificationLogId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            // CodeInfo
            modelBuilder.Entity<CodeInfo>()
                .Property(d => d.CodeInfoId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            // NotificationMsgTemplate
            modelBuilder.Entity<NotificationMsgTemplate>()
                .Property(d => d.NotificationMsgTemplateId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            // NotificationMsgTemplateData
            modelBuilder.Entity<NotificationMsgTemplateData>()
                .Property(d => d.NotificationMsgTemplateDataId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            // NotificationActionConfig
            modelBuilder.Entity<NotificationActionConfig>()
                .Property(d => d.NotificationActionConfigId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            // NotificationScheduledJob
            modelBuilder.Entity<NotificationScheduledJob>()
                .Property(d => d.NotificationScheduledJobId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();

            //NotificationLimitsConfig
            modelBuilder.Entity<NotificationLimitsConfig>()
                .Property(d => d.NotificationLimitsConfigId)
                .HasDefaultValueSql("gen_random_uuid()")
                .ValueGeneratedOnAdd();
        }
    }
}
