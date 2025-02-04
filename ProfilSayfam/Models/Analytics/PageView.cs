namespace ProfilSayfam.Models.Analytics
{
    public class PageView
    {
        public int Id { get; set; }
        public string PageName { get; set; }
        public DateTime ViewedAt { get; set; }
        public string SessionId { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
    }
} 