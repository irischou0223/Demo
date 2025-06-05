using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("user_info")]
    [Comment("使用者資訊")]
    public class UserInfo
    {
        [Key]
        [Required, StringLength(255)]
        [Column("user_account", TypeName = "varchar")]
        [Comment("使用者帳號")]
        public string UserAccount { get; set; } = null!;

        [Key]
        [Required]
        [Column("device_info_id", TypeName = "uuid")]
        [Comment("裝置資訊UID")]
        public Guid DeviceInfoId { get; set; }

        [Key]
        [Required]
        [Column("product_info_id", TypeName = "uuid")]
        [Comment("產品資訊UID")]
        public Guid ProductInfoId { get; set; }

        [Required, StringLength(100)]
        [Column("device_id", TypeName = "varchar")]
        [Comment("個別裝置ID")]
        public string DeviceId { get; set; } = null!;
    }
}
