using System.ComponentModel.DataAnnotations;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Portföy fotoğrafı. Dosya wwwroot dışında (App_Data/uploads) saklanır, görünürlük kontrolüyle verilir.
    public class PropertyPhoto
    {
        public int PropertyPhotoId { get; set; }

        public int PropertyId { get; set; }
        public Property? Property { get; set; }

        // Sunucuda üretilen dosya adı (kullanıcının verdiği ad kullanılmaz)
        [Required]
        [StringLength(260)]
        public string FileName { get; set; } = string.Empty;

        [StringLength(260)]
        public string? OriginalName { get; set; }

        [Required]
        [StringLength(50)]
        public string ContentType { get; set; } = string.Empty;

        public int SortOrder { get; set; }
        public bool IsCover { get; set; }

        public int UploadedByUserId { get; set; }
        public AppUser? UploadedByUser { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}
