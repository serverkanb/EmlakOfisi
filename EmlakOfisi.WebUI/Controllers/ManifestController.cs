using EmlakOfisi.WebUI.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.Controllers
{
    // PWA manifest'i (görev 16) ofis ayarından üretir; ofis adı koda sabit yazılmaz.
    // GİRİŞSİZ: tarayıcı manifest'i çerez göndermeden ister. Çıktıda sadece ad, renk ve ikon yolları var (kişisel veri yok).
    [AllowAnonymous]
    public class ManifestController : Controller
    {
        private readonly OfficeSettings _office;

        public ManifestController(OfficeSettings office)
        {
            _office = office;
        }

        [HttpGet("/manifest.webmanifest")]
        public IActionResult Index()
        {
            var icons = new List<Dictionary<string, string>>();
            void AddIcon(string? path, string size)
            {
                if (path != null)
                    icons.Add(new() { ["src"] = path, ["sizes"] = size, ["type"] = "image/png", ["purpose"] = "any" });
            }
            AddIcon(_office.Icon192Path, "192x192");
            AddIcon(_office.Icon512Path, "512x512");

            var manifest = new Dictionary<string, object>
            {
                ["name"] = _office.Name,
                ["short_name"] = _office.ShortName,
                ["description"] = "Emlak ofisi müşteri, portföy ve randevu yönetimi",
                ["lang"] = "tr",
                ["dir"] = "ltr",
                ["start_url"] = "/",
                ["scope"] = "/",
                ["display"] = "standalone",
                ["orientation"] = "any",
                ["background_color"] = "#ffffff",
                ["theme_color"] = "#2C2928",
                ["icons"] = icons
            };

            Response.Headers.CacheControl = "no-cache";
            return new JsonResult(manifest) { ContentType = "application/manifest+json; charset=utf-8" };
        }
    }
}
