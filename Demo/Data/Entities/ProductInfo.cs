using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Data.Entities
{
    [Table("product_info")]
    [Comment("產品資訊")]
    public class ProductInfo
    {
        [Key]
        [Required]
        [Column("product_info_id", TypeName = "uuid")]
        [Comment("產品資訊UID")]
        public Guid ProductInfoId { get; set; }

        [Required, StringLength(50)]
        [Column("product_name", TypeName = "varchar")]
        [Comment("產品名稱")]
        public string ProductName { get; set; } = null!;

        [Required, StringLength(50)]
        [Column("product_code", TypeName = "varchar")]
        [Comment("產品代號")]
        public string ProductCode { get; set; } = null!;

        [Required, StringLength(100)]
        [Column("firebase_project_id", TypeName = "varchar")]
        [Comment("Firebase專案ID")]
        public string FirebaseProjectId { get; set; } = null!;
    }
}
