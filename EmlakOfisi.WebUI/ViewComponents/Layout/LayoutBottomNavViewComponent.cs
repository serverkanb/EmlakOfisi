using Microsoft.AspNetCore.Mvc;

namespace EmlakOfisi.WebUI.ViewComponents.Layout
{
    // Mobilde ekranın altında sabit duran 4 butonluk menü (lg ve üstünde gizli)
    public class LayoutBottomNavViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View();
        }
    }
}
