using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using ProfilSayfam.Services;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProfilSayfam.Data;
namespace ProfilSayfam.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private const string AdminUsername = "admin";
        private const string AdminPassword = "Admin123!";
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<AdminController> _logger;
        private readonly AnalyticsDbContext _dbContext;
        private readonly IConfiguration _configuration;

        public AdminController(
            IAnalyticsService analyticsService, 
            ILogger<AdminController> logger,
            AnalyticsDbContext dbContext,
            IConfiguration configuration)
        {
            _analyticsService = analyticsService;
            _logger = logger;
            _dbContext = dbContext;
            _configuration = configuration;
        }

        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string returnUrl = null)
        {
            if (username == AdminUsername && password == AdminPassword)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return RedirectToLocal(returnUrl);
            }

            ModelState.AddModelError("", "Geçersiz kullanıcı adı veya şifre");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [Authorize]
        public IActionResult Index()
        {
            return View();
        }

        [Authorize]
        public IActionResult Analytics()
        {
            return View();
        }

        [Authorize]
        [Route("Admin/Analytics/Locations")]
        public async Task<IActionResult> Locations()
        {
            try
            {
                var visits = await _dbContext.Visits
                    .Where(v => v.Latitude != 0 && v.Longitude != 0)
                    .Select(v => new
                    {
                        v.Latitude,
                        v.Longitude,
                        v.Country,
                        v.City,
                        v.IpAddress,
                        v.VisitedAt
                    })
                    .ToListAsync();

                ViewBag.GoogleMapsApiKey = _configuration["GoogleMaps:ApiKey"];
                return View("~/Views/Admin/Analytics/Locations.cshtml", visits);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ziyaret konumları alınırken hata oluştu");
                return View("~/Views/Admin/Analytics/Locations.cshtml", Enumerable.Empty<dynamic>());
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAnalyticsData()
        {
            try
            {
                var realTimeData = await _analyticsService.GetRealTimeData();
                var dailyStats = await _analyticsService.GetDailyStats();
                var conversionStats = await _analyticsService.GetConversionStats();

                var response = new
                {
                    activeUsers = realTimeData["activeUsers"],
                    todayVisits = realTimeData["todayVisits"],
                    totalConversions = realTimeData["totalConversions"],
                    conversionRate = realTimeData["conversionRate"],
                    hourlyVisits = new[] { realTimeData["hourlyVisits"] },
                    conversionDistribution = new[] 
                    {
                        realTimeData["successConversions"],
                        realTimeData["failedConversions"],
                        realTimeData["processingConversions"]
                    }
                };

                return Json(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Analytics verisi alınırken hata oluştu: {ex.Message}");
                return StatusCode(500, "İstatistikler alınırken bir hata oluştu.");
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Admin");
        }
    }
} 