using EmlakOfisi.EntityLayer.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EmlakOfisi.DataAccessLayer.Context
{
    // InstrumentTrade'deki AppDbContext ile aynı yapı. Fark: connection string kodda değil,
    // Program.cs'te user-secrets'tan okunup buraya options olarak verilir (OnConfiguring yok).
    public class AppDbContext : IdentityDbContext<AppUser, AppRole, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Kendi tablolarımızdaki tüm ilişkilerde cascade silme kapalı.
            // Kayıtlar IsDeleted ile silindiği için cascade'e gerek yok; ayrıca SQL Server'ın
            // "multiple cascade paths" hatası da oluşmaz. Identity'nin kendi tabloları
            // (AspNetUserRoles vb.) bu klasörde olmadığı için varsayılan ayarında kalır.
            var ourNamespace = typeof(Contact).Namespace;

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.ClrType.Namespace != ourNamespace)
                    continue;

                foreach (var foreignKey in entityType.GetForeignKeys())
                {
                    foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
                }
            }

            // Türkçe arama: ad/soyad karşılaştırması Türkçe kurallarla ve büyük/küçük harfe duyarsız
            // ("IŞIK" araması "Işık" ve "ışık"ı da bulsun). InMemory test veritabanı bunu yok sayar.
            modelBuilder.Entity<Contact>().Property(c => c.Name).UseCollation(TurkishCollation);
            modelBuilder.Entity<Contact>().Property(c => c.Surname).UseCollation(TurkishCollation);

            // Portföy aramasında da aynı (başlık, şehir, ilçe, mahalle)
            modelBuilder.Entity<Property>().Property(p => p.Title).UseCollation(TurkishCollation);
            modelBuilder.Entity<Property>().Property(p => p.City).UseCollation(TurkishCollation);
            modelBuilder.Entity<Property>().Property(p => p.District).UseCollation(TurkishCollation);
            modelBuilder.Entity<Property>().Property(p => p.Neighborhood).UseCollation(TurkishCollation);
        }

        public const string TurkishCollation = "Turkish_CI_AS";

        public DbSet<Contact> Contacts { get; set; }
        public DbSet<Property> Properties { get; set; }
        public DbSet<PropertyPhoto> PropertyPhotos { get; set; }
        public DbSet<CustomerRequest> CustomerRequests { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
    }
}
