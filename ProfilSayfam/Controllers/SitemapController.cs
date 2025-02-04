using Microsoft.AspNetCore.Mvc;

namespace ProfilSayfam.Controllers
{
    public class SitemapController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;

        public SitemapController(IWebHostEnvironment webHostEnvironment, IConfiguration configuration)
        {
            _webHostEnvironment = webHostEnvironment;
            _configuration = configuration;
        }

        [HttpGet]
        [Route("sitemap.xml")]
        public IActionResult Index()
        {
            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, "sitemap.xml");
            var fileStream = System.IO.File.OpenRead(filePath);
            return File(fileStream, "application/xml");
        }
    }
} 