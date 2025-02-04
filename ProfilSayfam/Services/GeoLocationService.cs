using System.Text.Json;
using System.Net.Http;

namespace ProfilSayfam.Services
{
    public interface IGeoLocationService
    {
        Task<LocationInfo> GetLocationInfo(string ipAddress);
    }

    public class GeoLocationService : IGeoLocationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeoLocationService> _logger;
        private readonly IConfiguration _configuration;

        public GeoLocationService(ILogger<GeoLocationService> logger, IConfiguration configuration)
        {
            _httpClient = new HttpClient();
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<LocationInfo> GetLocationInfo(string ipAddress)
        {
            try
            {
                // IP adresini doğrula
                if (string.IsNullOrEmpty(ipAddress) || ipAddress.ToLower() == "unknown" || ipAddress == "::1")
                {
                    _logger.LogWarning("Geçersiz IP adresi: {IpAddress}", ipAddress);
                    return GetDefaultLocationInfo();
                }

                // Önce ipapi.com'u dene
                var ipapiResponse = await TryIpApi(ipAddress);
                if (ipapiResponse != null)
                {
                    return ipapiResponse;
                }

                // ipapi.com başarısız olursa ip-api.com'u dene
                var ipApiResponse = await TryIpApiCom(ipAddress);
                if (ipApiResponse != null)
                {
                    return ipApiResponse;
                }

                // Her iki servis de başarısız olursa varsayılan değerleri döndür
                _logger.LogWarning("Hiçbir lokasyon servisi çalışmadı: {IpAddress}", ipAddress);
                return GetDefaultLocationInfo();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IP lokasyon bilgisi alınırken hata oluştu: {IpAddress}", ipAddress);
                return GetDefaultLocationInfo();
            }
        }

        private async Task<LocationInfo> TryIpApi(string ipAddress)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"https://ipapi.co/{ipAddress}/json/");
                var result = JsonSerializer.Deserialize<IpapiResponse>(response);

                if (result != null && !string.IsNullOrEmpty(result.country_name))
                {
                    return new LocationInfo
                    {
                        Country = result.country_name,
                        City = result.city ?? "Bilinmiyor",
                        Region = result.region ?? "Bilinmiyor",
                        Isp = result.org ?? "Bilinmiyor",
                        Latitude = result.latitude ?? 0,
                        Longitude = result.longitude ?? 0
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ipapi.co servisi başarısız oldu: {IpAddress}", ipAddress);
            }

            return null;
        }

        private async Task<LocationInfo> TryIpApiCom(string ipAddress)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"http://ip-api.com/json/{ipAddress}");
                var result = JsonSerializer.Deserialize<IpApiComResponse>(response);

                if (result != null && result.status == "success")
                {
                    return new LocationInfo
                    {
                        Country = result.country,
                        City = result.city ?? "Bilinmiyor",
                        Region = result.regionName ?? "Bilinmiyor",
                        Isp = result.isp ?? "Bilinmiyor",
                        Latitude = result.lat ?? 0,
                        Longitude = result.lon ?? 0
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ip-api.com servisi başarısız oldu: {IpAddress}", ipAddress);
            }

            return null;
        }

        private LocationInfo GetDefaultLocationInfo()
        {
            return new LocationInfo
            {
                Country = "Türkiye",
                City = "İstanbul",
                Region = "Marmara",
                Isp = "Default ISP",
                Latitude = 41.0082,
                Longitude = 28.9784
            };
        }

        public class IpapiResponse
        {
            public string? ip { get; set; }
            public string? country_name { get; set; }
            public string? city { get; set; }
            public string? region { get; set; }
            public string? org { get; set; }
            public double? latitude { get; set; }
            public double? longitude { get; set; }
        }

        public class IpApiComResponse
        {
            public string? status { get; set; }
            public string? query { get; set; }
            public string? country { get; set; }
            public string? city { get; set; }
            public string? regionName { get; set; }
            public string? isp { get; set; }
            public double? lat { get; set; }
            public double? lon { get; set; }
        }
    }

    public class LocationInfo
    {
        public string Country { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string Isp { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
} 