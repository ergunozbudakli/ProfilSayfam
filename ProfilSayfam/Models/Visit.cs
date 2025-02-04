namespace ProfilSayfam.Models
{
    public class Visit
    {
        public int Id { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Path { get; set; }
        public DateTime VisitedAt { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Region { get; set; }
        public string? Isp { get; set; }
    }
} 