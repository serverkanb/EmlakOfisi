using EmlakOfisi.BusinessLayer.Abstract;
using EmlakOfisi.BusinessLayer.Helpers;
using EmlakOfisi.DataAccessLayer.Abstract;
using EmlakOfisi.EntityLayer.Constants;
using EmlakOfisi.EntityLayer.Entities;
using EmlakOfisi.EntityLayer.Enums;
using EmlakOfisi.WebUI.Extensions;
using EmlakOfisi.WebUI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // Portföyler. Sadece ofis çalışanları. Görünürlük ve yetki serviste (PropertyManager).
    // Görmeye yetkisi olmayan kayıt -> 404; görüp düzenleyemediği -> 403 (form doğrulamasından ÖNCE).
    // Entity view'a verilmez; ViewModel'e sadece gereken alanlar aktarılır.
    [Authorize(Roles = RoleNames.Staff)]
    public class PropertyController : Controller
    {
        public const int PageSize = 20;

        private readonly IPropertyService _propertyService;
        private readonly IContactService _contactService;
        private readonly IPropertyInterestService _interestService;
        private readonly IPropertyPhotoService _photoService;
        private readonly ITransactionService _transactionService;

        public PropertyController(IPropertyService propertyService, IContactService contactService,
            IPropertyInterestService interestService, IPropertyPhotoService photoService,
            ITransactionService transactionService)
        {
            _transactionService = transactionService;
            _propertyService = propertyService;
            _contactService = contactService;
            _interestService = interestService;
            _photoService = photoService;
        }

        // ---------------- Liste ----------------

        [HttpGet]
        public IActionResult Index(string? q, ListingType? listingType, PropertyType? propertyType, PropertyStatus? status,
            decimal? minPrice, decimal? maxPrice, bool mine = false, int page = 1, int? minArea = null)
        {
            var userId = User.GetUserId();
            var filter = new PropertyFilter
            {
                Search = q, ListingType = listingType, PropertyType = propertyType, Status = status,
                MinPrice = minPrice, MaxPrice = maxPrice, MinArea = minArea > 0 ? minArea : null, OnlyMine = mine
            };

            var (items, total) = _propertyService.TGetVisibleList(userId, User.CanSeeAll(), filter, page, PageSize);

            var lastPage = (int)Math.Ceiling(total / (double)PageSize);
            if (items.Count == 0 && page > 1 && lastPage >= 1)
                return RedirectToAction(nameof(Index), new { q, listingType, propertyType, status, minPrice, maxPrice, mine, minArea, page = lastPage });

            return View(new PropertyListViewModel
            {
                Items = items.Select(p => ToListItem(p, userId)).ToList(),
                Q = q, ListingType = listingType, PropertyType = propertyType, Status = status,
                MinPrice = minPrice, MaxPrice = maxPrice, MinArea = minArea > 0 ? minArea : null, Mine = mine,
                Page = Math.Max(page, 1), PageSize = PageSize, TotalCount = total
            });
        }

        // ---------------- Detay ----------------

        [HttpGet]
        public IActionResult Detail(int id)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            var p = _propertyService.TGetVisibleById(id, userId, canSeeAll);
            if (p == null)
                return NotFound();

            // Portföyü görmek sahibini görmek demek değil: sahip ayrıca müşteri görünürlüğünden geçer
            var owner = _propertyService.TGetVisibleOwner(p, userId, canSeeAll);
            var ownerPhoneOk = owner != null && owner.PhoneNormalized.Length == 10;

            return View(new PropertyDetailViewModel
            {
                Summary = ToListItem(p, userId),
                Neighborhood = p.Neighborhood,
                Address = p.Address,
                GrossArea = p.GrossArea,
                Floor = p.Floor,
                BuildingAge = p.BuildingAge,
                Description = p.Description,
                ListingUrl = p.ListingUrl,
                MapLink = PropertyLabels.MapLink(p.Latitude, p.Longitude, p.Address, p.Neighborhood, p.District, p.City),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                HasOwner = p.OwnerContactId != null,
                OwnerHidden = p.OwnerContactId != null && owner == null,
                OwnerContactId = owner?.ContactId,
                OwnerName = owner == null ? null : $"{owner.Name} {owner.Surname}",
                OwnerPhone = owner?.Phone,
                OwnerTelLink = ownerPhoneOk ? $"tel:+90{owner!.PhoneNormalized}" : null,
                CanEdit = _propertyService.TCanEdit(p, userId, canSeeAll),
                CanDelete = _propertyService.TCanDelete(p, userId, canSeeAll),
                Interests = BuildInterestPanel(p, userId, canSeeAll),
                Photos = (_photoService.TGetForProperty(p.PropertyId, userId, canSeeAll) ?? new List<PropertyPhoto>())
                    .Select(ph => new PropertyPhotoItemViewModel { PropertyPhotoId = ph.PropertyPhotoId, IsCover = ph.IsCover })
                    .ToList(),
                Transactions = BuildTransactionPanel(new TransactionFilter { PropertyId = p.PropertyId }, userId, canSeeAll, p.PropertyId)
            });
        }

        // ---------------- Ekleme ----------------

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Options = BuildOptions(propertyId: null, ownerHidden: false, current: null);
            return View(new PropertyFormViewModel { AgentUserId = User.GetUserId() });
        }

        [HttpPost]
        public IActionResult Create(PropertyFormViewModel model)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            var property = ValidateAndMap(model);
            if (property == null)
            {
                ViewBag.Options = BuildOptions(null, false, null);
                return View(model);
            }

            var result = _propertyService.TCreate(property, userId, canSeeAll);
            if (result == OperationResult.Invalid)
            {
                ModelState.AddModelError("", "Seçilen mülk sahibi veya sorumlu danışman geçersiz.");
                ViewBag.Options = BuildOptions(null, false, null);
                return View(model);
            }

            TempData["Success"] = "Portföy eklendi.";
            return RedirectToAction(nameof(Detail), new { id = property.PropertyId });
        }

        // ---------------- Düzenleme ----------------

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            var p = _propertyService.TGetVisibleById(id, userId, canSeeAll);
            if (p == null)
                return NotFound();
            if (!_propertyService.TCanEdit(p, userId, canSeeAll))
                return Forbid();

            var ownerHidden = p.OwnerContactId != null && _propertyService.TGetVisibleOwner(p, userId, canSeeAll) == null;
            ViewBag.Options = BuildOptions(id, ownerHidden, p);

            return View(new PropertyFormViewModel
            {
                Title = p.Title,
                ListingType = p.ListingType,
                PropertyType = p.PropertyType,
                Status = p.Status,
                PriceText = p.Price.ToString("0", PropertyLabels.Tr),
                City = p.City,
                District = p.District,
                Neighborhood = p.Neighborhood,
                Address = p.Address,
                LocationText = PropertyLabels.LocationText(p.Latitude, p.Longitude),
                GrossArea = p.GrossArea,
                NetArea = p.NetArea,
                RoomCount = p.RoomCount,
                Floor = p.Floor,
                BuildingAge = p.BuildingAge,
                Description = p.Description,
                ListingUrl = p.ListingUrl,
                OwnerContactId = ownerHidden ? null : p.OwnerContactId, // gizli sahibin id'si forma konmaz
                AgentUserId = p.AgentUserId,
                Visibility = p.Visibility
            });
        }

        [HttpPost]
        public IActionResult Edit(int id, PropertyFormViewModel model)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();

            // Önce yetki (form hatalarından önce): göremeyen 404, düzenleyemeyen 403
            var existing = _propertyService.TGetVisibleById(id, userId, canSeeAll);
            if (existing == null)
                return NotFound();
            if (!_propertyService.TCanEdit(existing, userId, canSeeAll))
                return Forbid();

            var ownerHidden = existing.OwnerContactId != null &&
                              _propertyService.TGetVisibleOwner(existing, userId, canSeeAll) == null;

            var changes = ValidateAndMap(model);
            if (changes == null)
            {
                ViewBag.Options = BuildOptions(id, ownerHidden, existing);
                return View(model);
            }

            // Servis yetkiyi tekrar kontrol eder; gizli sahip olduğu gibi korunur
            var result = _propertyService.TUpdate(id, changes, userId, canSeeAll);
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();
            if (result == OperationResult.Invalid)
            {
                ModelState.AddModelError("", "Seçilen mülk sahibi veya sorumlu danışman geçersiz.");
                ViewBag.Options = BuildOptions(id, ownerHidden, existing);
                return View(model);
            }

            TempData["Success"] = "Portföy güncellendi.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // ---------------- Silme ----------------

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var result = _propertyService.TSoftDelete(id, User.GetUserId(), User.CanSeeAll());
            if (result == OperationResult.NotFound)
                return NotFound();
            if (result == OperationResult.Forbidden)
                return Forbid();

            TempData["Success"] = "Portföy silindi.";
            return RedirectToAction(nameof(Index));
        }

        // ---------------- Yardımcılar ----------------

        // Form değerlerini doğrular ve entity'ye çevirir; hata varsa ModelState'e yazıp null döner
        private Property? ValidateAndMap(PropertyFormViewModel m)
        {
            if (!PropertyLabels.TryParsePrice(m.PriceText, out var price) && !string.IsNullOrWhiteSpace(m.PriceText))
                ModelState.AddModelError(nameof(m.PriceText), "Fiyata sadece rakam girin (ör. 5000000).");

            if (!PropertyLabels.TryParseLocation(m.LocationText, out var lat, out var lng))
                ModelState.AddModelError(nameof(m.LocationText), "Konumu \"40.98765, 29.03123\" biçiminde girin (Google Haritalar'dan kopyalayabilirsiniz).");

            var listingUrl = Clean(m.ListingUrl);
            if (listingUrl != null && !UrlValidator.IsSafeHttpUrl(listingUrl))
                ModelState.AddModelError(nameof(m.ListingUrl), "İlan linki http:// ya da https:// ile başlayan tam bir adres olmalıdır.");

            if (!Enum.IsDefined(m.Visibility)) ModelState.AddModelError(nameof(m.Visibility), "Geçersiz görünürlük seçimi.");
            if (!Enum.IsDefined(m.ListingType)) ModelState.AddModelError(nameof(m.ListingType), "Geçersiz ilan tipi.");
            if (!Enum.IsDefined(m.PropertyType)) ModelState.AddModelError(nameof(m.PropertyType), "Geçersiz gayrimenkul türü.");
            if (!Enum.IsDefined(m.Status)) ModelState.AddModelError(nameof(m.Status), "Geçersiz durum.");

            if (!ModelState.IsValid)
                return null;

            return new Property
            {
                Title = m.Title.Trim(),
                ListingType = m.ListingType,
                PropertyType = m.PropertyType,
                Status = m.Status,
                Price = price,
                City = m.City.Trim(),
                District = m.District.Trim(),
                Neighborhood = Clean(m.Neighborhood),
                Address = Clean(m.Address),
                Latitude = lat,
                Longitude = lng,
                GrossArea = m.GrossArea,
                NetArea = m.NetArea,
                RoomCount = Clean(m.RoomCount),
                Floor = m.Floor,
                BuildingAge = m.BuildingAge,
                Description = Clean(m.Description),
                ListingUrl = listingUrl,
                OwnerContactId = m.OwnerContactId,
                AgentUserId = m.AgentUserId,
                Visibility = m.Visibility
            };
        }

        // Mülk sahibi seçenekleri: kullanıcının GÖREBİLDİĞİ satıcı ve mülk sahibi kişiler (filtresiz arama ucu yok)
        private PropertyFormOptions BuildOptions(int? propertyId, bool ownerHidden, Property? current)
        {
            var userId = User.GetUserId();
            var canSeeAll = User.CanSeeAll();
            var currentOwnerId = current?.OwnerContactId;

            var owners = new Dictionary<int, string>();
            foreach (var role in new[] { ContactRole.Seller, ContactRole.Landlord })
            {
                foreach (var c in _contactService.TGetVisibleList(userId, canSeeAll, null, role, 1, 100).Items)
                    owners[c.ContactId] = $"{c.Name} {c.Surname}";
            }

            // Mevcut sahip görünürse (ama satıcı/mülk sahibi rolünde değilse) listede yine olsun
            if (!ownerHidden && currentOwnerId != null && !owners.ContainsKey(currentOwnerId.Value))
            {
                var currentOwner = _contactService.TGetVisibleById(currentOwnerId.Value, userId, canSeeAll);
                if (currentOwner != null)
                    owners[currentOwner.ContactId] = $"{currentOwner.Name} {currentOwner.Surname}";
            }

            var agents = canSeeAll ? _propertyService.TGetAssignableAgents() : new();

            // Mevcut sorumlu pasif yapıldıysa ya da rolü kaldırıldıysa listede yok. Yine de seçili kalsın, yoksa
            // tarayıcı ilk kişiyi gönderir ve yönetici başka bir alanı kaydederken sorumlu fark edilmeden değişir.
            // Aynı id geri gelince servis sorumluyu değiştirmez (doğrulama sadece değişiklikte yapılır).
            if (canSeeAll && current != null && agents.All(a => a.Id != current.AgentUserId))
            {
                var name = current.AgentUser == null ? "Sorumlu" : $"{current.AgentUser.Name} {current.AgentUser.Surname}";
                agents.Insert(0, (current.AgentUserId, name + " (pasif)"));
            }

            return new PropertyFormOptions
            {
                PropertyId = propertyId,
                OwnerHidden = ownerHidden,
                Owners = owners.OrderBy(o => o.Value, StringComparer.Create(PropertyLabels.Tr, true))
                               .Select(o => (o.Key, o.Value)).ToList(),
                CanChooseAgent = canSeeAll,
                Agents = agents
            };
        }

        // İşlemler (görev 18): sadece kullanıcının görebildiği işlemler (kendi; yönetici hepsi)
        private TransactionPanelViewModel BuildTransactionPanel(TransactionFilter filter, int userId, bool canSeeAll, int propertyId)
        {
            var (items, _) = _transactionService.TGetVisibleList(userId, canSeeAll, filter, 1, 100);
            return new TransactionPanelViewModel
            {
                PropertyId = propertyId,
                ShowProperty = false,
                Items = items.Select(r => TransactionItemViewModel.From(r, userId, _transactionService.TCanEdit(r.Transaction, userId, canSeeAll), canSeeAll)).ToList()
            };
        }

        // "İlgilenenler": sadece kullanıcının görebildiği müşteriler (sorguda filtrelenir; gizli kişi sayısı da verilmez).
        // Ekleme seçenekleri: görebildiği alıcılar (kiralıkta kiracılar), listede olanlar hariç.
        private InterestPanelViewModel BuildInterestPanel(Property p, int userId, bool canSeeAll)
        {
            var items = _interestService.TGetForProperty(p.PropertyId, userId, canSeeAll) ?? new List<PropertyInterest>();
            var existing = items.Select(i => i.ContactId).ToHashSet();
            var role = p.ListingType == ListingType.Rent ? ContactRole.Tenant : ContactRole.Buyer;

            return new InterestPanelViewModel
            {
                ForContact = false,
                AnchorId = p.PropertyId,
                Items = items.Select(i => InterestItemViewModel.From(i, _interestService.TCanDelete(i, p, userId, canSeeAll))).ToList(),
                Choices = _contactService.TGetVisibleList(userId, canSeeAll, null, role, 1, 100).Items
                    .Where(c => !existing.Contains(c.ContactId))
                    .Select(c => (c.ContactId, $"{c.Name} {c.Surname}"))
                    .ToList(),
                ChoiceHint = p.ListingType == ListingType.Rent
                    ? "Görebildiğiniz kiracı müşteriler listelenir. Kişi listede yoksa müşterinin sayfasından ekleyin."
                    : "Görebildiğiniz alıcı müşteriler listelenir. Kişi listede yoksa müşterinin sayfasından ekleyin."
            };
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static PropertyListItemViewModel ToListItem(Property p, int userId) => new()
        {
            PropertyId = p.PropertyId,
            Title = p.Title,
            ListingType = p.ListingType,
            PropertyType = p.PropertyType,
            Status = p.Status,
            Price = p.Price,
            Location = $"{p.District} / {p.City}",
            RoomCount = p.RoomCount,
            NetArea = p.NetArea,
            AgentName = p.AgentUser == null ? "" : $"{p.AgentUser.Name} {p.AgentUser.Surname}", // sadece ad
            Visibility = p.Visibility,
            IsMine = p.CreatedByUserId == userId || p.AgentUserId == userId
        };
    }
}
