using Microsoft.AspNetCore.Mvc;
using ProfilSayfam.Data;
using ProfilSayfam.Extensions;
using ProfilSayfam.Models;

namespace ProfilSayfam.Controllers
{
    public class ContactController : Controller
    {
        private readonly AnalyticsDbContext _dbContext;

        public ContactController(AnalyticsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new ContactForm());
        }

        [HttpPost]
        public async Task<IActionResult> Send(ContactForm form)
        {
            if (ModelState.IsValid)
            {
                // Form işlemleri...

                // Dönüşümü kaydet
                await HttpContext.TrackFormConversion(_dbContext, "Contact", new
                {
                    form.Name,
                    form.Email,
                    form.Subject,
                    MessageLength = form.Message?.Length ?? 0,
                    Timestamp = DateTime.UtcNow
                });

                TempData["SuccessMessage"] = "Mesajınız başarıyla gönderildi. En kısa sürede size dönüş yapacağız.";
                return RedirectToAction("ThankYou");
            }

            return View("Index", form);
        }

        public IActionResult ThankYou()
        {
            if (TempData["SuccessMessage"] == null)
            {
                return RedirectToAction("Index");
            }

            return View();
        }
    }
} 