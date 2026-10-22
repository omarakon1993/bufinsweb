using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using bufinsweb.Models.Observatorio;
using bufinsweb.Services;

namespace bufinsweb.Controllers
{
    public class SeoController : Controller
    {
        private const int PorArchivo = 10000;   // empresas por archivo de sitemap (el límite del protocolo es 50.000)

        // Robots de IA (entrenamiento) y de herramientas SEO: consumen mucho trafico y no traen visitas.
        // Son los mismos que BotGuard corta en el servidor (403), mas los "tokens" de IA de Google y Apple.
        private static readonly string[] RobotsBloqueados =
            new[] { "Google-Extended", "Applebot-Extended" }.Concat(bufinsweb.Filters.BotGuard.Bloqueados).ToArray();

        // Previsualizadores de enlaces (WhatsApp, redes sociales): necesitan la imagen /observatorio/og/ al compartir.
        private static readonly string[] Previsualizadores =
        {
            "facebookexternalhit", "WhatsApp", "LinkedInBot", "Twitterbot", "Slackbot", "TelegramBot"
        };

        public ActionResult Robots()
        {
            var baseUrl = BaseUrl();
            var sb = new StringBuilder();
            foreach (var ua in RobotsBloqueados) sb.Append("User-agent: ").Append(ua).Append('\n');
            sb.Append("Disallow: /\n\n");
            foreach (var ua in Previsualizadores) sb.Append("User-agent: ").Append(ua).Append('\n');
            sb.Append("Disallow: /observatorio/api/\nDisallow: /observatorio/informe/\n\n");
            // Buscadores: fichas, sectores y rankings nacionales/departamento/seccion; sin imagenes OG,
            // ni rankings por ciudad o clase CIIU (miles de combinaciones), ni comparaciones con parametros.
            sb.Append("User-agent: *\nAllow: /\n")
              .Append("Disallow: /Home/SetLanguage\n")   // cambio de idioma: redirige (302) a la misma pagina
              .Append("Disallow: /observatorio/api/\nDisallow: /observatorio/informe/\nDisallow: /observatorio/og/\n")
              .Append("Disallow: /observatorio/comparar?\n")
              .Append("Disallow: /observatorio/ranking/*/ciudad/\nDisallow: /observatorio/ranking/*/sector/ciiu-\n")
              .Append("Crawl-delay: 10\n\n");
            sb.Append("Sitemap: ").Append(baseUrl).Append("/sitemap.xml\n");
            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }

        /// <summary>Índice de sitemaps: páginas, sectores, rankings y empresas (en trozos de 10.000).</summary>
        public ActionResult Sitemap()
        {
            var svc = new ObservatorioService();
            var d = svc.Datos;
            var b = BaseUrl();
            var lm = Fecha(d);
            var n = (int)Math.Ceiling(d.Empresas.Count(x => x.En(d.MaxAnio, d.MinAnio) != null) / (double)PorArchivo);
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
            Parte(sb, b + "/sitemap-paginas.xml", lm);
            Parte(sb, b + "/sitemap-sectores.xml", lm);
            Parte(sb, b + "/sitemap-rankings.xml", lm);
            for (var i = 1; i <= n; i++) Parte(sb, b + "/sitemap-empresas-" + i + ".xml", lm);
            sb.Append("</sitemapindex>");
            return Xml(sb.ToString());
        }

        public ActionResult SitemapParte(string archivo)
        {
            var svc = new ObservatorioService();
            var d = svc.Datos;
            var b = BaseUrl();
            var lm = Fecha(d);
            var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
            if (archivo == "sitemap-paginas")
            {
                Add(sb, b + "/", null, "1.0");
                Add(sb, b + "/Home/About", null, "0.6");
                Add(sb, b + "/Home/Contact", null, "0.6");
                Add(sb, b + "/observatorio", lm, "0.9");
                Add(sb, b + "/observatorio/sectores", lm, "0.8");
                Add(sb, b + "/observatorio/comparar", lm, "0.7");
                Add(sb, b + "/observatorio/ranking", lm, "0.8");
            }
            else if (archivo == "sitemap-sectores")
            {
                foreach (var s in svc.SlugsSector()) Add(sb, b + "/observatorio/sector/" + s, lm, "0.7");
            }
            else if (archivo == "sitemap-rankings")
            {
                foreach (var m in ObservatorioService.Rankings)
                    foreach (var r in svc.RutasRanking())
                        Add(sb, b + "/observatorio/ranking/" + m.Clave + (r == "nacional" ? "" : "/" + r), lm, r == "nacional" ? "0.8" : "0.6");
            }
            else if (archivo.StartsWith("sitemap-empresas-", StringComparison.Ordinal))
            {
                int i;
                if (!int.TryParse(archivo.Substring(17), out i) || i < 1) return HttpNotFound();
                var lista = d.Empresas.Where(x => x.En(d.MaxAnio, d.MinAnio) != null).Skip((i - 1) * PorArchivo).Take(PorArchivo).ToList();
                if (lista.Count == 0) return HttpNotFound();
                foreach (var e in lista) Add(sb, b + ObservatorioService.UrlEmpresa(e), lm, "0.5");
            }
            else return HttpNotFound();
            sb.Append("</urlset>");
            return Xml(sb.ToString());
        }

        private ActionResult Xml(string xml)
        {
            Response.Cache.SetCacheability(HttpCacheability.Public);
            Response.Cache.SetMaxAge(TimeSpan.FromHours(6));
            // text/xml (valido para sitemaps): IIS lo comprime con gzip; application/xml sale sin comprimir (1,5 MB por archivo)
            return Content(xml, "text/xml", Encoding.UTF8);
        }

        private static string Fecha(ObservatorioDatos d)
        {
            DateTime t;
            return DateTime.TryParse(d.Meta.FuenteActualizado, out t) ? t.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd");
        }

        private static void Parte(StringBuilder sb, string loc, string lastmod)
        {
            sb.Append("<sitemap><loc>").Append(HttpUtility.HtmlEncode(loc)).Append("</loc><lastmod>").Append(lastmod).Append("</lastmod></sitemap>\n");
        }

        private static void Add(StringBuilder sb, string loc, string lastmod, string prio)
        {
            sb.Append("<url><loc>").Append(HttpUtility.HtmlEncode(loc)).Append("</loc>");
            if (lastmod != null) sb.Append("<lastmod>").Append(lastmod).Append("</lastmod>");
            sb.Append("<priority>").Append(prio).Append("</priority></url>\n");
        }

        private string BaseUrl()
        {
            return Request.Url.GetLeftPart(System.UriPartial.Authority);
        }
    }
}
