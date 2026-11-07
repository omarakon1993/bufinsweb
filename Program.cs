using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using bufinsweb;
using bufinsweb.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using bufinsweb.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Credenciales locales (SMTP): secretsSettings.json estÃ¡ en .gitignore. En producciÃ³n se usan variables de entorno.
builder.Configuration.AddJsonFile("secretsSettings.json", optional: true, reloadOnChange: true);

AppPaths.Iniciar(builder.Environment.ContentRootPath);
EnvironmentHelper.Iniciar(builder.Configuration);
Bundles.Configurar(builder.Environment);

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);   // no anunciar el servidor (solo aplica a Kestrel; en IIS lo quita web.config)

builder.Services.AddControllersWithViews();
builder.Services.AddHostedService<InicioEnSegundoPlano>();

// El IIS del hosting no comprime nada: la aplicaciÃ³n comprime todo lo que sea texto (pÃ¡ginas, API, sitemaps, bundles).
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "text/json", "text/csv", "image/svg+xml" });
});

// CachÃ© de salida de la API pÃºblica y de la imagen para compartir (equivale a [OutputCache] de MVC 5).
// Se separa por idioma porque el texto de algunas respuestas depende de la cookie "lang".
builder.Services.AddOutputCache(o =>
{
    Action<string, int, string[]> politica = (nombre, segundos, consulta) => o.AddPolicy(nombre, b =>
    {
        b.Expire(TimeSpan.FromSeconds(segundos));
        b.SetVaryByQuery(consulta);
        b.SetVaryByRouteValue("id");
        b.VaryByValue(ctx => new KeyValuePair<string, string>("lang", CultureInfo.CurrentUICulture.Name));
    });
    politica("opciones", 600, new[] { "v" });
    politica("buscar", 300, new[] { "q", "n", "np", "v" });
    politica("analisis", 300, new[] { "*" });
    politica("ficha", 300, new[] { "nit", "v" });
    politica("series", 300, new[] { "nits", "v" });
    politica("og", 86400, new[] { "v" });
});

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.UseDeveloperExceptionPage();
else app.UseExceptionHandler("/Home/Error");

app.UseResponseCompression();
app.UseMiddleware<CabecerasSeguridad>();
app.UseMiddleware<BotGuard>();
app.UsarBundles();

// EstÃ¡ticos: viven en /Assets (no en wwwroot). Fuentes e imÃ¡genes casi no cambian: cachÃ© del navegador de 30 dÃ­as.
var assets = Path.Combine(app.Environment.ContentRootPath, "Assets");
if (Directory.Exists(assets))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(assets),
        RequestPath = "/Assets",
        OnPrepareResponse = ctx =>
        {
            var ruta = ctx.Context.Request.Path.Value ?? "";
            if (ruta.StartsWith("/Assets/fonts/", StringComparison.OrdinalIgnoreCase) || ruta.StartsWith("/Assets/webfonts/", StringComparison.OrdinalIgnoreCase)
                || ruta.StartsWith("/Assets/images/", StringComparison.OrdinalIgnoreCase))
                ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=2592000";
        }
    });
}
foreach (var icono in new[] { "favicon.ico", "Bufins_Favicon_Ink.ico" })
{
    var fisico = Path.Combine(app.Environment.ContentRootPath, icono);
    app.MapGet("/" + icono, (HttpContext ctx) =>
    {
        ctx.Response.Headers["Cache-Control"] = "public, max-age=2592000";
        return Results.File(fisico, "image/x-icon");
    });
}

// Idioma: cookie "lang" con "es-CO" o "en-US" (la escribe Home/SetLanguage). Por defecto es-CO; no se usa Accept-Language.
var idiomas = new[] { new CultureInfo("es-CO"), new CultureInfo("en-US") };
var localizacion = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("es-CO"),
    SupportedCultures = idiomas,
    SupportedUICultures = idiomas
};
localizacion.RequestCultureProviders.Clear();
localizacion.RequestCultureProviders.Add(new CustomRequestCultureProvider(ctx =>
{
    var v = ctx.Request.Cookies["lang"];
    return Task.FromResult(v == "es-CO" || v == "en-US" ? new ProviderCultureResult(v) : null);
}));
app.UseRequestLocalization(localizacion);

app.UseRouting();
app.UseOutputCache();

// SEO
app.MapControllerRoute("Robots", "robots.txt", new { controller = "Seo", action = "Robots" });
app.MapControllerRoute("Sitemap", "sitemap.xml", new { controller = "Seo", action = "Sitemap" });
app.MapControllerRoute("SitemapParte", "{archivo}.xml", new { controller = "Seo", action = "SitemapParte" }, new { archivo = @"sitemap-[a-z0-9-]+" });

// Pulso financiero
app.MapControllerRoute("ObsApi", "pulso-bufins/api/{action}", new { controller = "Pulso" });
app.MapControllerRoute("ObsSectores", "pulso-bufins/sectores", new { controller = "Pulso", action = "Sectores" });
app.MapControllerRoute("ObsComparar", "pulso-bufins/comparar", new { controller = "Pulso", action = "Comparador" });
app.MapControllerRoute("ObsNodo", "pulso-bufins/sector/{slug}", new { controller = "Pulso", action = "Nodo" });
app.MapControllerRoute("ObsEmpresa", "pulso-bufins/empresa/{id}", new { controller = "Pulso", action = "Empresa" });
app.MapControllerRoute("ObsInforme", "pulso-bufins/informe/{id}", new { controller = "Pulso", action = "Informe" });
app.MapControllerRoute("ObsOg", "pulso-bufins/og/{id}", new { controller = "Pulso", action = "Og" });
app.MapControllerRoute("ObsRankingHub", "pulso-bufins/ranking", new { controller = "Pulso", action = "Ranking" });
app.MapControllerRoute("ObsRankingAmbito", "pulso-bufins/ranking/{metrica}/{tipo}/{clave}", new { controller = "Pulso", action = "Ranking" });
app.MapControllerRoute("ObsRanking", "pulso-bufins/ranking/{metrica}", new { controller = "Pulso", action = "Ranking" });
app.MapControllerRoute("ObsIndex", "pulso-bufins", new { controller = "Pulso", action = "Index" });
// URLs anteriores del mÃ³dulo: 301 hacia la direcciÃ³n actual (conserva SEO y enlaces compartidos)
app.MapControllerRoute("ObsAntigua", "observatorio/{*resto}", new { controller = "Pulso", action = "Antigua" });
app.MapControllerRoute("ObsAntiguaRaiz", "observatorio", new { controller = "Pulso", action = "Antigua", resto = "" });

app.MapControllerRoute("Default", "{controller=Home}/{action=Index}/{id?}");

app.Run();

