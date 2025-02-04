using ProfilSayfam.Middleware;

namespace ProfilSayfam.Services
{
    public interface IAnalyticsService
    {
        void TrackPageView(string pageName);
        void TrackConversion(string status);
        Task TrackVisit(VisitData visitData);
        Task<Dictionary<string, int>> GetRealTimeData();
        Task<Dictionary<string, int>> GetDailyStats();
        Task<Dictionary<string, int>> GetConversionStats();
        Task CleanupOldSessions();
    }
} 