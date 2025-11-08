using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

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
    }
}
