using Microsoft.AspNetCore.Http;
using ProfilSayfam.Data;
using ProfilSayfam.Models.Analytics;
using System.Text.Json;

namespace ProfilSayfam.Extensions
{
    public static class AnalyticsExtensions
    {
        private static string GetIpAddress(HttpContext context)
        {
            // X-Forwarded-For header'ını kontrol et (proxy veya load balancer varsa)
            string ip = context.Request.Headers["X-Forwarded-For"].ToString();
            
            if (string.IsNullOrEmpty(ip))
            {
                // X-Real-IP header'ını kontrol et
                ip = context.Request.Headers["X-Real-IP"].ToString();
            }

            if (string.IsNullOrEmpty(ip))
            {
                // RemoteIpAddress'i kontrol et
                ip = context.Connection.RemoteIpAddress?.ToString();
            }

            // Eğer hala IP bulunamadıysa
            if (string.IsNullOrEmpty(ip))
            {
                return "Unknown";
            }

            // Eğer birden fazla IP varsa (X-Forwarded-For içinde virgülle ayrılmış olabilir)
            if (ip.Contains(","))
            {
                // İlk IP'yi al (gerçek client IP'si genelde ilk sıradadır)
                ip = ip.Split(',')[0].Trim();
            }

            return ip;
        }

        public static async Task TrackConversion(this HttpContext context, AnalyticsDbContext dbContext, string status)
        {
            var sessionId = context.Request.Cookies["SessionId"];
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                context.Response.Cookies.Append("SessionId", sessionId, new CookieOptions
                {
                    HttpOnly = true,
                    Expires = DateTimeOffset.UtcNow.AddMonths(1)
                });
            }

            var conversion = new Conversion
            {
                Status = status,
                ConvertedAt = DateTime.UtcNow,
                SessionId = sessionId,
                IpAddress = GetIpAddress(context),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                ConversionType = "General"
            };

            dbContext.Conversions.Add(conversion);
            await dbContext.SaveChangesAsync();
        }

        public static async Task TrackFormConversion(this HttpContext context, AnalyticsDbContext dbContext, string formType, object formData)
        {
            var sessionId = context.Request.Cookies["SessionId"];
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();
                context.Response.Cookies.Append("SessionId", sessionId, new CookieOptions
                {
                    HttpOnly = true,
                    Expires = DateTimeOffset.UtcNow.AddMonths(1)
                });
            }

            var conversion = new Conversion
            {
                Status = $"Form Submitted: {formType}",
                ConvertedAt = DateTime.UtcNow,
                SessionId = sessionId,
                IpAddress = GetIpAddress(context),
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                ConversionType = "Form",
                FormData = JsonSerializer.Serialize(formData)
            };

            dbContext.Conversions.Add(conversion);
            await dbContext.SaveChangesAsync();
        }
    }
}