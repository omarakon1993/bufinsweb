using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using bufinsweb.Models;
using bufinsweb.Services;

namespace bufinsweb.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home
        public ActionResult Index()
        {
            ViewBag.Message = "¡Bienvenido a Bufins Web!";
            ViewBag.DateTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            return View();
        }

        // GET: Home/About
        public ActionResult About()
        {
            ViewBag.Message = "Acerca de Bufins Web";
            return View();
        }

        // GET: Home/Contact
        public ActionResult Contact()
        {
            ViewBag.Message = "Contacto";
            return View();
        }

        // POST: Home/Contact
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Contact(ContactFormModel model)
        {
            try
            {
                // Validar el modelo
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    return Json(new
                    {
                        success = false,
                        message = "Por favor, corrija los errores en el formulario.",
                        errors = errors
                    });
                }

                // Crear instancia del servicio de correo
                EmailService emailService = new EmailService();

                // Enviar correo al administrador
                emailService.SendContactFormToAdmin(model);

                // Enviar correo de confirmación al usuario
                emailService.SendConfirmationToUser(model);

                return Json(new
                {
                    success = true,
                    message = "¡Gracias por contactarnos! Hemos recibido tu mensaje y te responderemos pronto."
                });
            }
            catch (InvalidOperationException ex)
            {
                // Error de configuración (variables de entorno no configuradas)
                return Json(new
                {
                    success = false,
                    message = "Error de configuración del servidor. Por favor, contacte al administrador.",
                    error = ex.Message
                });
            }
            catch (Exception ex)
            {
                // Error general
                return Json(new
                {
                    success = false,
                    message = "Ha ocurrido un error al enviar el mensaje. Por favor, inténtelo más tarde.",
                    error = ex.Message
                });
            }
        }
    }
}
