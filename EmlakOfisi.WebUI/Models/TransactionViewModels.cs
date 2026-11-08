using EmlakOfisi.BusinessLayer.Abstract;
using System.ComponentModel.DataAnnotations;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Enums;

namespace EmlakOfisi.WebUI.Models
{
    public static class TransactionLabels
    {
        public static readonly TransactionStatus[] All =
        {
            TransactionStatus.Offer, TransactionStatus.Negotiation, TransactionStatus.Agreed,
            TransactionStatus.Completed, TransactionStatus.Cancelled
        };

        public static string Status(TransactionStatus s) => s switch
        {
            TransactionStatus.Offer => "Teklif",
            TransactionStatus.Negotiation => "Pazarlık",
            TransactionStatus.Agreed => "Anlaşıldı",
            TransactionStatus.Completed => "Tamamlandı",
            TransactionStatus.Cancelled => "İptal",
            _ => s.ToString()
        };

        public static string StatusBadge(TransactionStatus s) => s switch
        {
            TransactionStatus.Completed => "bg-green-lt",
            TransactionStatus.Cancelled => "bg-secondary-lt",
            TransactionStatus.Agreed => "bg-primary-lt",
            _ => "bg-orange-lt"
        };

        // Biten kira rozeti (görev 23): tamamlanmış kiralamada bitişe 60 gün ya da daha az kaldıysa / bittiyse
        public static (string Text, string Css)? LeaseBadge(ListingType type, TransactionStatus status, DateTime? end)
        {
            if (type != ListingType.Rent || status != TransactionStatus.Completed || end == null)
                return null;
            var days = (end.Value.Date - DateTime.Today).Days;
            if (days < -ITransactionService.LeaseEndedDays)
                return null;   // çoktan bitmiş: "Tamamlanan" listesinde rozet kalabalığı olmasın
            if (days < 0)
                return ($"Kira bitti ({-days} gün önce)", "bg-red-lt");
            if (days == 0)
                return ("Kira bugün bitiyor", "bg-red-lt");
            if (days <= ITransactionService.LeaseEndingDays)
                return ($"Kiranın bitmesine {days} gün", "bg-orange-lt");
            return null;
        }

        public static string Money(decimal? v) => v == null ? "–" : v.Value.ToString("N0", PropertyLabels.Tr) + " ₺";
    }

    // İşlem kartı. Entity view'a verilmez; görünmeyen portföy/müşterinin adı ve id'si taşınmaz.
    public class TransactionItemViewModel
    {
        public int TransactionId { get; set; }
        public ListingType Type { get; set; }
        public TransactionStatus Status { get; set; }
        public decimal? OfferPrice { get; set; }
        public decimal? AgreedPrice { get; set; }
        public decimal? CommissionRate { get; set; }
        public decimal? CommissionAmount { get; set; }
        public bool ShowShare { get; set; }
        public decimal? AgentShareRate { get; set; }
        public decimal? AgentShareAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public bool IsMine { get; set; }
        public string? Notes { get; set; }

        public int? PropertyId { get; set; }
        public string? PropertyTitle { get; set; }
        public bool PropertyHidden { get; set; }

        public int? ContactId { get; set; }
        public string? ContactName { get; set; }
        public bool ContactHidden { get; set; }
        public string TelLink { get; set; } = string.Empty;

        public bool CanEdit { get; set; }

        public DateTime? LeaseStartDate { get; set; }
        public DateTime? LeaseEndDate { get; set; }
        public bool HasContract { get; set; }   // dosya adı/yolu view'a gitmez; link işlem id'siyle kurulur

