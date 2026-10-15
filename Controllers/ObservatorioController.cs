using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.Mvc;
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

        private ContentResult JsonOut(object o)
        {
            return Content(JsonConvert.SerializeObject(o, JsonCfg), "application/json", Encoding.UTF8);
        }

        // ------------------------------------------------------------ páginas

        /// <summary>Inicio: buscador de empresas y acceso a los tres módulos.</summary>
        public ActionResult Index()
        {
            ViewBag.Datos = _svc.Datos;
            ViewBag.Top = _svc.TopNacional(10);
            return View();
        }

        /// <summary>Análisis por sector (benchmark).</summary>
        public ActionResult Sectores()
        {
            ViewBag.Datos = _svc.Datos;
            return View();
        }

        /// <summary>Comparación de empresas (hasta 4).</summary>
        public ActionResult Comparador()
        {
            ViewBag.Datos = _svc.Datos;
            return View();
        }

        /// <summary>Análisis individual de una empresa.</summary>
        public ActionResult Empresa(string nit)
        {
            var e = _svc.PorNit(nit);
            if (e == null) return HttpNotFound();
            ViewBag.Datos = _svc.Datos;
            return View(e);
        }

        /// <summary>Página indexable por sección / división / clase CIIU.</summary>
        public ActionResult Nodo(string slug)
        {
            var s = _svc.Nodo(slug);
            if (s == null) return HttpNotFound();
            ViewBag.Datos = _svc.Datos;
            return View(s);
        }

        // ------------------------------------------------------------ API (GET, solo lectura)

        [OutputCache(Duration = 600, VaryByParam = "*")]
        public ActionResult Opciones() { return JsonOut(_svc.Opciones()); }

        [OutputCache(Duration = 300, VaryByParam = "q;n")]
        public ActionResult Buscar(string q, int n = 8) { return JsonOut(_svc.Buscar(q, n)); }

        [OutputCache(Duration = 300, VaryByParam = "*")]
        public ActionResult Analisis() { return JsonOut(_svc.Sector(LeerFiltro())); }

        [OutputCache(Duration = 300, VaryByParam = "nit")]
        public ActionResult Ficha(string nit)
        {
            var o = _svc.Detalle(nit);
            if (o == null) return HttpNotFound();
            return JsonOut(o);
        }

        [OutputCache(Duration = 300, VaryByParam = "nits")]
        public ActionResult Series(string nits)
        {
            var lista = (nits ?? "").Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim());
            return JsonOut(_svc.Comparar(lista));
        }

        public ActionResult Exportar()
        {
            int anio;
            var filas = _svc.Exportar(LeerFiltro(), out anio);
            var sb = new StringBuilder();
            sb.AppendLine("NIT;Razon social;CIIU;Actividad;Ciudad;Departamento;Estado;Tamano;Ingresos (millones COP);Utilidad bruta;Utilidad operativa;EBITDA;Utilidad neta;Activos;Pasivos;Patrimonio;Deuda financiera;Capital de trabajo neto;Margen bruto %;Margen operativo %;Margen EBITDA %;Margen neto %;Deuda/EBITDA (x);Capital de trabajo/Ingresos %;Crecimiento ingresos %");
            foreach (var f in filas) sb.AppendLine(string.Join(";", f.Select(Csv)));
            sb.AppendLine();
            sb.AppendLine(Csv("Fuente: Superintendencia de Sociedades (datos.gov.co), estados financieros NIIF del ano " + anio + ". Calculos de Bufins (bufins.com)."));
            return File(ConBom(sb.ToString()), "text/csv", "bufins-sector-" + anio + ".csv");
        }

        public ActionResult ExportarEmpresa(string nit)
        {
            var e = _svc.PorNit(nit);
            if (e == null) return HttpNotFound();
            var orden = ObservatorioRubros.Orden;
            var filas = _svc.ExportarEmpresa(nit, ObservatorioRubros.Etiqueta, orden);
            var sb = new StringBuilder();
            sb.AppendLine(Csv(e.Razon + " - NIT " + e.Nit));
            foreach (var f in filas) sb.AppendLine(string.Join(";", f.Select(Csv)));
            sb.AppendLine();
            sb.AppendLine(Csv("Fuente: Superintendencia de Sociedades (datos.gov.co). Cifras en miles de COP. Calculos de Bufins (bufins.com)."));
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
            f.Orden = new[] { "nombre", "ing", "uop", "ebitda", "un", "act", "deuda", "mbruto", "moper", "mebitda", "deudaebitda", "ktingresos", "crec" }.Contains(orden) ? orden : "ing";
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
