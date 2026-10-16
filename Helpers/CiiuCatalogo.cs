using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using Newtonsoft.Json;

namespace bufinsweb.Helpers
{
    /// <summary>
    /// Catálogo CIIU Rev. 4 A.C. (DANE): sección (letra) > división (2) > grupo (3) > clase (4).
    /// Se carga de App_Data/observatorio/ciiu.json (generado por Tools/ObservatorioEtl/build_ciiu.py).
    /// </summary>
    public class CiiuCatalogo
    {
        private const string CacheKey = "observatorio:ciiu";

        public class DivisionInfo { [JsonProperty("n")] public string N; [JsonProperty("s")] public string S; }

        [JsonProperty("secciones")] public Dictionary<string, string> Secciones = new Dictionary<string, string>();
        [JsonProperty("divisiones")] public Dictionary<string, DivisionInfo> Divisiones = new Dictionary<string, DivisionInfo>();
        [JsonProperty("grupos")] public Dictionary<string, string> Grupos = new Dictionary<string, string>();
        [JsonProperty("clases")] public Dictionary<string, string> Clases = new Dictionary<string, string>();

        public static CiiuCatalogo Actual { get { return Cargar(); } }

        /// <summary>Carga (o devuelve de caché) el catálogo; usa HostingEnvironment para poder invocarse desde hilos sin HttpContext.</summary>
        public static CiiuCatalogo Cargar()
        {
            var c = HttpRuntime.Cache[CacheKey] as CiiuCatalogo;
            if (c != null) return c;
            var path = HostingEnvironment.MapPath("~/App_Data/observatorio/ciiu.json");
            c = File.Exists(path) ? JsonConvert.DeserializeObject<CiiuCatalogo>(File.ReadAllText(path, Encoding.UTF8)) : new CiiuCatalogo();
            HttpRuntime.Cache.Insert(CacheKey, c, File.Exists(path) ? new CacheDependency(path) : null);
            return c;
        }

        /// <summary>CIIU de 4 dígitos (el cero inicial suele perderse en la fuente).</summary>
        public static string Clase(string ciiu)
        {
            var d = new string((ciiu ?? "").Where(char.IsDigit).ToArray());
            return d.Length == 0 ? "" : d.PadLeft(4, '0').Substring(0, 4);
        }

        public static string Division(string ciiu) { var c = Clase(ciiu); return c.Length == 4 ? c.Substring(0, 2) : ""; }

        public string Seccion(string division)
        {
            DivisionInfo d;
            return Divisiones.TryGetValue(division ?? "", out d) ? d.S : "";
        }

        /// <summary>Nombre de cualquier nivel: "G", "47", "471" o "4711".</summary>
        public string Nombre(string codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return "";
            string n;
            switch (codigo.Length)
            {
                case 1: return Secciones.TryGetValue(codigo, out n) ? n : codigo;
                case 2: DivisionInfo d; return Divisiones.TryGetValue(codigo, out d) ? d.N : "Actividad " + codigo;
                case 3: return Grupos.TryGetValue(codigo, out n) ? n : "Grupo " + codigo;
                default: return Clases.TryGetValue(codigo, out n) ? n : "Clase " + codigo;
            }
        }

        /// <summary>¿El CIIU de 4 dígitos pertenece al nodo (sección, división, grupo o clase)?</summary>
        public bool Pertenece(string clase4, string nodo)
        {
            if (string.IsNullOrEmpty(nodo) || clase4.Length != 4) return false;
            if (nodo.Length == 1) return Seccion(clase4.Substring(0, 2)) == nodo;
            return clase4.StartsWith(nodo, StringComparison.Ordinal);
        }

        /// <summary>Árbol {c, n, k, h[]} con conteo de empresas por nodo (solo nodos con k &gt; 0).</summary>
        public List<object> Arbol(Dictionary<string, int> conteoPorClase)
        {
            Func<string, int> cuenta = prefijo => conteoPorClase.Where(kv => kv.Key.StartsWith(prefijo, StringComparison.Ordinal)).Sum(kv => kv.Value);
            var res = new List<object>();
            foreach (var s in Secciones.OrderBy(x => x.Key))
            {
                var divs = Divisiones.Where(d => d.Value.S == s.Key).OrderBy(d => d.Key).ToList();
                var hijosS = new List<object>();
                int totS = 0;
                foreach (var d in divs)
                {
                    int kd = cuenta(d.Key);
                    if (kd == 0) continue;
                    var hijosD = new List<object>();
                    foreach (var g in Grupos.Where(x => x.Key.StartsWith(d.Key, StringComparison.Ordinal)).OrderBy(x => x.Key))
                    {
                        int kg = cuenta(g.Key);
                        if (kg == 0) continue;
                        var hijosG = Clases.Where(x => x.Key.StartsWith(g.Key, StringComparison.Ordinal)).OrderBy(x => x.Key)
                            .Select(c => new { c = c.Key, n = c.Value, k = cuenta(c.Key) }).Where(c => c.k > 0).Cast<object>().ToList();
                        hijosD.Add(new { c = g.Key, n = g.Value, k = kg, h = hijosG });
                    }
                    totS += kd;
                    hijosS.Add(new { c = d.Key, n = d.Value.N, k = kd, h = hijosD });
                }
                if (totS > 0) res.Add(new { c = s.Key, n = s.Value, k = totS, h = hijosS });
            }
            return res;
        }
    }
}
