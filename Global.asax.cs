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

            bufinsweb.Services.UsoService.Iniciar();

            // Carga los datos de Pulso Bufins en un hilo de fondo: la primera visita ya los encuentra en memoria
            Task.Run(() =>
            {
                try { bufinsweb.Services.PulsoService.Precargar(); }
                catch (Exception) { /* si faltan los archivos, el error aparecerá al visitar /pulso-bufins */ }
            });
        }

        protected void Application_End()
        {
            bufinsweb.Services.UsoService.Vaciar();
        }

        // Robots bloqueados y límite por IP: cortan la petición antes de generar la página (ahorro de tráfico)
        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            bufinsweb.Filters.BotGuard.Filtrar((HttpApplication)sender);
        }

        // gzip desde la aplicación: el IIS del hosting no comprime
        protected void Application_PostReleaseRequestState(object sender, EventArgs e)
        {
            bufinsweb.Filters.Compresion.Aplicar((HttpApplication)sender);
        }

        // Respaldo fuera de MVC: peticiones inválidas de robots -> 400 sin cuerpo (no 500 ni redirección a la página de error)
        protected void Application_Error(object sender, EventArgs e)
        {
            if (!bufinsweb.Filters.ManejoErroresAttribute.EsSolicitudInvalida(Server.GetLastError())) return;
            Server.ClearError();
            Response.Clear();
            Response.StatusCode = 400;
            Response.TrySkipIisCustomErrors = true;
            Response.SuppressContent = true;
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
