using System.Web;
using System.Web.Mvc;

namespace bufinsweb.Filters
{
    /// <summary>
    /// HandleError con un caso extra: las peticiones inválidas que mandan los robots (texto con "&lt;" en la URL o el
    /// formulario, POST al contacto sin token antifalsificación) responden 400 sin cuerpo en lugar de un error 500.
    /// El resto de excepciones siguen mostrando la vista Error.
    /// </summary>
    public class ManejoErroresAttribute : HandleErrorAttribute
    {
        public override void OnException(ExceptionContext ctx)
        {
            if (ctx.ExceptionHandled) return;
            if (EsSolicitudInvalida(ctx.Exception))
            {
                ctx.ExceptionHandled = true;
                ctx.Result = new HttpStatusCodeResult(400);
                ctx.HttpContext.Response.TrySkipIisCustomErrors = true;
                return;
            }
            base.OnException(ctx);
        }

        public static bool EsSolicitudInvalida(System.Exception ex)
        {
            var raiz = ex == null ? null : ex.GetBaseException();
            return ex is HttpRequestValidationException || raiz is HttpRequestValidationException
                || ex is HttpAntiForgeryException || raiz is HttpAntiForgeryException;
        }
    }
}
