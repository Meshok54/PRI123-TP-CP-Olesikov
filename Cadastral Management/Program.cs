using Cadastral_Management.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Настройка гибридной локализации
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    // Создал кастомную культуру на основе русской, но с точкой как десятичным разделителем
    var customCulture = new CultureInfo("ru-RU");
    customCulture.NumberFormat.NumberDecimalSeparator = ".";
    customCulture.NumberFormat.CurrencyDecimalSeparator = ".";
    customCulture.NumberFormat.PercentDecimalSeparator = ".";

    var supportedCultures = new[]
    {
        customCulture,  // кастомная русская культура с точкой
        new CultureInfo("en-US")
    };

    options.DefaultRequestCulture = new RequestCulture(customCulture);
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Добавляю провайдеры
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(),
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

// Добавляю сервис локализации
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlServerVersion(new Version(8, 0, 0))
    ));

builder.Services.AddControllersWithViews()
    .AddViewLocalization()  // Для локализации представлений
    .AddDataAnnotationsLocalization();  // Для локализации атрибутов валидации

builder.Services.AddSession();

var app = builder.Build();

// Включаю локализацию
app.UseRequestLocalization();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();