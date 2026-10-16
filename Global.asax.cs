using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace bufinsweb
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Carga los datos del Observatorio en un hilo de fondo: la primera visita ya los encuentra en memoria
            Task.Run(() =>
            {
                try { bufinsweb.Services.ObservatorioService.Precargar(); }
                catch (Exception) { /* si faltan los archivos, el error aparecerá al visitar /observatorio */ }
            });
        }

        protected void Application_AcquireRequestState(object sender, EventArgs e)
        {
            string lang = "es-CO";
            var cookie = Request.Cookies["lang"];
            if (cookie != null && (cookie.Value == "es-CO" || cookie.Value == "en-US"))
                lang = cookie.Value;

            var culture = new CultureInfo(lang);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}
