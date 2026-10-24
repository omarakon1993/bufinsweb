using System;
using System.Web;
using System.Web.Mvc;
using bufinsweb.Models;
using bufinsweb.Services;

namespace bufinsweb.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            return View();
        }

        public ActionResult Contact()
        {
            // Embudo anónimo: ¿llegan a «Contacto» desde Pulso Bufins o de otro lado?
            var desde = Request.UrlReferrer != null && Request.UrlReferrer.AbsolutePath.StartsWith("/pulso-bufins", StringComparison.OrdinalIgnoreCase) ? "pulso" : "otro";
            UsoService.Registrar(Request, "vista_contacto", desde, conOrigen: true);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Contact(ContactFormModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = System.Linq.Enumerable.ToList(
                        System.Linq.Enumerable.Select(
                            System.Linq.Enumerable.SelectMany(ModelState.Values, v => v.Errors),
                            e => e.ErrorMessage));

                    return Json(new { success = false, message = "Por favor, corrija los errores en el formulario.", errors = errors });
                }

                EmailService emailService = new EmailService();
                emailService.SendContactFormToAdmin(model);
                emailService.SendConfirmationToUser(model);
                UsoService.Registrar(Request, "contacto_enviado", "web");   // solo el hecho: nada del formulario se guarda en la analítica

                return Json(new { success = true, message = "¡Gracias por contactarnos! Hemos recibido tu mensaje y te responderemos pronto." });
            }
            catch (InvalidOperationException)
            {
                return Json(new { success = false, message = "Error de configuración del servidor. Por favor, contacte al administrador." });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Ha ocurrido un error al enviar el mensaje. Por favor, inténtelo más tarde." });
            }
        }

        [HttpGet]
        public ActionResult SetLanguage(string lang, string returnUrl)
        {
            if (lang == "es-CO" || lang == "en-US")
            {
                var cookie = new HttpCookie("lang", lang)
                {
                    Expires = DateTime.Now.AddYears(1),
                    HttpOnly = true
                };
                Response.SetCookie(cookie);
            }

            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
                returnUrl = "/";

            return Redirect(returnUrl);
        }
    }
}
