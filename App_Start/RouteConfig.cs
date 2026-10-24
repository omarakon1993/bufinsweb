using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace bufinsweb
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // SEO
            routes.MapRoute("Robots", "robots.txt", new { controller = "Seo", action = "Robots" });
            routes.MapRoute("Sitemap", "sitemap.xml", new { controller = "Seo", action = "Sitemap" });
            routes.MapRoute("SitemapParte", "{archivo}.xml", new { controller = "Seo", action = "SitemapParte" }, new { archivo = @"sitemap-[a-z0-9-]+" });

            // Pulso financiero
            routes.MapRoute("ObsApi", "pulso-bufins/api/{action}", new { controller = "Pulso" });
            routes.MapRoute("ObsSectores", "pulso-bufins/sectores", new { controller = "Pulso", action = "Sectores" });
            routes.MapRoute("ObsComparar", "pulso-bufins/comparar", new { controller = "Pulso", action = "Comparador" });
            routes.MapRoute("ObsNodo", "pulso-bufins/sector/{slug}", new { controller = "Pulso", action = "Nodo" });
            routes.MapRoute("ObsEmpresa", "pulso-bufins/empresa/{id}", new { controller = "Pulso", action = "Empresa" });
            routes.MapRoute("ObsInforme", "pulso-bufins/informe/{id}", new { controller = "Pulso", action = "Informe" });
            routes.MapRoute("ObsOg", "pulso-bufins/og/{id}", new { controller = "Pulso", action = "Og" });
            routes.MapRoute("ObsRankingHub", "pulso-bufins/ranking", new { controller = "Pulso", action = "Ranking" });
            routes.MapRoute("ObsRankingAmbito", "pulso-bufins/ranking/{metrica}/{tipo}/{clave}", new { controller = "Pulso", action = "Ranking" });
            routes.MapRoute("ObsRanking", "pulso-bufins/ranking/{metrica}", new { controller = "Pulso", action = "Ranking" });
            routes.MapRoute("ObsIndex", "pulso-bufins", new { controller = "Pulso", action = "Index" });
            // URLs anteriores del modulo: 301 hacia la direccion actual (conserva SEO y enlaces compartidos)
            routes.MapRoute("ObsAntigua", "observatorio/{*resto}", new { controller = "Pulso", action = "Antigua" });
            routes.MapRoute("ObsAntiguaRaiz", "observatorio", new { controller = "Pulso", action = "Antigua", resto = "" });

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
