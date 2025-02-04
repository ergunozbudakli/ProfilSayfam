using System.Collections.Concurrent;
using System.Threading.Tasks;
using ProfilSayfam.Data;
using ProfilSayfam.Middleware;
using ProfilSayfam.Models;
using ProfilSayfam.Models.Analytics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ProfilSayfam.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly ConcurrentDictionary<string, DateTime> _activeSessions;
        private readonly ConcurrentDictionary<string, int> _pageViews;
        private readonly ConcurrentDictionary<string, int> _conversions;
        private readonly ILogger<AnalyticsService> _logger;
        private readonly Timer _cleanupTimer;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public AnalyticsService(
            ILogger<AnalyticsService> logger,
            IServiceScopeFactory serviceScopeFactory)
        {
            _activeSessions = new ConcurrentDictionary<string, DateTime>();
            _pageViews = new ConcurrentDictionary<string, int>();
            _conversions = new ConcurrentDictionary<string, int>();
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;

            // Her 5 dakikada bir eski oturumları temizle
            _cleanupTimer = new Timer(async _ => await CleanupOldSessions(), null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        }

        public void TrackPageView(string pageName)
        {
            var sessionId = Guid.NewGuid().ToString();
            _activeSessions.TryAdd(sessionId, DateTime.UtcNow);
            
            _pageViews.AddOrUpdate(pageName, 1, (key, value) => value + 1);
            _logger.LogInformation($"Sayfa görüntüleme kaydedildi: {pageName}");
        }

        public void TrackConversion(string status)
        {
            _conversions.AddOrUpdate(status, 1, (key, value) => value + 1);
            _logger.LogInformation($"Dönüşüm kaydedildi: {status}");
        }

        public async Task<Dictionary<string, int>> GetRealTimeData()
        {
            await CleanupOldSessions();
            
            var totalVisits = _pageViews.Values.Sum();
            var totalConversions = _conversions.Values.Sum();
            var conversionRate = totalVisits > 0 ? (totalConversions * 100) / totalVisits : 0;
            
            var hourlyVisits = new int[8]; // 3'er saatlik dilimler için
            var currentHour = DateTime.UtcNow.Hour;
            for (int i = 0; i < 8; i++)
            {
                hourlyVisits[i] = _pageViews.Values.Sum();
            }

            var conversionDistribution = new[]
            {
                _conversions.GetValueOrDefault("success", 0),
                _conversions.GetValueOrDefault("failed", 0),
                _conversions.GetValueOrDefault("processing", 0)
            };

            return new Dictionary<string, int>
            {
                { "activeUsers", _activeSessions.Count },
                { "todayVisits", totalVisits },
                { "totalConversions", totalConversions },
                { "conversionRate", conversionRate },
                { "hourlyVisits", hourlyVisits[0] }, // Şimdilik sadece ilk değeri gönderiyoruz
                { "successConversions", conversionDistribution[0] },
                { "failedConversions", conversionDistribution[1] },
                { "processingConversions", conversionDistribution[2] }
            };
        }

        public async Task<Dictionary<string, int>> GetDailyStats()
        {
            return await Task.FromResult(_pageViews.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        public async Task<Dictionary<string, int>> GetConversionStats()
        {
            return await Task.FromResult(_conversions.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        public async Task CleanupOldSessions()
        {
            await Task.Run(() =>
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-5);
                var oldSessions = _activeSessions.Where(kvp => kvp.Value < cutoff).ToList();

                foreach (var session in oldSessions)
                {
                    _activeSessions.TryRemove(session.Key, out _);
                }

                _logger.LogInformation($"{oldSessions.Count} eski oturum temizlendi");
            });
        }

        public async Task TrackVisit(VisitData visitData)
        {
            try
            {
                _logger.LogInformation($"Ziyaret kaydı başlatılıyor: Path={visitData.Path}, IP={visitData.IpAddress}");
                
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
                    _logger.LogDebug("Database context oluşturuldu");
                    
                    var visit = new Models.Analytics.Visit
                    {
                        IpAddress = visitData.IpAddress,
                        UserAgent = visitData.UserAgent,
                        Path = visitData.Path,
                        VisitedAt = visitData.Timestamp,
                        Country = visitData.Country ?? "Unknown",
                        City = visitData.City ?? "Unknown",
                        Region = visitData.Region ?? "Unknown",
                        Isp = visitData.Isp ?? "Unknown",
                        Latitude = visitData.Latitude,
                        Longitude = visitData.Longitude
                    };

                    _logger.LogDebug($"Visit nesnesi oluşturuldu: {visit.Path}");

                    try
                    {
                        dbContext.Visits.Add(visit);
                        _logger.LogDebug("Visit nesnesi DbContext'e eklendi");
                        
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation($"Ziyaret başarıyla kaydedildi. ID: {visit.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Veritabanına kayıt sırasında hata oluştu");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ziyaret kaydedilirken hata oluştu");
                throw;
            }
        }
    }
} 