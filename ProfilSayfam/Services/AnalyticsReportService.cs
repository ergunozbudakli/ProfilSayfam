using ProfilSayfam.Data;
using ProfilSayfam.Models.Analytics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ProfilSayfam.Services
{
    public interface IAnalyticsReportService
    {
        Task<DashboardStats> GetDashboardStats();
        Task<List<LocationStats>> GetLocationStats();
        Task<List<BrowserStats>> GetBrowserStats();
        Task<List<DailyStats>> GetDailyStats(DateTime startDate, DateTime endDate);
        Task<List<MonthlyStats>> GetMonthlyStats(int year);
    }

    public class AnalyticsReportService : IAnalyticsReportService
    {
        private readonly AnalyticsDbContext _context;
        private readonly IGeoLocationService _geoLocationService;
        private readonly ILogger<AnalyticsReportService> _logger;

        public AnalyticsReportService(AnalyticsDbContext context, IGeoLocationService geoLocationService, ILogger<AnalyticsReportService> logger)
        {
            _context = context;
            _geoLocationService = geoLocationService;
            _logger = logger;
        }

        public async Task<DashboardStats> GetDashboardStats()
        {
            var now = DateTime.UtcNow;
            var today = DateTime.UtcNow.Date;
            var thisMonth = new DateTime(now.Year, now.Month, 1);
            var thisYear = new DateTime(now.Year, 1, 1);

            return new DashboardStats
            {
                TotalPageViews = await _context.Visits.CountAsync(),
                TotalConversions = await _context.Conversions.CountAsync(),
                DailyPageViews = await _context.Visits.CountAsync(x => x.VisitedAt >= today),
                MonthlyPageViews = await _context.Visits.CountAsync(x => x.VisitedAt >= thisMonth),
                YearlyPageViews = await _context.Visits.CountAsync(x => x.VisitedAt >= thisYear),
                UniqueVisitors = await _context.Visits.Select(x => x.IpAddress).Distinct().CountAsync()
            };
        }

        public async Task<List<LocationStats>> GetLocationStats()
        {
            return await _context.Visits
                .Where(v => v.Latitude != 0 && v.Longitude != 0)
                .GroupBy(v => new { v.Country, v.City, v.Region, v.Latitude, v.Longitude })
                .Select(g => new LocationStats
                {
                    Country = g.Key.Country,
                    City = g.Key.City,
                    Region = g.Key.Region,
                    Latitude = (double)g.Key.Latitude,
                    Longitude = (double)g.Key.Longitude,
                    VisitCount = g.Count()
                })
                .OrderByDescending(x => x.VisitCount)
                .ToListAsync();
        }

        public async Task<List<BrowserStats>> GetBrowserStats()
        {
            try
            {
                // Önce tüm ziyaretleri çek
                var visits = await _context.Visits
                    .Select(v => new { v.UserAgent })
                    .ToListAsync();

                _logger.LogInformation($"Toplam {visits.Count} ziyaret verisi çekildi.");

                // Bellek üzerinde gruplama yap
                var browserGroups = visits
                    .GroupBy(x => new { 
                        Browser = GetBrowserInfo(x.UserAgent), 
                        Platform = GetPlatformInfo(x.UserAgent) 
                    })
                    .Select(g => new BrowserStats
                    {
                        Browser = g.Key.Browser,
                        Platform = g.Key.Platform,
                        VisitCount = g.Count()
                    })
                    .OrderByDescending(x => x.VisitCount)
                    .ToList();

                // Eğer veri yoksa
                if (!browserGroups.Any())
                {
                    _logger.LogWarning("Hiç tarayıcı verisi bulunamadı.");
                    return new List<BrowserStats>();
                }

                _logger.LogInformation($"Toplam {browserGroups.Count} farklı tarayıcı/platform kombinasyonu bulundu.");
                foreach (var stat in browserGroups)
                {
                    _logger.LogDebug($"Tarayıcı: {stat.Browser}, Platform: {stat.Platform}, Ziyaret: {stat.VisitCount}");
                }

                return browserGroups;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tarayıcı istatistikleri alınırken hata oluştu.");
                throw;
            }
        }

        public async Task<List<DailyStats>> GetDailyStats(DateTime startDate, DateTime endDate)
        {
            return await _context.Visits
                .Where(x => x.VisitedAt >= startDate && x.VisitedAt <= endDate)
                .GroupBy(x => x.VisitedAt.Date)
                .Select(g => new DailyStats
                {
                    Date = g.Key,
                    PageViews = g.Count(),
                    UniqueVisitors = g.Select(x => x.IpAddress).Distinct().Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();
        }

        public async Task<List<MonthlyStats>> GetMonthlyStats(int year)
        {
            return await _context.Visits
                .Where(x => x.VisitedAt.Year == year)
                .GroupBy(x => new { x.VisitedAt.Year, x.VisitedAt.Month })
                .Select(g => new MonthlyStats
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    PageViews = g.Count(),
                    UniqueVisitors = g.Select(x => x.IpAddress).Distinct().Count()
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ToListAsync();
        }

        private static string GetBrowserInfo(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Bilinmiyor";
            
            userAgent = userAgent.ToLower();
            
            // Edge önce kontrol edilmeli çünkü Chrome'u da içerir
            if (userAgent.Contains("edg/")) return "Edge";
            if (userAgent.Contains("chrome/") && !userAgent.Contains("edg/")) return "Chrome";
            if (userAgent.Contains("firefox/")) return "Firefox";
            if (userAgent.Contains("safari/") && !userAgent.Contains("chrome") && !userAgent.Contains("edg/")) return "Safari";
            if (userAgent.Contains("opr/") || userAgent.Contains("opera/")) return "Opera";
            if (userAgent.Contains("msie") || userAgent.Contains("trident/")) return "Internet Explorer";
            
            return "Diğer";
        }

        private static string GetPlatformInfo(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return "Bilinmiyor";
            
            userAgent = userAgent.ToLower();
            
            // Mobil platformlar önce kontrol edilmeli
            if (userAgent.Contains("iphone") || userAgent.Contains("ipad") || userAgent.Contains("ipod")) return "iOS";
            if (userAgent.Contains("android")) return "Android";
            
            // Masaüstü platformlar
            if (userAgent.Contains("windows")) return "Windows";
            if (userAgent.Contains("macintosh") || userAgent.Contains("mac os")) return "MacOS";
            if (userAgent.Contains("linux") && !userAgent.Contains("android")) return "Linux";
            
            return "Diğer";
        }
    }

    public class DashboardStats
    {
        public int TotalPageViews { get; set; }
        public int TotalConversions { get; set; }
        public int DailyPageViews { get; set; }
        public int MonthlyPageViews { get; set; }
        public int YearlyPageViews { get; set; }
        public int UniqueVisitors { get; set; }
    }

    public class DailyStats
    {
        public DateTime Date { get; set; }
        public int PageViews { get; set; }
        public int UniqueVisitors { get; set; }
    }

    public class MonthlyStats
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int PageViews { get; set; }
        public int UniqueVisitors { get; set; }
    }
} 