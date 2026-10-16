using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Hosting;
using Newtonsoft.Json;

namespace bufinsweb.Services
{
    /// <summary>
    /// Analítica propia y anónima del Observatorio: solo contadores agregados (día | tipo | clave). No guarda IP, cookies ni User-Agent.
    /// Los contadores viven en memoria y se vuelcan cada 5 minutos (y al cerrar la aplicación) a App_Data/observatorio/uso/yyyy-MM.json.
    /// </summary>
    public static class UsoService
    {
        public static readonly HashSet<string> Tipos = new HashSet<string>
        {
            "vista_home", "vista_empresa", "vista_sector", "vista_ranking", "vista_comparar", "vista_sectores",
            "busqueda_sel", "cta_click", "export_csv", "export_xlsx", "informe_pdf", "filtro_sector"
        };

        private static readonly ConcurrentDictionary<string, long> Pendiente = new ConcurrentDictionary<string, long>();
        private static readonly object LockArchivo = new object();
        private static Timer _timer;
        private static readonly Regex Bots = new Regex("bot|crawl|spider|slurp|preview|facebookexternalhit|curl|wget|python|monitor|headless|lighthouse", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ClaveOk = new Regex("^[a-z0-9\\-_.]{1,40}$", RegexOptions.Compiled);

        public static void Iniciar()
        {
            if (_timer == null) _timer = new Timer(_ => Vaciar(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        public static bool EsBot(string userAgent) { return string.IsNullOrEmpty(userAgent) || Bots.IsMatch(userAgent); }

        public static void Contar(string tipo, string clave)
        {
            if (!Tipos.Contains(tipo)) return;
            clave = (clave ?? "").Trim().ToLowerInvariant();
            if (clave.Length > 0 && !ClaveOk.IsMatch(clave)) clave = clave.Length > 40 ? "" : Regex.Replace(clave, "[^a-z0-9\\-_.]", "");
            if (clave.Length > 40) clave = clave.Substring(0, 40);
            if (Pendiente.Count > 20000) return;   // protección ante abuso: no crece sin límite
            var k = DateTime.UtcNow.AddHours(-5).ToString("yyyy-MM-dd") + "|" + tipo + "|" + clave;
            Pendiente.AddOrUpdate(k, 1, (_, v) => v + 1);
        }

        private static string Carpeta { get { return HostingEnvironment.MapPath("~/App_Data/observatorio/uso"); } }

        public static void Vaciar()
        {
            try
            {
                if (Pendiente.IsEmpty) return;
                var lote = new Dictionary<string, long>();
                foreach (var k in Pendiente.Keys.ToList()) { long v; if (Pendiente.TryRemove(k, out v)) lote[k] = v; }
                if (lote.Count == 0) return;
                lock (LockArchivo)
                {
                    Directory.CreateDirectory(Carpeta);
                    foreach (var grupo in lote.GroupBy(x => x.Key.Substring(0, 7)))
                    {
                        var ruta = Path.Combine(Carpeta, grupo.Key + ".json");
                        var actual = new Dictionary<string, long>();
                        if (File.Exists(ruta))
                        {
                            try { actual = JsonConvert.DeserializeObject<Dictionary<string, long>>(File.ReadAllText(ruta, Encoding.UTF8)) ?? actual; } catch (Exception) { }
                        }
                        foreach (var kv in grupo) { long v; actual.TryGetValue(kv.Key, out v); actual[kv.Key] = v + kv.Value; }
                        var tmp = ruta + ".tmp";
                        File.WriteAllText(tmp, JsonConvert.SerializeObject(actual), new UTF8Encoding(false));
                        if (File.Exists(ruta)) File.Delete(ruta);
                        File.Move(tmp, ruta);
                    }
                }
            }
            catch (Exception) { /* la analítica nunca debe afectar al sitio */ }
        }
    }
}
