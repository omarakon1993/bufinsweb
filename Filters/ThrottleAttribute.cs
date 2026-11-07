using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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
        /// <summary>Nombre del grupo de límite (cada endpoint puede tener el suyo).</summary>
        public string Grupo { get; set; }
        /// <summary>Peticiones permitidas por ventana.</summary>
        public int Max { get; set; }
        /// <summary>Duración de la ventana en segundos.</summary>
        public int Segundos { get; set; }

        public ThrottleAttribute() { Max = 60; Segundos = 60; }

        public override void OnActionExecuting(ActionExecutingContext ctx)
        {
            var accion = (string)ctx.RouteData.Values["action"] ?? "";
            var grupo = Grupo ?? accion;
            var clave = "thr:" + grupo + ":" + ctx.HttpContext.Ip();
            if (VentanaFija.Incrementar(clave, Segundos) > Max)
            {
                bufinsweb.Services.UsoService.Contar("throttle_429", grupo.Replace(' ', '-'));
                ctx.HttpContext.Response.Headers["Retry-After"] = Segundos.ToString();
                ctx.Result = new StatusCodeResult(429);
            }
        }
    }
}
