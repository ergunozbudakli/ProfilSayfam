using Microsoft.AspNetCore.Mvc;
using ProfilSayfam.Models;
using ProfilSayfam.Services;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Drawing;
using System.Drawing.Imaging;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using System.IO.Compression;

namespace ProfilSayfam.Controllers
{
    public class HomeController : Controller
    {
        private const double LINE_THRESHOLD = 1.5;
        private const double SPACE_THRESHOLD = 5.0;
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult FormatConverter()
        {
            return View();
        }

        [HttpPost]
        [RequestFormLimits(MultipartBodyLengthLimit = 104857600)] // 100 MB
        [RequestSizeLimit(104857600)] // 100 MB
        public async Task<IActionResult> ConvertPdfToWord([FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("Dosya gönderilmedi veya boş dosya gönderildi.");
                    return BadRequest("Dosya gönderilmedi veya boş dosya gönderildi.");
                }

                if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning($"Geçersiz dosya tipi: {file.ContentType}");
                    return BadRequest("Lütfen sadece PDF dosyası yükleyin.");
                }

                // Geçici dizin oluştur
                var tempPath = Path.Combine(_webHostEnvironment.WebRootPath, "temp");
                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                    _logger.LogInformation($"Temp dizini oluşturuldu: {tempPath}");
                }

                // Dosya adlarını oluştur
                var safeFileName = SafeFileName(file.FileName);
                var pdfPath = Path.Combine(tempPath, safeFileName);
                var docxPath = Path.Combine(tempPath, Path.ChangeExtension(safeFileName, ".docx"));

                // Varolan dosyaları temizle
                if (System.IO.File.Exists(pdfPath))
                    System.IO.File.Delete(pdfPath);
                if (System.IO.File.Exists(docxPath))
                    System.IO.File.Delete(docxPath);

