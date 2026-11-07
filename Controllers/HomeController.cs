using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using bufinsweb.Models;
using bufinsweb.Services;

namespace bufinsweb.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            // Embudo anónimo: ¿llegan a «Contacto» desde Pulso Bufins o de otro lado?
            Uri referente;
            var desde = Uri.TryCreate(Request.Headers.Referer.ToString(), UriKind.Absolute, out referente) && referente.AbsolutePath.StartsWith("/pulso-bufins", StringComparison.OrdinalIgnoreCase) ? "pulso" : "otro";
            UsoService.Registrar(Request, "vista_contacto", desde, conOrigen: true);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Contact(ContactFormModel model)
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
        public IActionResult SetLanguage(string lang, string returnUrl)
        {
            if (lang == "es-CO" || lang == "en-US")
            {
                Response.Cookies.Append("lang", lang, new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddYears(1),
                    HttpOnly = true,
                    IsEssential = true
                });
            }

            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
                returnUrl = "/";

            return Redirect(returnUrl);
        }
    
        /// <summary>Página de error de producción (UseExceptionHandler): reemplaza a HandleError de MVC 5.</summary>
        [Route("Home/Error")]
        public IActionResult Error()
        {
            Response.StatusCode = 500;
            return View("~/Views/Shared/Error.cshtml");
        }
    }
}
