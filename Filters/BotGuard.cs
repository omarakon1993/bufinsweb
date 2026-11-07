using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace bufinsweb.Filters
{
    /// <summary>
    /// Protección del tráfico del hosting (10 GB/mes). Middleware que corre al inicio de cada petición:
    /// 1. Robots bloqueados (IA de entrenamiento, herramientas SEO, rastreadores sin valor): 403 sin cuerpo,
    ///    aunque ignoren robots.txt. robots.txt sí se les sirve para que dejen de intentarlo.
    /// 2. Límite por IP para páginas (no archivos estáticos): por minuto y por hora; responde 429 sin cuerpo.
    ///    Google y Bing quedan por fuera del límite.
    /// En octubre 2026 GPTBot, ShapBot, AhrefsBot, QlyzeBot y MJ12bot hicieron ~92 % de las peticiones.
    /// </summary>
    public class BotGuard
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

        private readonly RequestDelegate _next;
        public BotGuard(RequestDelegate next) { _next = next; }

        public Task InvokeAsync(HttpContext ctx)
        {
            var codigo = Filtrar(ctx);
            if (codigo == 0) return _next(ctx);
            ctx.Response.StatusCode = codigo;   // sin cuerpo: 0 bytes
            return Task.CompletedTask;
        }

        /// <summary>0 si la petición sigue; 403/429 si se corta.</summary>
        private static int Filtrar(HttpContext ctx)
        {
            var req = ctx.Request;
            var ruta = req.Path.Value ?? "";
            if (ruta.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase)) return 0;
            var ua = req.UserAgentTexto();
            if (ua.Length > 0 && RxBloqueados.IsMatch(ua))
            {
                bufinsweb.Services.UsoService.ContarBot(ua, "Bloqueado");
                return 403;
            }
            // solo páginas: los estáticos y la API ya tienen caché/límites propios
            if (ruta.StartsWith("/Assets/", StringComparison.OrdinalIgnoreCase) || ruta.StartsWith("/bundles/", StringComparison.OrdinalIgnoreCase)
                || ruta.StartsWith("/pulso-bufins/api/", StringComparison.OrdinalIgnoreCase) || RxBuscadores.IsMatch(ua))
                return 0;
            var ip = ctx.Ip();
            if ((VentanaFija.Incrementar("bg:m:" + ip, 60) > MaxPorMinuto) | (VentanaFija.Incrementar("bg:h:" + ip, 3600) > MaxPorHora))
            {
                bufinsweb.Services.UsoService.Contar("throttle_429", "paginas");
                ctx.Response.Headers["Retry-After"] = "600";
                return 429;
            }
            return 0;
        }
    }
}
