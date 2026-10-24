using System.Collections.Generic;
using System.Linq;
using System.Web;
using bufinsweb.Services;
using Newtonsoft.Json.Linq;

namespace bufinsweb.Helpers
{
    /// <summary>Datos estructurados (JSON-LD) de Pulso Bufins: FAQPage y BreadcrumbList.</summary>
    public static class PulsoSeo
    {
        private static IHtmlString Script(JObject o)
        {
            // "</" dentro de un bloque script cerraría la etiqueta antes de tiempo
            var json = o.ToString(Newtonsoft.Json.Formatting.None).Replace("</", "<\\/");
            return new HtmlString("<script type=\"application/ld+json\">" + json + "</script>");
        }

        public static IHtmlString Faq(PulsoService.Narrativa n)
        {
            if (n == null || n.Faqs.Count == 0) return new HtmlString("");
            return Script(new JObject
            {
                { "@context", "https://schema.org" }, { "@type", "FAQPage" },
                { "mainEntity", new JArray(n.Faqs.Select(f => new JObject
                    { { "@type", "Question" }, { "name", f.P }, { "acceptedAnswer", new JObject { { "@type", "Answer" }, { "text", f.R } } } })) }
            });
        }

        /// <summary>Migas de pan: pares (nombre, URL absoluta).</summary>
        public static IHtmlString Breadcrumb(IEnumerable<KeyValuePair<string, string>> items)
        {
            var i = 0;
            return Script(new JObject
            {
                { "@context", "https://schema.org" }, { "@type", "BreadcrumbList" },
                { "itemListElement", new JArray(items.Select(x => new JObject
                    { { "@type", "ListItem" }, { "position", ++i }, { "name", x.Key }, { "item", x.Value } })) }
            });
        }

        public static IHtmlString Json(JObject o) { return Script(o); }
    }
}
