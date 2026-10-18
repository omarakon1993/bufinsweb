using System;
using System.Threading;
using System.Web;
using System.Web.Caching;
using System.Web.Mvc;

namespace bufinsweb.Filters
{
    /// <summary>
    /// Límite de peticiones por IP en ventana fija (por defecto N por minuto) para los endpoints públicos costosos.
    /// Solo cuenta las peticiones que llegan a la acción: las respuestas servidas desde la caché de salida no pasan por aquí.
    /// Responde 429 con Retry-After al excederlo.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class ThrottleAttribute : ActionFilterAttribute
    {
        private class Contador { public int N; }

        /// <summary>Nombre del grupo de límite (cada endpoint puede tener el suyo).</summary>
        public string Grupo { get; set; }
        /// <summary>Peticiones permitidas por ventana.</summary>
        public int Max { get; set; }
        /// <summary>Duración de la ventana en segundos.</summary>
        public int Segundos { get; set; }

        public ThrottleAttribute() { Max = 60; Segundos = 60; }

        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            var ip = ctx.HttpContext.Request.UserHostAddress ?? "desconocida";
            var clave = "thr:" + (Grupo ?? ctx.ActionDescriptor.ActionName) + ":" + ip;
            var cache = HttpRuntime.Cache;
            var c = cache[clave] as Contador;
            if (c == null)
            {
                var nuevo = new Contador();
                cache.Add(clave, nuevo, null, DateTime.UtcNow.AddSeconds(Segundos), Cache.NoSlidingExpiration, CacheItemPriority.Low, null);
                c = cache[clave] as Contador ?? nuevo;
            }
            if (Interlocked.Increment(ref c.N) > Max)
            {
                bufinsweb.Services.UsoService.Contar("throttle_429", (Grupo ?? ctx.ActionDescriptor.ActionName).Replace(' ', '-'));
                ctx.HttpContext.Response.AppendHeader("Retry-After", Segundos.ToString());
                ctx.Result = new HttpStatusCodeResult(429, "Demasiadas solicitudes. Intente de nuevo en un momento.");
            }
        }
    }
}
