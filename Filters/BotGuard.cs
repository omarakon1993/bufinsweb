using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Caching;

namespace bufinsweb.Filters
{
    /// <summary>
    /// Protección del tráfico del hosting (10 GB/mes). Se ejecuta al inicio de cada petición (Global.asax):
    /// 1. Robots bloqueados (IA de entrenamiento, herramientas SEO, rastreadores sin valor): 403 sin cuerpo,
    ///    aunque ignoren robots.txt. robots.txt sí se les sirve para que dejen de intentarlo.
    /// 2. Límite por IP para páginas (no archivos estáticos): por minuto y por hora; responde 429 sin cuerpo.
    ///    Google y Bing quedan por fuera del límite.
    /// En octubre 2026 GPTBot, ShapBot, AhrefsBot, QlyzeBot y MJ12bot hicieron ~92 % de las peticiones.
    /// </summary>
    public static class BotGuard
    {
        /// <summary>Fragmentos de User-Agent bloqueados (mantener en sintonía con SeoController.RobotsBloqueados).</summary>
        public static readonly string[] Bloqueados =
        {
            "GPTBot", "ClaudeBot", "anthropic-ai", "CCBot", "Bytespider", "Amazonbot", "meta-externalagent", "FacebookBot",
            "cohere-ai", "Diffbot", "ImagesiftBot", "Omgilibot", "Timpibot", "PetalBot", "AhrefsBot", "SemrushBot", "MJ12bot",
            "DotBot", "DataForSeoBot", "BLEXBot", "barkrowler", "ShapBot", "QlyzeBot", "SeekportBot", "serpstatbot", "Scrapy",
            "python-requests", "Go-http-client", "okhttp", "HeadlessChrome"
        };

        private static readonly Regex RxBloqueados = new Regex(string.Join("|", Array.ConvertAll(Bloqueados, Regex.Escape)), RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RxBuscadores = new Regex("Googlebot|Google-InspectionTool|bingbot|AdsBot-Google", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private const int MaxPorMinuto = 60;
        private const int MaxPorHora = 600;

        private class Contador { public int N; }

        /// <summary>true si la petición se cortó (403/429) y no debe seguir.</summary>
        public static bool Filtrar(HttpApplication app)
        {
            var req = app.Request;
            var ruta = req.Path ?? "";
            if (ruta.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase)) return false;

            var ua = req.UserAgent ?? "";
            if (ua.Length > 0 && RxBloqueados.IsMatch(ua))
            {
                bufinsweb.Services.UsoService.ContarBot(ua, "Bloqueado");
                return Cortar(app, 403);
            }

            // solo páginas: los estáticos y la API ya tienen caché/límites propios
            if (ruta.StartsWith("/Assets/", StringComparison.OrdinalIgnoreCase) || ruta.StartsWith("/bundles/", StringComparison.OrdinalIgnoreCase)
                || ruta.StartsWith("/observatorio/api/", StringComparison.OrdinalIgnoreCase) || RxBuscadores.IsMatch(ua))
                return false;

            var ip = req.UserHostAddress ?? "desconocida";
            if (Excede("bg:m:" + ip, MaxPorMinuto, 60) | Excede("bg:h:" + ip, MaxPorHora, 3600))
            {
                bufinsweb.Services.UsoService.Contar("throttle_429", "paginas");
                app.Response.AppendHeader("Retry-After", "600");
                return Cortar(app, 429);
            }
            return false;
        }

        private static bool Excede(string clave, int max, int segundos)
        {
            var cache = HttpRuntime.Cache;
            var c = cache[clave] as Contador;
            if (c == null)
            {
                var nuevo = new Contador();
                cache.Add(clave, nuevo, null, DateTime.UtcNow.AddSeconds(segundos), Cache.NoSlidingExpiration, CacheItemPriority.Low, null);
                c = cache[clave] as Contador ?? nuevo;
            }
            return Interlocked.Increment(ref c.N) > max;
        }

        private static bool Cortar(HttpApplication app, int codigo)
        {
            var res = app.Response;
            res.StatusCode = codigo;
            res.TrySkipIisCustomErrors = true;   // sin página de error: 0 bytes de cuerpo
            res.SuppressContent = true;
            app.CompleteRequest();
            return true;
        }
    }
}
