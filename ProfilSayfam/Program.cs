using ProfilSayfam.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ProfilSayfam.Data;
using ProfilSayfam.Middleware;

var builder = WebApplication.CreateBuilder(args);

// SQL Server bağlantısını ekle
var connectionString = builder.Environment.IsDevelopment() 
    ? builder.Configuration.GetConnectionString("DefaultConnection")
    : builder.Configuration.GetConnectionString("ProductionConnection");

builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseSqlServer(connectionString));

// Analytics servislerini ekle
builder.Services.AddSingleton<IGeoLocationService, GeoLocationService>();
builder.Services.AddScoped<IAnalyticsReportService, AnalyticsReportService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<AnalyticsDbContext>();

// Email servisini ekle
builder.Services.AddTransient<IEmailSender, EmailSender>();

// Lokalizasyon için desteklenen kültürleri ve varsayılan kültürü ayarla
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { "tr-TR", "en-US" };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new List<CultureInfo>();
    foreach (var culture in supportedCultures)
    {
        cultures.Add(new CultureInfo(culture));
    }

    options.DefaultRequestCulture = new RequestCulture("tr-TR");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
});

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options => {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(ProfilSayfam.SharedResource));
    });

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Admin/Logout";
        options.AccessDeniedPath = "/Admin/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
    });

// Configure URLs and Kestrel
builder.WebHost.UseUrls("http://*:8080");
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(8080);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Lokalizasyon middleware'ini ekle
var localizationOptions = app.Services.GetService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Analytics middleware'ini ekle
app.UseAnalytics();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
