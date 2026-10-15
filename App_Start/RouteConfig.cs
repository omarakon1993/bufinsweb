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

            // Observatorio financiero
            routes.MapRoute("ObsApi", "observatorio/api/{action}", new { controller = "Observatorio" });
            routes.MapRoute("ObsSectores", "observatorio/sectores", new { controller = "Observatorio", action = "Sectores" });
            routes.MapRoute("ObsComparar", "observatorio/comparar", new { controller = "Observatorio", action = "Comparador" });
            routes.MapRoute("ObsNodo", "observatorio/sector/{slug}", new { controller = "Observatorio", action = "Nodo" });
            routes.MapRoute("ObsEmpresa", "observatorio/empresa/{nit}", new { controller = "Observatorio", action = "Empresa" });
            routes.MapRoute("ObsIndex", "observatorio", new { controller = "Observatorio", action = "Index" });

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
