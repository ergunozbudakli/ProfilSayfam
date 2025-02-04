namespace ProfilSayfam.Models.Analytics
{
    public class Conversion
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public DateTime ConvertedAt { get; set; }
        public string SessionId { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string ConversionType { get; set; } = "General";
        public string? FormData { get; set; }
    }
}