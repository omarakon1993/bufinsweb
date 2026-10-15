using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using bufinsweb.Services;

namespace bufinsweb.Controllers
{
    public class SeoController : Controller
    {
        public ActionResult Robots()
        {
            var baseUrl = BaseUrl();
            var txt = "User-agent: *\nAllow: /\nDisallow: /observatorio/api/\n\nSitemap: " + baseUrl + "/sitemap.xml\n";
            return Content(txt, "text/plain", Encoding.UTF8);
        }

        public ActionResult Sitemap()
        {
            var cached = HttpRuntime.Cache["seo:sitemap"] as string;
            if (cached == null)
            {
                var svc = new ObservatorioService();
                var d = svc.Datos;
                var b = BaseUrl();
                var fecha = d.Meta.FuenteActualizado;
                var sb = new StringBuilder();
                sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");
                Add(sb, b + "/", null, "1.0");
                Add(sb, b + "/Home/About", null, "0.6");
                Add(sb, b + "/Home/Contact", null, "0.6");
                Add(sb, b + "/observatorio", fecha, "0.9");
                Add(sb, b + "/observatorio/sectores", fecha, "0.8");
                Add(sb, b + "/observatorio/comparar", fecha, "0.7");
                foreach (var s in svc.SlugsSector()) Add(sb, b + "/observatorio/sector/" + s, fecha, "0.7");
                foreach (var e in d.Empresas.Where(x => x.En(d.MaxAnio, d.MinAnio) != null))
                    Add(sb, b + "/observatorio/empresa/" + e.Nit, fecha, "0.5");
                sb.Append("</urlset>");
                cached = sb.ToString();
                HttpRuntime.Cache.Insert("seo:sitemap", cached, null, System.DateTime.UtcNow.AddHours(12), System.Web.Caching.Cache.NoSlidingExpiration);
            }
            return Content(cached, "application/xml", Encoding.UTF8);
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
