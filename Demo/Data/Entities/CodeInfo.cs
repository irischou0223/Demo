using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("code_info")]
    [Comment("代碼資訊")]
    public class CodeInfo
    {
        [Key]
        [Required]
        [Column("code_info_id", TypeName = "uuid")]
        [Comment("代碼資訊UID")]
        public Guid CodeInfoId { get; set; }

        [Required, StringLength(50)]
        [Column("code", TypeName = "varchar")]
        [Comment("代碼")]
        public string Code { get; set; } = null!;

        [Required, StringLength(10)]
        [Column("lang", TypeName = "varchar")]
        [Comment("語系")]
        public string Lang { get; set; } = null!;

        [StringLength(255)]
        [Column("value", TypeName = "varchar")]
        [Comment("值")]
        public string? Value { get; set; } = null;

        [StringLength(50)]
        [Column("title", TypeName = "varchar")]
        [Comment("通知標題")]
        public string? Title { get; set; } = null;

        [StringLength(255)]
        [Column("body", TypeName = "varchar")]
        [Comment("通知內容")]
        public string? Body { get; set; } = null;

        [StringLength(255)]
        [Column("desc", TypeName = "varchar")]
        [Comment("說明")]
        public string? Desc { get; set; } = null;

        [Required]
        [Column("create_at_utc", TypeName = "timestamp with time zone")]
        [Comment("建立時間")]
        public DateTime CreateAtUtc { get; set; }

        [Column("update_at_utc", TypeName = "timestamp with time zone")]
        [Comment("更新時間")]
        public DateTime? UpdateAtUtc { get; set; }
    }
}
