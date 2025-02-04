using Microsoft.AspNetCore.Http;
using ProfilSayfam.Services;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ProfilSayfam.Middleware
{
    public class AnalyticsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AnalyticsMiddleware> _logger;
        private readonly IGeoLocationService _geoLocationService;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public AnalyticsMiddleware(
            RequestDelegate next,
            ILogger<AnalyticsMiddleware> logger,
            IGeoLocationService geoLocationService,
            IServiceScopeFactory serviceScopeFactory)
        {
            _next = next;
            _logger = logger;
            _geoLocationService = geoLocationService;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
                try
                {
                    // Statik dosyaları ve API isteklerini atla
                    if (!ShouldTrackRequest(context.Request))
                    {
                        await _next(context);
                        return;
                    }

                    try
                    {
                        // IP adresini al
                        var ipAddress = GetIpAddress(context);
                        _logger.LogInformation($"IP Adresi alındı: {ipAddress}");
                        
                        // Lokasyon bilgisini al
                        var locationInfo = await _geoLocationService.GetLocationInfo(ipAddress);
                        _logger.LogInformation($"Lokasyon bilgisi alındı: {locationInfo.Country}, {locationInfo.City}");

                        // Analytics verilerini kaydet
                        var visitData = new VisitData
                        {
                            IpAddress = ipAddress,
                            UserAgent = context.Request.Headers["User-Agent"].ToString(),
                            Path = context.Request.Path.Value ?? "/",
                            Timestamp = DateTime.UtcNow,
                            Country = locationInfo.Country,
                            City = locationInfo.City,
                            Region = locationInfo.Region,
                            Isp = locationInfo.Isp,
                            Latitude = (decimal)locationInfo.Latitude,
                            Longitude = (decimal)locationInfo.Longitude
                        };

                        _logger.LogInformation($"Ziyaret verisi hazırlandı: {visitData.Path}");
                        await analyticsService.TrackVisit(visitData);
                        _logger.LogInformation("Ziyaret başarıyla kaydedildi");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ziyaret işlenirken detaylı hata: {Message}", ex.Message);
                        if (ex.InnerException != null)
                        {
                            _logger.LogError(ex.InnerException, "İç hata: {Message}", ex.InnerException.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Analytics middleware'inde genel hata oluştu");
                }

                await _next(context);
            }
        }

        private bool ShouldTrackRequest(HttpRequest request)
        {
            // Statik dosyaları atla
            if (request.Path.StartsWithSegments("/assets") ||
                request.Path.StartsWithSegments("/lib") ||
                request.Path.StartsWithSegments("/js") ||
                request.Path.StartsWithSegments("/css") ||
                request.Path.StartsWithSegments("/images") ||
                (request.Path.Value?.EndsWith(".ico") ?? false))
            {
                _logger.LogDebug($"Statik dosya isteği atlandı: {request.Path}");
                return false;
            }

            // API isteklerini atla
            if (request.Path.StartsWithSegments("/api"))
            {
                _logger.LogDebug($"API isteği atlandı: {request.Path}");
                return false;
            }

            // Sadece GET isteklerini takip et
            if (request.Method != "GET")
            {
                _logger.LogDebug($"GET olmayan istek atlandı: {request.Method} {request.Path}");
                return false;
            }

            _logger.LogInformation($"İstek takip edilecek: {request.Method} {request.Path}");
            return true;
        }

        private string GetIpAddress(HttpContext context)
        {
            try
            {
                // X-Forwarded-For header'ını kontrol et (proxy/load balancer durumu için)
                string ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                _logger.LogDebug($"X-Forwarded-For IP: {ip}");
                
                if (string.IsNullOrEmpty(ip))
                {
                    // Remote IP adresini al
                    ip = context.Connection.RemoteIpAddress?.ToString();
                    _logger.LogDebug($"Remote IP: {ip}");
                }
                else
                {
                    // X-Forwarded-For birden fazla IP içerebilir, ilkini al
                    ip = ip.Split(',').First().Trim();
                    _logger.LogDebug($"Parsed X-Forwarded-For IP: {ip}");
                }

                // IP adresini doğrula
                if (string.IsNullOrEmpty(ip))
                {
                    _logger.LogWarning("IP adresi bulunamadı, varsayılan değer kullanılıyor");
                    return "127.0.0.1";
                }

                // Localhost kontrolü
                if (ip == "::1" || ip == "127.0.0.1")
                {
                    _logger.LogDebug("Localhost IP'si tespit edildi, gerçek IP kullanılıyor");
                    return "127.0.0.1";
                }

                _logger.LogInformation($"Geçerli IP adresi: {ip}");
                return ip;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IP adresi alınırken hata oluştu");
                return "127.0.0.1";
            }
        }
    }

    public class VisitData
    {
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Path { get; set; }
        public DateTime Timestamp { get; set; }
        public string Country { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string Isp { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
    }

    public static class AnalyticsMiddlewareExtensions
    {
        public static IApplicationBuilder UseAnalytics(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AnalyticsMiddleware>();
        }
    }
} 