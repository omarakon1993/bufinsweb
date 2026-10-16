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

            // Observatorio financiero
            routes.MapRoute("ObsApi", "observatorio/api/{action}", new { controller = "Observatorio" });
            routes.MapRoute("ObsSectores", "observatorio/sectores", new { controller = "Observatorio", action = "Sectores" });
            routes.MapRoute("ObsComparar", "observatorio/comparar", new { controller = "Observatorio", action = "Comparador" });
            routes.MapRoute("ObsNodo", "observatorio/sector/{slug}", new { controller = "Observatorio", action = "Nodo" });
            routes.MapRoute("ObsEmpresa", "observatorio/empresa/{id}", new { controller = "Observatorio", action = "Empresa" });
            routes.MapRoute("ObsInforme", "observatorio/informe/{id}", new { controller = "Observatorio", action = "Informe" });
            routes.MapRoute("ObsOg", "observatorio/og/{id}", new { controller = "Observatorio", action = "Og" });
            routes.MapRoute("ObsRankingHub", "observatorio/ranking", new { controller = "Observatorio", action = "Ranking" });
            routes.MapRoute("ObsRankingAmbito", "observatorio/ranking/{metrica}/{tipo}/{clave}", new { controller = "Observatorio", action = "Ranking" });
            routes.MapRoute("ObsRanking", "observatorio/ranking/{metrica}", new { controller = "Observatorio", action = "Ranking" });
            routes.MapRoute("ObsIndex", "observatorio", new { controller = "Observatorio", action = "Index" });

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
