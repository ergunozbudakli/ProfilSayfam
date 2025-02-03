using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ProfilSayfam.Controllers
{
    public class CultureController : Controller
    {
        [HttpGet]
        public IActionResult Change(string culture, string returnUrl = null)
        {
            if (culture != null)
            {
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
                );
            }

            returnUrl = returnUrl ?? Url.Action("Index", "Home");
            return LocalRedirect(returnUrl);
        }
    }
} 