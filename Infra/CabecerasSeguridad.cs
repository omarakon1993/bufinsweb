using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace bufinsweb
{
    /// <summary>
    /// Cabeceras de seguridad que antes vivían en Web.config (system.webServer/httpProtocol/customHeaders).
    /// Se añaden al empezar a responder, así también salen en los estáticos, los 304 y los errores.
    /// </summary>
    public class CabecerasSeguridad
    {
        private readonly RequestDelegate _next;
        public CabecerasSeguridad(RequestDelegate next) { _next = next; }

        public Task InvokeAsync(HttpContext ctx)
        {
            ctx.Response.OnStarting(() =>
            {
                var h = ctx.Response.Headers;
                h["X-Content-Type-Options"] = "nosniff";
                h["X-Frame-Options"] = "SAMEORIGIN";
                h["X-XSS-Protection"] = "1; mode=block";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
                h.Remove("Server");
                return Task.CompletedTask;
            });
            return _next(ctx);
        }
    }
}
