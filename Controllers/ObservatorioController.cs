using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using bufinsweb.Helpers;
using System.Web.Mvc;
using bufinsweb.Filters;
using bufinsweb.Models.Observatorio;
using bufinsweb.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace bufinsweb.Controllers
{
    public class ObservatorioController : Controller
    {
        private static readonly JsonSerializerSettings JsonCfg = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver { NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false } },
            FloatFormatHandling = FloatFormatHandling.DefaultValue
        };

        private readonly ObservatorioService _svc = new ObservatorioService();
        private static readonly HashSet<string> PaginasBot = new HashSet<string> { "Index", "Sectores", "Comparador", "Empresa", "Informe", "Ranking", "Nodo", "Og" };

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.ObsVer = _svc.Datos.Version;   // versiona las URL de la API: al publicar datos nuevos cambian y no se sirve caché vieja
            var accion = Convert.ToString(filterContext.ActionDescriptor.ActionName);
            if (Request.HttpMethod == "GET" && PaginasBot.Contains(accion) && UsoService.EsBot(Request.UserAgent))
                UsoService.ContarBot(Request.UserAgent, accion);   // rastreadores (buscadores, IA, previsualizadores): solo un contador por familia
            base.OnActionExecuting(filterContext);
        }

        private ContentResult JsonOut(object o)
        {
            return Content(JsonConvert.SerializeObject(o, JsonCfg), "application/json", Encoding.UTF8);
        }

        // ------------------------------------------------------------ páginas

        /// <summary>Inicio: buscador de empresas y acceso a los tres módulos.</summary>
        public ActionResult Index()
        {
            Vista("vista_home", "");
            ViewBag.Datos = _svc.Datos;
            ViewBag.Top = _svc.TopNacional(10);
            return View();
        }

        /// <summary>Análisis por sector (benchmark).</summary>
        public ActionResult Sectores()
        {
            Vista("vista_sectores", "");
            ViewBag.Datos = _svc.Datos;
            return View();
        }

        /// <summary>Comparación de empresas (hasta 4).</summary>
        public ActionResult Comparador()
        {
            Vista("vista_comparar", "");
            ViewBag.Datos = _svc.Datos;
            return View();
        }

        /// <summary>Análisis individual de una empresa.</summary>
        public ActionResult Empresa(string id)
        {
            var e = _svc.PorNit(ObservatorioService.NitDeId(id));
            if (e == null) return NoEncontrado();
            // una sola URL por empresa: /observatorio/empresa/{nit}-{nombre}; las demás redirigen (301)
            var canon = ObservatorioService.UrlEmpresa(e);
            if (!string.Equals(Request.Path, canon, StringComparison.Ordinal)) return RedirectPermanent(canon + Request.Url.Query);
            Vista("vista_empresa", e.Ciiu, e.Nit, e.Razon);
            ViewBag.Datos = _svc.Datos;
            ViewBag.Narrativa = _svc.NarrativaEmpresa(e);
            ViewBag.OgImage = "/observatorio/og/" + e.Nit + "?v=" + _svc.Datos.Version;
            return View(e);
        }

        /// <summary>Informe imprimible (A4) de una empresa: se guarda como PDF desde el navegador.</summary>
        public ActionResult Informe(string id)
        {
            var e = _svc.PorNit(ObservatorioService.NitDeId(id));
            if (e == null) return NoEncontrado();
            Vista("informe_pdf", e.Ciiu, e.Nit, e.Razon);
            ViewBag.Datos = _svc.Datos;
            ViewBag.Narrativa = _svc.NarrativaEmpresa(e);
            return View(e);
        }

        /// <summary>Rankings: sin parámetros muestra el índice; con métrica, el Top 100 (nacional, sector, departamento o ciudad).</summary>
        public ActionResult Ranking(string metrica, string tipo, string clave)
        {
            ViewBag.Datos = _svc.Datos;
            if (string.IsNullOrEmpty(metrica))
            {
                Vista("vista_ranking", "indice");
                ViewBag.Hub = _svc.HubRankings();
                return View("RankingHub");
            }
            var r = _svc.Ranking(metrica, tipo, clave, 100);
            if (r == null) return NoEncontrado();
            Vista("vista_ranking", metrica, (tipo ?? "nacional") + "/" + (clave ?? ""));
            return View(r);
        }

        [Throttle(Grupo = "obs-og", Max = 40)]
        [OutputCache(Duration = 86400, VaryByParam = "v;id")]
        public ActionResult Og(string id)
        {
            var d = _svc.Datos;
            var e = _svc.PorNit(ObservatorioService.NitDeId(id));
            if (e == null) return NoEncontrado();
            var yv = e.En(d.MaxAnio, d.MinAnio) ?? e.Y.LastOrDefault(z => z != null);
            if (yv == null) return NoEncontrado();
            var cat = CiiuCatalogo.Actual;
            var sal = Salud.Calcular(yv);
            var kpis = new List<OgImage.Kpi>
            {
                new OgImage.Kpi { Etiqueta = "Ingresos", Valor = ObservatorioFormat.Moneda(yv[M.Ing] / 1000.0) },
                new OgImage.Kpi { Etiqueta = "EBITDA", Valor = ObservatorioFormat.Moneda(yv[M.Ebitda] / 1000.0) },
                new OgImage.Kpi { Etiqueta = "Margen EBITDA", Valor = ObservatorioFormat.Porcentaje(Ratio.MargenEbitda(yv)) },
                new OgImage.Kpi { Etiqueta = "Score de salud", Valor = sal.Total.ToString("0") + " / 100" }
            };
            var png = OgImage.Generar(e.Razon, cat.Nombre(e.Ciiu) + " · " + e.Ciudad, kpis, "NIT " + e.Nit + " · Año " + d.MaxAnio + " · bufins.com/observatorio", Server.MapPath("~/Assets/images/Bufins_Logofinal.png"));
            return File(png, "image/png");
        }

        [HttpPost]
        [Throttle(Grupo = "obs-evento", Max = 120)]
        public ActionResult Evento(string t, string k, string x, int ms = 0, int sc = 0, string tz = null, int w = 0)
        {
            t = (t ?? "").Trim();
            if (UsoService.TiposCliente.Contains(t)) UsoService.Registrar(Request, t, k, x, "", ms, sc, tz, w);
            return new HttpStatusCodeResult(204);
        }

        /// <summary>Vista de página (GET): registro anónimo con origen de la visita (buscador, red social, directo…).</summary>
        private void Vista(string tipo, string clave, string detalle = "", string nombre = "")
        {
            if (Request.HttpMethod == "GET") UsoService.Registrar(Request, tipo, clave, detalle, nombre, conOrigen: true);
        }

        private ActionResult NoEncontrado()
        {
            if (Request.HttpMethod == "GET") UsoService.Registrar(Request, "error_404", Convert.ToString(RouteData.Values["action"]));
            return HttpNotFound();
        }

        /// <summary>Página indexable por sección / división / clase CIIU.</summary>
        public ActionResult Nodo(string slug)
        {
            var s = _svc.Nodo(slug);
            if (s == null) return NoEncontrado();
            Vista("vista_sector", slug);
            ViewBag.Narrativa = _svc.NarrativaNodo(s);
            ViewBag.Datos = _svc.Datos;
            return View(s);
        }

        // ------------------------------------------------------------ API (GET, solo lectura)

        [Throttle(Grupo = "obs-opciones", Max = 30)]
        [OutputCache(Duration = 600, VaryByParam = "v")]
        public ActionResult Opciones() { return JsonOut(_svc.Opciones()); }

        [Throttle(Grupo = "obs-buscar", Max = 120)]
        [OutputCache(Duration = 300, VaryByParam = "q;n;v")]
        public ActionResult Buscar(string q, int n = 8) { return JsonOut(_svc.Buscar(q, n)); }

        [Throttle(Grupo = "obs-analisis", Max = 60)]
        [OutputCache(Duration = 300, VaryByParam = "*")]
        public ActionResult Analisis() { return JsonOut(_svc.Sector(LeerFiltro())); }

        [Throttle(Grupo = "obs-ficha", Max = 90)]
        [OutputCache(Duration = 300, VaryByParam = "nit;v")]
        public ActionResult Ficha(string nit)
        {
            var o = _svc.Detalle(nit);
            if (o == null) return NoEncontrado();
            return JsonOut(o);
        }

        [Throttle(Grupo = "obs-series", Max = 60)]
        [OutputCache(Duration = 300, VaryByParam = "nits;v")]
        public ActionResult Series(string nits)
        {
            var lista = (nits ?? "").Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
            return JsonOut(_svc.Comparar(lista));
        }

        [Throttle(Grupo = "obs-exportar", Max = 6)]
        public ActionResult ExportarXlsx()
        {
            Vista2("export_xlsx", "sector");
            var f = LeerFiltro();
            var bytes = _svc.XlsxSector(f);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "bufins-sector-" + (f.Anio ?? _svc.Datos.MaxAnio) + ".xlsx");
        }

        [Throttle(Grupo = "obs-exportar-empresa-xlsx", Max = 15)]
        public ActionResult ExportarEmpresaXlsx(string nit)
        {
            var e = _svc.PorNit(nit);
            if (e == null) return NoEncontrado();
            Vista2("export_xlsx", "empresa", e.Nit);
            var bytes = _svc.XlsxEmpresa(nit, ObservatorioRubros.Etiqueta, ObservatorioRubros.Orden.ToList());
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "bufins-" + e.Nit + ".xlsx");
        }

        private void Vista2(string tipo, string clave, string detalle = "") { UsoService.Registrar(Request, tipo, clave, detalle); }

        [Throttle(Grupo = "obs-exportar", Max = 6)]
        public ActionResult Exportar()
        {
            Vista2("export_csv", "sector");
            int anio;
            var filas = _svc.Exportar(LeerFiltro(), out anio);
            var sb = new StringBuilder();
            sb.AppendLine("NIT;Razon social;CIIU;Actividad;Ciudad;Departamento;Estado;Tamano;Ingresos (millones COP);Utilidad bruta;Utilidad operativa;EBITDA;Utilidad neta;Activos;Pasivos;Patrimonio;Deuda financiera;Capital de trabajo neto;Margen bruto %;Margen operativo %;Margen EBITDA %;Margen neto %;Deuda/EBITDA (x);Capital de trabajo/Ingresos %;Crecimiento ingresos %");
            foreach (var f in filas) sb.AppendLine(string.Join(";", f.Select(Csv)));
            sb.AppendLine();
            sb.AppendLine(Csv("Fuente: Superintendencia de Sociedades (datos.gov.co), estados financieros NIIF del ano " + anio + ", licencia CC BY-SA 4.0 (creativecommons.org/licenses/by-sa/4.0). Clasificacion CIIU Rev. 4 A.C.: Departamento Administrativo Nacional de Estadistica: www.dane.gov.co. Calculos de Bufins (bufins.com)."));
            return File(ConBom(sb.ToString()), "text/csv", "bufins-sector-" + anio + ".csv");
        }

        [Throttle(Grupo = "obs-exportar-empresa", Max = 15)]
        public ActionResult ExportarEmpresa(string nit)
        {
            var e = _svc.PorNit(nit);
            if (e == null) return NoEncontrado();
            Vista2("export_csv", "empresa");
            var orden = ObservatorioRubros.Orden;
            var filas = _svc.ExportarEmpresa(nit, ObservatorioRubros.Etiqueta, orden);
            var sb = new StringBuilder();
            sb.AppendLine(Csv(e.Razon + " - NIT " + e.Nit));
            foreach (var f in filas) sb.AppendLine(string.Join(";", f.Select(Csv)));
            sb.AppendLine();
            sb.AppendLine(Csv("Fuente: Superintendencia de Sociedades (datos.gov.co), licencia CC BY-SA 4.0 (creativecommons.org/licenses/by-sa/4.0). Cifras en miles de COP. Calculos de Bufins (bufins.com)."));
            return File(ConBom(sb.ToString()), "text/csv", "bufins-" + e.Nit + ".csv");
        }

        private static byte[] ConBom(string s) { return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(s)).ToArray(); }

        private static string Csv(string v)
        {
            v = v ?? "";
            return v.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        // ------------------------------------------------------------ parámetros

        private FiltroSector LeerFiltro()
        {
            var q = Request.QueryString;
            var f = new FiltroSector();
            f.Anio = Entero(q["anio"]);
            f.Ciiu = Lista(q["k"]);
            f.Tamanos = Lista(q["t"]).Where(x => new[] { "S", "M", "L", "XL" }.Contains(x)).ToList();
            f.Departamento = Texto(q["d"], 80);
            f.Estado = Texto(q["e"], 80);
            f.Q = Texto(q["q"], 80);
            f.IngMin = Decimal(q["imin"]);
            f.IngMax = Decimal(q["imax"]);
            f.Resultado = q["res"] == "utilidad" || q["res"] == "perdida" ? q["res"] : null;
            var orden = q["o"];
            f.Orden = new[] { "nombre", "ing", "uop", "ebitda", "un", "act", "deuda", "mbruto", "moper", "mebitda", "deudaebitda", "ktingresos", "crec", "potencial", "score" }.Contains(orden) ? orden : "ing";
            f.Senales = Lista(q["sn"]).Where(x => x.Length <= 20 && x.All(c => char.IsLetterOrDigit(c))).Take(10).ToList();
            f.PotencialMin = Math.Max(0, Math.Min(100, Entero(q["pot"]) ?? 0));
            f.Desc = q["asc"] != "1";
            f.Pagina = Entero(q["p"]) ?? 1;
            f.Tamano = Entero(q["ps"]) ?? 25;
            return f;
        }

        private static List<string> Lista(string v)
        {
            return (v ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Take(40).ToList();
        }
        private static string Texto(string v, int max)
        {
            v = (v ?? "").Trim();
            return v.Length == 0 ? null : (v.Length > max ? v.Substring(0, max) : v);
        }
        private static int? Entero(string v)
        {
            int i; return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out i) ? (int?)i : null;
        }
        private static double? Decimal(string v)
        {
            double d; return double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? (double?)d : null;
        }
    }

    /// <summary>Rubros del detalle (claves del ETL) con su etiqueta en español, solo para exportaciones CSV.</summary>
    public static class ObservatorioRubros
    {
        private static readonly string[][] Defs =
        {
            new[] { "ing", "Ingresos de actividades ordinarias" }, new[] { "costo", "Costo de ventas" }, new[] { "ub", "Utilidad bruta" },
            new[] { "gadm", "Gastos de administracion" }, new[] { "gven", "Gastos de ventas" }, new[] { "gdis", "Costos de distribucion" }, new[] { "ogas", "Otros gastos" },
            new[] { "gop", "Total gastos operacionales" }, new[] { "oing", "Otros ingresos" }, new[] { "ogan", "Otras ganancias (perdidas)" },
            new[] { "uop", "Utilidad operativa" }, new[] { "da", "Depreciacion y amortizacion" }, new[] { "ebitda", "EBITDA" },
            new[] { "ifin", "Ingresos financieros" }, new[] { "cfin", "Costos financieros" }, new[] { "uai", "Utilidad antes de impuestos" },
            new[] { "imp", "Impuestos" }, new[] { "un", "Utilidad neta" },
            new[] { "caja", "Efectivo y equivalentes" }, new[] { "cxc", "Cuentas por cobrar comerciales (corriente)" }, new[] { "inv", "Inventarios (corriente)" },
            new[] { "aimp", "Activos por impuestos corrientes" }, new[] { "oanf", "Otros activos no financieros corrientes" }, new[] { "ac", "Total activos corrientes" },
            new[] { "ppye", "Propiedades, planta y equipo" }, new[] { "intang", "Intangibles" }, new[] { "plus", "Plusvalia" }, new[] { "anc", "Total activos no corrientes" },
            new[] { "act", "Total activos" }, new[] { "cxp", "Proveedores y cuentas por pagar comerciales (corriente)" }, new[] { "pimp", "Pasivos por impuestos corrientes" },
            new[] { "pbe", "Beneficios a empleados (corriente)" }, new[] { "oprov", "Otras provisiones corrientes" }, new[] { "opnf", "Otros pasivos no financieros corrientes" },
            new[] { "opfc", "Otros pasivos financieros corrientes" }, new[] { "opfnc", "Otros pasivos financieros no corrientes" }, new[] { "deuda", "Deuda financiera" },
            new[] { "pc", "Total pasivos corrientes" }, new[] { "pnct", "Total pasivos no corrientes" }, new[] { "pas", "Total pasivos" }, new[] { "capital", "Capital emitido" },
            new[] { "gacum", "Ganancias acumuladas" }, new[] { "pat", "Patrimonio total" }, new[] { "ktn", "Capital de trabajo neto operativo" },
            new[] { "fco", "Flujo de efectivo de operacion" }, new[] { "fci", "Flujo de efectivo de inversion" }, new[] { "fcf", "Flujo de efectivo de financiacion" },
            new[] { "capex", "Compras de propiedades, planta y equipo" }, new[] { "intpag", "Intereses pagados" }, new[] { "divpag", "Dividendos pagados" },
            new[] { "prest_in", "Prestamos recibidos" }, new[] { "prest_out", "Reembolsos de prestamos" }, new[] { "dcaja", "Variacion neta del efectivo" },
            new[] { "cajai", "Efectivo al inicio del periodo" }, new[] { "cajaf", "Efectivo al final del periodo" }
        };

        public static IEnumerable<string> Orden { get { return Defs.Select(d => d[0]); } }
        public static string Etiqueta(string clave) { var d = Defs.FirstOrDefault(x => x[0] == clave); return d != null ? d[1] : clave; }
    }
}