        // Prim (tutar ve danışmanın kişisel oranı) sadece yöneticiye ve işlemin sorumlu danışmanına gösterilir;
        // işlemi açıp sorumluyu başkasına devreden kişi o danışmanın primini görmez.
        public static TransactionItemViewModel From(TransactionRow row, int userId, bool canEdit, bool canSeeAll)
        {
            var t = row.Transaction;
            var showShare = canSeeAll || t.AgentUserId == userId;
            var c = row.Contact;
            var phoneOk = c != null && c.PhoneNormalized.Length == 10 && c.PhoneNormalized.All(char.IsAsciiDigit);
            return new TransactionItemViewModel
            {
                TransactionId = t.TransactionId,
                Type = t.Type,
                Status = t.Status,
                OfferPrice = t.OfferPrice,
                AgreedPrice = t.AgreedPrice,
                CommissionRate = t.CommissionRate,
                CommissionAmount = t.CommissionAmount,
                ShowShare = showShare,
                AgentShareRate = showShare ? t.AgentShareRate : null,
                AgentShareAmount = showShare ? t.AgentShareAmount : null,
                CreatedAt = t.CreatedAt,
                CompletedAt = t.CompletedAt,
                AgentName = row.AgentName,
                IsMine = t.AgentUserId == userId,
                Notes = t.Notes,
                PropertyId = row.Property?.PropertyId,
                PropertyTitle = row.Property?.Title,
                PropertyHidden = row.PropertyHidden,
                ContactId = c?.ContactId,
                ContactName = c == null ? null : $"{c.Name} {c.Surname}",
                ContactHidden = row.ContactHidden,
                TelLink = phoneOk ? $"tel:+90{c!.PhoneNormalized}" : string.Empty,
                CanEdit = canEdit,
                LeaseStartDate = t.LeaseStartDate,
                LeaseEndDate = t.LeaseEndDate,
                HasContract = t.ContractFileName != null
            };
        }
    }

    public class TransactionListViewModel
    {
        public List<TransactionItemViewModel> Items { get; set; } = new();
        public TransactionScope Scope { get; set; } = TransactionScope.Open;
        public bool Mine { get; set; }
        public bool CanSeeAll { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    // Portföy ve müşteri detayındaki işlem listesi
    public class TransactionPanelViewModel
    {
        public int? PropertyId { get; set; }          // portföy detayında "İşlem başlat" için
        public bool ShowProperty { get; set; }        // müşteri detayında portföy adı gösterilir
        public List<TransactionItemViewModel> Items { get; set; } = new();
    }

    // Ekleme / düzenleme formu. Id, portföy (adresten), oluşturan, tarih ve tür YOK (over-posting kapalı).
    // Fiyat ve komisyon tutarı sadece rakam (tam TL); oran "2" ya da "2,5" biçiminde.
    public class TransactionFormViewModel
    {
        [Display(Name = "Müşteri")]
        public int CustomerContactId { get; set; }

        [Display(Name = "Durum")]
        public TransactionStatus Status { get; set; } = TransactionStatus.Offer;

        [Display(Name = "Teklif fiyatı")]
        public string? OfferPriceText { get; set; }

        [Display(Name = "Anlaşılan fiyat")]
        public string? AgreedPriceText { get; set; }

        [Display(Name = "Komisyon oranı (%)")]
        public string? CommissionRateText { get; set; }

        [Display(Name = "Komisyon tutarı")]
        public string? CommissionAmountText { get; set; }

        // Sadece yönetici yazar; boşsa danışmanın (Admin'in tanımladığı) oranı kullanılır
        [Display(Name = "Danışman prim oranı (%)")]
        public string? AgentShareRateText { get; set; }

        [Display(Name = "Sorumlu danışman")]
        public int AgentUserId { get; set; }

        [StringLength(2000, ErrorMessage = "Not en fazla 2000 karakter olabilir.")]
        [Display(Name = "Not")]
        public string? Notes { get; set; }

        // Sadece kiralamada gösterilir ve kaydedilir
        [Display(Name = "Kira başlangıcı")]
        public DateTime? LeaseStartDate { get; set; }

        [Display(Name = "Kira bitişi")]
        public DateTime? LeaseEndDate { get; set; }
    }

    public class TransactionFormOptions
    {
        public int? TransactionId { get; set; }
        public int PropertyId { get; set; }
        public string PropertyTitle { get; set; } = string.Empty;
        public ListingType Type { get; set; }
        public List<(int Id, string Name)> Customers { get; set; } = new();   // kullanıcının görebildiği kişiler
        public bool CustomerHidden { get; set; }                             // mevcut müşteri görünmüyor: değişmez
        public bool CanChooseAgent { get; set; }
        public List<(int Id, string Name)> Agents { get; set; } = new();

        // Düzenleme ekranındaki kontrat bölümü (dosya yoksa null)
        public string? ContractOriginalName { get; set; }
        public DateTime? ContractUploadedAt { get; set; }
        public bool HasContract => ContractOriginalName != null;
    }
}
