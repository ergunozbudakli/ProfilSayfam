using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProfilSayfam.Services;

namespace ProfilSayfam.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("Admin/[controller]")]
    [Authorize]
    public class AnalyticsController : Controller
    {
        private readonly IAnalyticsReportService _analyticsReportService;

        public AnalyticsController(IAnalyticsReportService analyticsReportService)
        {
            _analyticsReportService = analyticsReportService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var dashboardStats = await _analyticsReportService.GetDashboardStats();
            return View(dashboardStats);
        }

        [HttpGet("locations")]
        public async Task<IActionResult> Locations()
        {
            var locationStats = await _analyticsReportService.GetLocationStats();
            return View(locationStats);
        }

        [HttpGet("browsers")]
        public async Task<IActionResult> Browsers()
        {
            var browserStats = await _analyticsReportService.GetBrowserStats();
            return View(browserStats);
        }

        [HttpGet("daily")]
        public async Task<IActionResult> Daily(DateTime? startDate, DateTime? endDate)
        {
            startDate ??= DateTime.UtcNow.AddDays(-30);
            endDate ??= DateTime.UtcNow;

            var dailyStats = await _analyticsReportService.GetDailyStats(startDate.Value, endDate.Value);
            ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            return View(dailyStats);
        }

        [HttpGet("monthly")]
        public async Task<IActionResult> Monthly(int? year)
        {
            year ??= DateTime.UtcNow.Year;

            var monthlyStats = await _analyticsReportService.GetMonthlyStats(year.Value);
            ViewBag.Year = year.Value;
            return View(monthlyStats);
        }
    }
} 