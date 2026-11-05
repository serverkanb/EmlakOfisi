using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace EmlakOfisi.EntityLayer.Entities
{
    // Ofis çalışanı (Admin, Manager, Agent). InstrumentTrade'deki AppUser ile aynı yapı.
    public class AppUser : IdentityUser<int>
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Surname { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
