using System;
using System.IO.Compression;
using System.Web;

namespace bufinsweb.Filters
{
    /// <summary>
    /// Compresión gzip hecha por la aplicación: el IIS del hosting (Conexcol) no comprime nada, aunque Web.config lo pida
    /// (verificado en producción, oct-2026). Se aplica a las respuestas de texto que genera ASP.NET (páginas, API JSON,
    /// sitemaps, CSV y bundles de CSS/JS); los archivos estáticos sueltos de /Assets no pasan por aquí, por eso el CSS/JS
    /// del layout sale por bundles. Se llama desde Global.asax (PostReleaseRequestState, antes del filtrado de la respuesta).
    /// </summary>
    public static class Compresion
    {
        public static void Aplicar(HttpApplication app)
        {
            var res = app.Response;
            var req = app.Request;
            try
            {
                if (res.StatusCode != 200 || res.SuppressContent || res.HeadersWritten) return;
                if (!string.IsNullOrEmpty(res.Headers["Content-Encoding"])) return;
                var tipo = (res.ContentType ?? "").ToLowerInvariant();
                var texto = tipo.StartsWith("text/") || tipo.Contains("json") || tipo.Contains("xml") || tipo.Contains("javascript");
                if (!texto) return;

                // la caché de salida guarda una copia por codificación (con y sin gzip)
                res.Cache.VaryByContentEncodings["gzip"] = true;
                var ae = req.Headers["Accept-Encoding"] ?? "";
                if (ae.IndexOf("gzip", StringComparison.OrdinalIgnoreCase) < 0) return;

                res.Filter = new GZipStream(res.Filter, CompressionLevel.Optimal);
                res.AppendHeader("Content-Encoding", "gzip");
                res.AppendHeader("Vary", "Accept-Encoding");
            }
            catch (Exception) { /* si algo falla se envía sin comprimir */ }
        }
    }
}