                // PDF dosyasını kaydet
                using (var stream = new FileStream(pdfPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _logger.LogInformation("PDF dosyası kaydedildi");

                // PDF'i oku ve Word'e dönüştür
                using (var document = PdfDocument.Open(pdfPath))
                {
                    _logger.LogInformation($"PDF açıldı, sayfa sayısı: {document.NumberOfPages}");

                    using (var wordDocument = WordprocessingDocument.Create(docxPath, WordprocessingDocumentType.Document))
                    {
                        var mainPart = wordDocument.AddMainDocumentPart();
                        mainPart.Document = new Document();

                        var body = mainPart.Document.AppendChild(new Body());
                        var sectionProperties = new SectionProperties(
                            new PageMargin() { 
                                Top = 1440,     // 1 inç (yaklaşık 25.4mm)
                                Right = 0,
                                Bottom = 0,
                                Left = 1440,    // 1 inç (yaklaşık 25.4mm)
                                Header = 0,
                                Footer = 0,
                                Gutter = 0
                            }
                        );
                        body.AppendChild(sectionProperties);

                        // Stil tanımlamalarını ekle
                        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
                        var styles = new Styles(
                            new DocDefaults(
                                new RunPropertiesDefault(
                                    new RunProperties(
                                        new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
                                        new FontSize { Val = "24" }
                                    )
                                ),
                                new ParagraphPropertiesDefault(
                                    new ParagraphProperties(
                                        new SpacingBetweenLines { 
                                            Before = "0",
                                            After = "0",
                                            Line = "240",
                                            LineRule = LineSpacingRuleValues.Auto
                                        },
                                        new Indentation { Left = "0", Right = "0", FirstLine = "0" }
                                    )
                                )
                            )
                        );
                        stylesPart.Styles = styles;

                        Paragraph currentParagraph = null;
                        var lastY = 0.0;

                        foreach (var page in document.GetPages())
                        {
                            // Sayfadaki içeriği kontrol et
                            var pageWords = page.GetWords().ToList();
                            var pageImages = page.GetImages().ToList();

                            // Sayfa boşsa atla
                            if (pageWords.Count == 0 && pageImages.Count == 0)
                            {
                                _logger.LogInformation($"Sayfa {page.Number} boş olduğu için atlanıyor.");
                                continue;
                            }

                            // İlk sayfa değilse sayfa sonu ekle
                            if (page.Number > 1)
                            {
                                body.AppendChild(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                            }

                            // Sayfadaki görselleri işle
                            var imageIndex = 1U;
                            foreach (var image in pageImages)
                            {
                                try
                                {
                                    byte[] imageBytes = image.RawBytes.ToArray();
                                    if (imageBytes == null || imageBytes.Length == 0)
                                    {
                                        _logger.LogWarning($"Sayfa {page.Number}: Boş görsel verisi.");
                                        continue;
                                    }

                                    // PDF'deki görsel bilgilerini logla
                                    _logger.LogInformation($"Sayfa {page.Number}: PDF Görsel Detayları:");
                                    _logger.LogInformation($"- Görsel Boyutu: {imageBytes.Length} bytes");
                                    
                                    // Görsel boyutlarını al
                                    var bounds = image.Bounds;
                                    int width = (int)Math.Ceiling(bounds.Width);
                                    int height = (int)Math.Ceiling(bounds.Height);
                                    
                                    _logger.LogInformation($"- Görsel Genişliği: {width}");
                                    _logger.LogInformation($"- Görsel Yüksekliği: {height}");
                                    
                                    // İlk birkaç byte'ı kontrol et
                                    var headerBytes = imageBytes.Take(8).ToArray();
                                    var headerHex = BitConverter.ToString(headerBytes);
                                    _logger.LogInformation($"- İlk 8 Byte (Hex): {headerHex}");

                                    // Görsel verilerini doğrula ve formatı belirle
                                    string imageFormat = DetermineImageFormat(imageBytes);
                                    if (string.IsNullOrEmpty(imageFormat))
                                    {
                                        // Format belirlenemezse tüm formatları dene
                                        imageFormat = TryAllImageFormats(imageBytes);
                                        if (string.IsNullOrEmpty(imageFormat))
                                        {
                                            _logger.LogWarning($"Sayfa {page.Number}: Hiçbir format ile açılamadı, görsel atlanıyor.");
                                            continue;
                                        }
                                    }

                                    _logger.LogInformation($"Sayfa {page.Number}: Görsel formatı belirlendi: {imageFormat}");

                                    // Görseli işle ve optimize et
                                    byte[] processedImageBytes = ProcessImage(imageBytes, imageFormat);
                                    if (processedImageBytes == null || processedImageBytes.Length == 0)
                                    {
                                        _logger.LogWarning($"Sayfa {page.Number}: Görsel işlenemedi, orijinal veri deneniyor...");
                                        processedImageBytes = imageBytes;
                                    }

                                    // Görsel verilerini kontrol et
                                    using var processedMs = new MemoryStream(processedImageBytes);
                                    using var img = System.Drawing.Image.FromStream(processedMs);

                                    if (img.Width < 1 || img.Height < 1)
                                    {
                                        _logger.LogWarning($"Sayfa {page.Number}: Geçersiz görsel boyutları: {img.Width}x{img.Height}");
                                        continue;
                                    }

                                    // Görsel boyutlarını ayarla
                                    double ratio = (double)img.Height / img.Width;
                                    long emuWidth = img.Width * 9525;
                                    long emuHeight = img.Height * 9525;

                                    // Maksimum ve minimum boyut kontrolleri
                                    const long maxWidth = 9144000L; // 15 cm
                                    const long minWidth = 914400L;  // 1.5 cm

                                    if (emuWidth > maxWidth)
                                    {
                                        emuWidth = maxWidth;
                                        emuHeight = (long)(emuWidth * ratio);
                                    }
                                    else if (emuWidth < minWidth)
                                    {
                                        emuWidth = minWidth;
                                        emuHeight = (long)(minWidth * ratio);
                                    }

                                    // Word belgesine ekle
                                    var imagePart = mainPart.AddImagePart(GetImagePartType(imageFormat));
                                    using (var stream = new MemoryStream(processedImageBytes))
                                    {
                                        imagePart.FeedData(stream);
                                    }

                                    var element = CreateImageElement(mainPart, imagePart, emuWidth, emuHeight, imageIndex++);
                                    body.AppendChild(new Paragraph(
                                        new ParagraphProperties(
                                            new SpacingBetweenLines { 
                                                After = "0",
                                                Before = "0",
                                                Line = "240",
                                                LineRule = LineSpacingRuleValues.Auto
                                            },
                                            new Justification { Val = JustificationValues.Left }
                                        ),
                                        new Run(element)
                                    ));

                                    _logger.LogInformation($"Sayfa {page.Number}: Görsel başarıyla eklendi. Boyut: {img.Width}x{img.Height}, Format: {imageFormat}");
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError($"Sayfa {page.Number}: Görsel işleme hatası: {ex.Message}");
                                }
                            }

                            // Sayfadaki metinleri işle
                            var pageWidth = page.Width;

                            // Kelimeleri Y koordinatına göre grupla (satırlar)
                            var lineGroups = pageWords
                                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1))
                                .OrderBy(g => g.Key)
                                .ToList();

                            // Satırları tersine çevir
                            lineGroups.Reverse();

                            // Her satır için
                            foreach (var line in lineGroups)
                            {
                                // Satırdaki kelimeleri soldan sağa sırala
                                var lineWords = line.OrderBy(w => w.BoundingBox.Left).ToList();

                                // Satırın başlangıç ve bitiş pozisyonlarını bul
                                var lineStart = lineWords.First().BoundingBox.Left;
                                var lineEnd = lineWords.Last().BoundingBox.Right;
                                var lineWidth = lineEnd - lineStart;
                                var totalPageWidth = pageWidth;

                                // İlk kelimeyi kontrol et - liste işareti veya numara ise
                                var firstWord = lineWords.First().Text;
                                bool isListItem = firstWord.EndsWith(".") || firstWord.EndsWith(")") || 
                                                firstWord.StartsWith("-") || firstWord.StartsWith("•");

                                // Yeni paragraf başlat
                                currentParagraph = new Paragraph(
                                    new ParagraphProperties(
                                        new SpacingBetweenLines { 
                                            Before = "0",
                                            After = "0",
                                            Line = "240",
                                            LineRule = LineSpacingRuleValues.Auto
                                        },
                                        new Indentation { 
                                            Left = isListItem ? "720" : "0",  // Liste öğeleri için girinti
                                            Right = "0", 
                                            FirstLine = "0" 
                                        }
                                    )
                                );
                                body.AppendChild(currentParagraph);

                                // Satırdaki kelimeleri işle
                                for (int i = 0; i < lineWords.Count; i++)
                                {
                                    var word = lineWords[i];
                                    var fontSize = Math.Max(1, (int)(word.BoundingBox.Height * 2.5));

                                    var run = new Run(
                                        new RunProperties(
                                            new RunFonts { 
                                                Ascii = word.FontName ?? "Times New Roman",
                                                HighAnsi = word.FontName ?? "Times New Roman",
                                                ComplexScript = word.FontName ?? "Times New Roman",
                                                EastAsia = word.FontName ?? "Times New Roman"
                                            },
                                            new FontSize { Val = fontSize.ToString() },
                                            new Bold { Val = word.FontName?.Contains("Bold") == true || word.FontName?.Contains("Heavy") == true },
                                            new Italic { Val = word.FontName?.Contains("Italic") == true || word.FontName?.Contains("Oblique") == true }
                                        )
                                    );

                                    // Önceki kelime ile arasındaki boşluğu hesapla
                                    if (i > 0)
                                    {
                                        var prevWord = lineWords[i - 1];
                                        var spaceWidth = word.BoundingBox.Left - prevWord.BoundingBox.Right;
                                        var wordCharWidth = (word.BoundingBox.Width / word.Text.Length + 
                                                              prevWord.BoundingBox.Width / prevWord.Text.Length) / 2;
                                        var spaceCount = Math.Max(1, (int)Math.Round(spaceWidth / (wordCharWidth * 0.5)));
                                        run.AppendChild(new Text(new string(' ', spaceCount)) { Space = SpaceProcessingModeValues.Preserve });
                                    }

                                    run.AppendChild(new Text(word.Text) { Space = SpaceProcessingModeValues.Preserve });
                                    currentParagraph.AppendChild(run);
                                }
                            }
                        }

                        mainPart.Document.Save();
                    }
                }

                _logger.LogInformation("Word belgesi oluşturuldu");

                // Oluşturulan Word belgesini gönder
                var fileBytes = await System.IO.File.ReadAllBytesAsync(docxPath);
                return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 
                    Path.GetFileName(docxPath));
            }
            catch (Exception ex)
            {
                _logger.LogError($"PDF dönüştürme hatası: {ex.Message}");
                return BadRequest($"Dönüştürme işlemi başarısız oldu: {ex.Message}");
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private string SafeFileName(string fileName)
        {
            var safeFileName = Regex.Replace(fileName, "[^a-zA-Z0-9-_.]", "");
            return safeFileName;
        }

        private bool IsValidImage(MemoryStream ms)
        {
            try
            {
                using var img = System.Drawing.Image.FromStream(ms);
                return img.Width > 0 && img.Height > 0 && img.RawFormat != null;
            }
            catch
            {
                return false;
            }
        }

        private string DetermineImageFormat(byte[] imageBytes)
        {
            if (imageBytes.Length < 4) return null;

            // JPEG formatı kontrolü
            if (imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF)
                return "image/jpeg";

            // PNG formatı kontrolü
            if (imageBytes[0] == 0x89 && imageBytes[1] == 0x50 && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
                return "image/png";

            // GIF formatı kontrolü
            if (imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
                return "image/gif";

            // BMP formatı kontrolü
            if (imageBytes[0] == 0x42 && imageBytes[1] == 0x4D)
                return "image/bmp";

            // TIFF formatı kontrolü (Intel)
            if (imageBytes[0] == 0x49 && imageBytes[1] == 0x49 && imageBytes[2] == 0x2A && imageBytes[3] == 0x00)
                return "image/tiff";

            // TIFF formatı kontrolü (Motorola)
            if (imageBytes[0] == 0x4D && imageBytes[1] == 0x4D && imageBytes[2] == 0x00 && imageBytes[3] == 0x2A)
                return "image/tiff";

            return null;
        }

        private byte[] ProcessImage(byte[] imageBytes, string imageFormat)
        {
            try
            {
                // Görsel verisi kontrolü
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    _logger.LogWarning("Boş görsel verisi");
                    return null;
                }

                // Sıkıştırılmış veri kontrolü
                if (IsCompressedData(imageBytes))
                {
                    _logger.LogInformation("Sıkıştırılmış veri tespit edildi, atlanıyor...");
                    return null;
                }

                // JPEG için özel işleme
                if (imageFormat == "image/jpeg")
                {
                    try
                    {
                        return ProcessJpegImage(imageBytes);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"JPEG işleme hatası: {ex.Message}");
                    }
                }

                // Diğer formatlar için genel işleme
                return ProcessGeneralImage(imageBytes, imageFormat);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Görsel işleme hatası: {ex.Message}");
                return null; // Hata durumunda null döndür
            }
        }

        private bool IsCompressedData(byte[] data)
        {
            // Zlib sıkıştırma başlığı kontrolü (78 9C)
            return data.Length >= 2 && data[0] == 0x78 && data[1] == 0x9C;
        }

        private byte[] DecompressData(byte[] compressedData, int width, int height)
        {
            try
            {
                byte[] decompressedData;

                // ZLIB sıkıştırmasını aç
                using (var compressedStream = new MemoryStream(compressedData))
                using (var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress))
                using (var resultStream = new MemoryStream())
                {
                    zlibStream.CopyTo(resultStream);
                    decompressedData = resultStream.ToArray();
                }

                _logger.LogInformation($"Veri başarıyla açıldı. Orijinal boyut: {compressedData.Length}, Açılmış boyut: {decompressedData.Length}");

                // Bitmap oluştur
                using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    PixelFormat.Format32bppArgb);

                try
                {
                    int stride = bitmapData.Stride;
                    byte[] pixels = new byte[stride * height];

                    // Her piksel için byte sayısını hesapla
                    int bytesPerPixel = 16; // Her piksel 16 byte
                    _logger.LogInformation($"Her piksel için byte sayısı: {bytesPerPixel}");

                    // İlk 32 byte'ı logla
                    var firstBytes = decompressedData.Take(32).ToArray();
                    _logger.LogInformation($"İlk 32 byte: {BitConverter.ToString(firstBytes)}");

                    // Her piksel için renk değerlerini ayarla
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int destIndex = y * stride + x * 4; // RGBA için 4 byte
                            int sourceIndex = (y * width + x) * bytesPerPixel;

                            if (sourceIndex + bytesPerPixel <= decompressedData.Length)
                            {
                                // 16 byte'lık veriyi analiz et
                                byte[] pixelData = new byte[bytesPerPixel];
                                Array.Copy(decompressedData, sourceIndex, pixelData, 0, bytesPerPixel);

                                // İlk 3 byte'ı kullan (03-02-03 deseni)
                                byte r = 0, g = 0, b = 0;

                                // Her 16 byte'lık bloğun ilk byte'ını kontrol et
                                bool isBlack = true;
                                for (int i = 0; i < bytesPerPixel; i += 3)
                                {
                                    if (i + 2 < bytesPerPixel)
                                    {
                                        if (pixelData[i] != 3 || pixelData[i + 1] != 2 || pixelData[i + 2] != 3)
                                        {
                                            isBlack = false;
                                            break;
                                        }
                                    }
                                }

                                // Renk değerlerini ayarla
                                if (isBlack)
                                {
                                    r = g = b = 0; // Siyah
                                }
                                else
                                {
                                    r = g = b = 255; // Beyaz
                                }

                                // RGBA değerlerini ayarla
                                pixels[destIndex + 3] = 255;  // Alpha (tam opak)
                                pixels[destIndex + 2] = r;    // Red
                                pixels[destIndex + 1] = g;    // Green
                                pixels[destIndex] = b;        // Blue
                            }
                        }
                    }

                    // Bitmap'e kopyala
                    System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }

                // Bitmap'i PNG olarak kaydet
                using var ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Veri açma ve dönüştürme hatası: {ex.Message}");
                return null;
            }
        }

        private int FindPatternLength(byte[] data)
        {
            // En az 16 byte'lık bir örnek al
            int sampleSize = Math.Min(data.Length, 64);
            byte[] sample = new byte[sampleSize];
            Array.Copy(data, sample, sampleSize);

            // 2'den 16'ya kadar olan desen uzunluklarını kontrol et
            for (int length = 2; length <= 16; length++)
            {
                bool isPattern = true;
                for (int i = length; i < sampleSize - length; i += length)
                {
                    bool patternMatches = true;
                    for (int j = 0; j < length; j++)
                    {
                        if (sample[j] != sample[i + j])
                        {
                            patternMatches = false;
                            break;
                        }
                    }
                    if (!patternMatches)
                    {
                        isPattern = false;
                        break;
                    }
                }
                if (isPattern)
                {
                    return length;
                }
            }
            return 16; // Varsayılan olarak 16 döndür
        }

        private byte CalculateColorValue(byte[] pattern, int colorIndex)
        {
            // Her renk için desendeki değerleri analiz et
            int sum = 0;
            int count = 0;
            
            for (int i = colorIndex; i < pattern.Length; i += 3)
            {
                if (pattern[i] > 0)
                {
                    sum += pattern[i];
                    count++;
                }
            }

            // Ortalama değeri hesapla ve renk değerine dönüştür
            if (count > 0)
            {
                return (byte)(sum / count);
            }
            return 0;
        }

        private byte[] ProcessJpegImage(byte[] imageBytes)
        {
            try
            {
                // JPEG başlık kontrolü ve düzeltme
                if (!IsValidJpegData(imageBytes))
                {
                    _logger.LogWarning("JPEG verisi düzeltiliyor...");
                    imageBytes = TryFixJpegData(imageBytes);
                    if (imageBytes == null)
                    {
                        _logger.LogWarning("JPEG verisi düzeltilemedi, alternatif yöntemler deneniyor...");
                        return TryAlternativeJpegProcessing(imageBytes);
                    }
                }

                // Güvenli yükleme denemesi
                byte[] processedBytes = null;
                Exception lastError = null;

                // 1. Deneme: Doğrudan yükleme
                try
                {
                    processedBytes = LoadJpegDirect(imageBytes);
                    if (processedBytes != null && processedBytes.Length > 0)
                        return processedBytes;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    _logger.LogDebug($"Doğrudan yükleme başarısız: {ex.Message}");
                }

                // 2. Deneme: Bellek optimizasyonu ile yükleme
                if (processedBytes == null)
                {
                    try
                    {
                        processedBytes = LoadJpegWithMemoryOptimization(imageBytes);
                        if (processedBytes != null && processedBytes.Length > 0)
                            return processedBytes;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        _logger.LogDebug($"Bellek optimizasyonlu yükleme başarısız: {ex.Message}");
                    }
                }

                // 3. Deneme: Piksel formatı değiştirerek yükleme
                if (processedBytes == null)
                {
                    try
                    {
                        processedBytes = LoadJpegWithDifferentPixelFormat(imageBytes);
                        if (processedBytes != null && processedBytes.Length > 0)
                            return processedBytes;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        _logger.LogDebug($"Piksel format değişikliği ile yükleme başarısız: {ex.Message}");
                    }
                }

                // Tüm denemeler başarısız olduysa alternatif yöntemleri dene
                if (processedBytes == null)
                {
                    _logger.LogWarning("Standart yöntemler başarısız oldu, alternatif yöntemler deneniyor...");
                    processedBytes = TryAlternativeJpegProcessing(imageBytes);
                    if (processedBytes != null && processedBytes.Length > 0)
                        return processedBytes;
                }

                if (processedBytes == null && lastError != null)
                {
                    throw lastError;
                }

                return processedBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError($"JPEG işleme hatası: {ex.Message}");
                // Son çare olarak alternatif yöntemleri dene
                return TryAlternativeJpegProcessing(imageBytes);
            }
        }

        private bool IsValidJpegData(byte[] data)
        {
            if (data == null || data.Length < 4) return false;

            // JPEG başlık kontrolü (FF D8 FF)
            if (data[0] != 0xFF || data[1] != 0xD8 || data[2] != 0xFF) return false;

            // JPEG bitiş işareti kontrolü (FF D9)
            if (data[data.Length - 2] != 0xFF || data[data.Length - 1] != 0xD9)
            {
                // Bitiş işareti eksik olabilir
                return false;
            }

            return true;
        }

        private byte[] TryFixJpegData(byte[] data)
        {
            if (data == null || data.Length < 4) return null;

            try
            {
                using var ms = new MemoryStream();

                // JPEG başlığı ekle
                if (data[0] != 0xFF || data[1] != 0xD8 || data[2] != 0xFF)
                {
                    ms.WriteByte(0xFF);
                    ms.WriteByte(0xD8);
                    ms.WriteByte(0xFF);
                    ms.WriteByte(0xE0);
                }

                // Veriyi yaz
                ms.Write(data, 0, data.Length);

                // JPEG bitiş işareti ekle
                if (data[data.Length - 2] != 0xFF || data[data.Length - 1] != 0xD9)
                {
                    ms.WriteByte(0xFF);
                    ms.WriteByte(0xD9);
                }

                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private byte[] LoadJpegDirect(byte[] imageBytes)
        {
            using var ms = new MemoryStream(imageBytes);
            using var img = Image.FromStream(ms, false, true);
            using var resultMs = new MemoryStream();

            var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var encoderParams = new EncoderParameters(1)
            {
                Param = { [0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L) }
            };

            img.Save(resultMs, jpegEncoder, encoderParams);
            return resultMs.ToArray();
        }

        private byte[] LoadJpegWithMemoryOptimization(byte[] imageBytes)
        {
            using var ms = new MemoryStream(imageBytes);
            using var img = Image.FromStream(ms, false, true);
            using var newBitmap = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb);
            
            using (var g = Graphics.FromImage(newBitmap))
            {
                g.Clear(System.Drawing.Color.White);
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using var wrapMode = new ImageAttributes();
                wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height));
            }

            using var resultMs = new MemoryStream();
            var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var encoderParams = new EncoderParameters(1)
            {
                Param = { [0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L) }
            };

            newBitmap.Save(resultMs, jpegEncoder, encoderParams);
            return resultMs.ToArray();
        }

        private byte[] LoadJpegWithDifferentPixelFormat(byte[] imageBytes)
        {
            using var ms = new MemoryStream(imageBytes);
            using var img = Image.FromStream(ms, false, true);
            
            // Farklı piksel formatlarını dene
            var pixelFormats = new[] 
            {
                PixelFormat.Format32bppArgb,
                PixelFormat.Format24bppRgb,
                PixelFormat.Format32bppRgb,
                PixelFormat.Format16bppRgb555
            };

            foreach (var format in pixelFormats)
            {
                try
                {
                    using var newBitmap = new Bitmap(img.Width, img.Height, format);
                    using var g = Graphics.FromImage(newBitmap);
                    g.Clear(System.Drawing.Color.White);
                    g.DrawImage(img, 0, 0, img.Width, img.Height);

                    using var resultMs = new MemoryStream();
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                        .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    var encoderParams = new EncoderParameters(1)
                    {
                        Param = { [0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 100L) }
                    };

                    newBitmap.Save(resultMs, jpegEncoder, encoderParams);
                    if (resultMs.Length > 0)
                        return resultMs.ToArray();
                }
                catch
                {
                    continue;
                }
            }

            return null;
        }

        private byte[] ProcessGeneralImage(byte[] imageBytes, string imageFormat)
        {
            try
            {
                using var ms = new MemoryStream(imageBytes);
                using var originalImage = Image.FromStream(ms);

                // Boyut kontrolü
                if (originalImage.Width < 1 || originalImage.Height < 1)
                {
                    _logger.LogWarning($"Geçersiz görsel boyutları: {originalImage.Width}x{originalImage.Height}");
                    return imageBytes;
                }

                // Yeni bir bitmap oluştur
                using var newBitmap = new Bitmap(originalImage.Width, originalImage.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(newBitmap))
                {
                    g.Clear(System.Drawing.Color.White);
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                    using var wrapMode = new ImageAttributes();
                    wrapMode.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                    g.DrawImage(originalImage,
                        new Rectangle(0, 0, newBitmap.Width, newBitmap.Height),
                        0, 0, originalImage.Width, originalImage.Height,
                        GraphicsUnit.Pixel,
                        wrapMode);
                }

                using var resultMs = new MemoryStream();
                if (imageFormat == "image/png" || string.IsNullOrEmpty(imageFormat))
                {
                    newBitmap.Save(resultMs, ImageFormat.Png);
                }
                else
                {
                    newBitmap.Save(resultMs, ImageFormat.Jpeg);
                }

                return resultMs.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Genel görsel işleme hatası: {ex.Message}");
                return imageBytes; // Hata durumunda orijinal veriyi döndür
            }
        }

        private string GetImagePartType(string imageFormat)
        {
            return imageFormat switch
            {
                "image/jpeg" => "image/jpeg",
                "image/png" => "image/png",
                "image/gif" => "image/png",
                "image/bmp" => "image/png",
                "image/tiff" => "image/png",
                _ => "image/png" // Varsayılan olarak PNG kullan
            };
        }

        private Drawing CreateImageElement(MainDocumentPart mainPart, ImagePart imagePart, long emuWidth, long emuHeight, uint imageId)
        {
            return new Drawing(
                new DW.Inline(
                    new DW.Extent() { Cx = emuWidth, Cy = emuHeight },
                    new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties() { Id = imageId, Name = $"Picture {imageId}" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks() { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties() { Id = imageId, Name = $"Picture {imageId}" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip() { Embed = mainPart.GetIdOfPart(imagePart) },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset() { X = 0L, Y = 0L },
                                        new A.Extents() { Cx = emuWidth, Cy = emuHeight }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                        ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
                )
            );
        }

        private string TryAllImageFormats(byte[] imageBytes)
        {
            // Denenecek tüm formatlar ve yöntemler
            var attempts = new List<Func<byte[], (bool success, string mimeType)>>
            {
                // Standart yöntem
                (bytes) => TryLoadImageStandard(bytes),
                // Bitmap yöntemi
                (bytes) => TryLoadImageAsBitmap(bytes),
                // PixelFormat değiştirerek deneme
                (bytes) => TryLoadImageWithDifferentPixelFormats(bytes),
                // Boyut küçülterek deneme
                (bytes) => TryLoadImageWithReducedSize(bytes),
                // Raw veri olarak deneme
                (bytes) => TryLoadImageAsRawData(bytes)
            };

            foreach (var attempt in attempts)
            {
                try
                {
                    var (success, mimeType) = attempt(imageBytes);
                    if (success)
                    {
                        _logger.LogInformation($"Görsel başarıyla yüklendi: {mimeType}");
                        return mimeType;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Görsel yükleme denemesi başarısız: {ex.Message}");
                }
            }

            return null;
        }

        private (bool success, string mimeType) TryLoadImageStandard(byte[] imageBytes)
        {
            var formats = new[] { ImageFormat.Jpeg, ImageFormat.Png, ImageFormat.Gif, ImageFormat.Bmp, ImageFormat.Tiff };
            
            using var ms = new MemoryStream(imageBytes);
            try
            {
                using var img = Image.FromStream(ms, false, true);
                foreach (var format in formats)
                {
                    try
                    {
                        using var testMs = new MemoryStream();
                        img.Save(testMs, format);
                        if (testMs.Length > 0)
                        {
                            return (true, $"image/{format.ToString().ToLower()}");
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
                // Standart yöntem başarısız oldu
            }
            
            return (false, null);
        }

        private (bool success, string mimeType) TryLoadImageAsBitmap(byte[] imageBytes)
        {
            try
            {
                using var ms = new MemoryStream(imageBytes);
                using var bitmap = new Bitmap(ms);
                using var testMs = new MemoryStream();
                
                // 32-bit ARGB formatında yeni bir bitmap oluştur
                using var newBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(newBitmap))
                {
                    g.Clear(System.Drawing.Color.White);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
                }
                
                newBitmap.Save(testMs, ImageFormat.Png);
                return testMs.Length > 0 ? (true, "image/png") : (false, null);
            }
            catch
            {
                return (false, null);
            }
        }

        private (bool success, string mimeType) TryLoadImageWithDifferentPixelFormats(byte[] imageBytes)
        {
            var pixelFormats = new[]
            {
                PixelFormat.Format24bppRgb,
                PixelFormat.Format32bppArgb,
                PixelFormat.Format32bppRgb,
                PixelFormat.Format16bppRgb555,
                PixelFormat.Format8bppIndexed
            };

            foreach (var format in pixelFormats)
            {
                try
                {
                    using var ms = new MemoryStream(imageBytes);
                    using var originalBitmap = new Bitmap(ms);
                    using var newBitmap = new Bitmap(originalBitmap.Width, originalBitmap.Height, format);
                    using var g = Graphics.FromImage(newBitmap);
                    
                    g.Clear(System.Drawing.Color.White);
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(originalBitmap, 0, 0, originalBitmap.Width, originalBitmap.Height);
                    
                    using var testMs = new MemoryStream();
                    newBitmap.Save(testMs, ImageFormat.Png);
                    
                    if (testMs.Length > 0)
                    {
                        return (true, "image/png");
                    }
                }
                catch
                {
                    continue;
                }
            }
            
            return (false, null);
        }

        private (bool success, string mimeType) TryLoadImageWithReducedSize(byte[] imageBytes)
        {
            try
            {
                using var ms = new MemoryStream(imageBytes);
                using var originalBitmap = new Bitmap(ms);
                
                // Boyutu yarıya indir
                int newWidth = Math.Max(1, originalBitmap.Width / 2);
                int newHeight = Math.Max(1, originalBitmap.Height / 2);
                
                using var newBitmap = new Bitmap(newWidth, newHeight, PixelFormat.Format24bppRgb);
                using var g = Graphics.FromImage(newBitmap);
                
                g.Clear(System.Drawing.Color.White);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(originalBitmap, 0, 0, newWidth, newHeight);
                
                using var testMs = new MemoryStream();
                newBitmap.Save(testMs, ImageFormat.Jpeg);
                
                return testMs.Length > 0 ? (true, "image/jpeg") : (false, null);
            }
            catch
            {
                return (false, null);
            }
        }

        private (bool success, string mimeType) TryLoadImageAsRawData(byte[] imageBytes)
        {
            try
            {
                // En basit haliyle raw veriyi bitmap'e dönüştürmeyi dene
                int width = (int)Math.Sqrt(imageBytes.Length / 3); // RGB için 3 byte
                int height = width;
                
                if (width > 0 && height > 0)
                {
                    using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                    var bitmapData = bitmap.LockBits(
                        new Rectangle(0, 0, width, height),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format24bppRgb);
                    
                    try
                    {
                        System.Runtime.InteropServices.Marshal.Copy(imageBytes, 0, bitmapData.Scan0, Math.Min(imageBytes.Length, width * height * 3));
                    }
                    finally
                    {
                        bitmap.UnlockBits(bitmapData);
                    }
                    
                    using var testMs = new MemoryStream();
                    bitmap.Save(testMs, ImageFormat.Jpeg);
                    
                    return testMs.Length > 0 ? (true, "image/jpeg") : (false, null);
                }
            }
            catch
            {
                // Raw veri denemesi başarısız
            }
            
            return (false, null);
        }

        private byte[] TryAlternativeJpegProcessing(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return null;

            try
            {
                // 1. Yöntem: Bitmap ile yeniden oluşturma
                using (var ms = new MemoryStream(imageBytes))
                {
                    try
                    {
                        using var bitmap = new Bitmap(1, 1);
                        bitmap.SetPixel(0, 0, System.Drawing.Color.White); // Test için geçerli bir bitmap
                        using var tempMs = new MemoryStream();
                        bitmap.Save(tempMs, ImageFormat.Jpeg);
                    }
                    catch
                    {
                        _logger.LogWarning("GDI+ desteklenmiyor, alternatif yöntem kullanılıyor...");
                        return TryProcessWithoutGDI(imageBytes);
                    }

                    // GDI+ destekleniyorsa devam et
                    using var img = Image.FromStream(ms, false, true);
                    if (img.Width < 1 || img.Height < 1)
                        return null;

                    using var newBitmap = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(newBitmap))
                    {
                        g.Clear(System.Drawing.Color.White);
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                        var destRect = new Rectangle(0, 0, img.Width, img.Height);
                        var srcRect = new Rectangle(0, 0, img.Width, img.Height);
                        g.DrawImage(img, destRect, srcRect, GraphicsUnit.Pixel);
                    }

                    using var resultMs = new MemoryStream();
                    var jpegEncoder = ImageCodecInfo.GetImageEncoders()
                        .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    var encoderParams = new EncoderParameters(1)
                    {
                        Param = { [0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 95L) }
                    };

                    newBitmap.Save(resultMs, jpegEncoder, encoderParams);
                    return resultMs.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Alternatif JPEG işleme başarısız: {ex.Message}");
                return TryProcessWithoutGDI(imageBytes);
            }
        }

        private byte[] TryProcessWithoutGDI(byte[] imageBytes)
        {
            try
            {
                // JPEG başlık ve bitiş işaretlerini kontrol et ve düzelt
                var processedBytes = new List<byte>();
                bool hasHeader = false;
                bool hasFooter = false;

                // JPEG başlığını kontrol et
                if (imageBytes.Length >= 2 && imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                {
                    hasHeader = true;
                }

                // JPEG bitiş işaretini kontrol et
                if (imageBytes.Length >= 2 && imageBytes[imageBytes.Length - 2] == 0xFF && imageBytes[imageBytes.Length - 1] == 0xD9)
                {
                    hasFooter = true;
                }

                // Başlık ekle
                if (!hasHeader)
                {
                    processedBytes.AddRange(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00 });
                }

                // Orijinal veriyi ekle
                processedBytes.AddRange(imageBytes);

                // Bitiş işareti ekle
                if (!hasFooter)
                {
                    processedBytes.AddRange(new byte[] { 0xFF, 0xD9 });
                }

                return processedBytes.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError($"GDI olmadan işleme başarısız: {ex.Message}");
                return null;
            }
        }
    }
}