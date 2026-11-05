using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.ViewComponents.Layout
{
    // Sol menü (InstrumentTrade'deki AdminLayoutSidebarViewComponent ile aynı yapı).
    // Menüde bir bağlantıyı gizlemek yetki kontrolü DEĞİLDİR; asıl kontrol controller'lardadır.
    public class LayoutSidebarViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View();
        }
    }
}
