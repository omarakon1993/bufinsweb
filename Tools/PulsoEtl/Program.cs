using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PulsoEtl
{
    /// <summary>
    /// ETL de Pulso Bufins: estados financieros NIIF de las sociedades que reportan a la Supersociedades
    /// (API abierta Socrata de datos.gov.co, sin llave).
    ///
    ///   PulsoEtl.exe --out "C:\...\bufinsweb\App_Data\pulso" [--force] [--years 2024 2025] [--cache DIR] [--workers 5] [--solo-grandes]
    ///   --solo-grandes: solo regenera grandes.json (empresas de «10.000 más grandes» que no están en los NIIF); tarda segundos.
    ///
    /// Genera en --out: empresas.bin, meta.json, detalle.{version}.bin, detalle.{version}.idx.json y escribe el avance en etl_status.json.
    /// La publicación es atómica: los archivos nuevos se escriben con nombre temporal/versionado y empresas.bin se reemplaza al final;
    /// si el proceso se interrumpe, el sitio sigue sirviendo los datos anteriores.
    /// Códigos de salida: 0 ok o sin cambios, 2 argumentos, 3 ya hay otra ejecución, 1 error.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288;
            ServicePointManager.DefaultConnectionLimit = 32;
            Opciones o;
            try { o = Opciones.Leer(args); }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 2; }

            Directory.CreateDirectory(o.Salida);
            FileStream bloqueo;
            try { bloqueo = new FileStream(Path.Combine(o.Salida, "etl.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { Console.Error.WriteLine("Ya hay una actualización en curso."); return 3; }

            var estado = new Estado(o.EstadoArchivo);
            try
            {
                using (bloqueo) new Etl(o, estado).Ejecutar();
                return 0;
            }
            catch (Exception ex)
            {
                estado.Error(ex.GetBaseException().Message);
                Console.Error.WriteLine(ex);
                return 1;
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ opciones y estado

    internal class Opciones
    {
        public string Salida, Cache, EstadoArchivo;
        public bool Forzar, SoloGrandes;
        public int Workers = 5;
        public List<int> Anios = new List<int>();

        public static Opciones Leer(string[] a)
        {
            var o = new Opciones();
            for (var i = 0; i < a.Length; i++)
            {
                switch (a[i])
                {
                    case "--out": o.Salida = Path.GetFullPath(a[++i]); break;
                    case "--cache": o.Cache = Path.GetFullPath(a[++i]); break;
                    case "--status": o.EstadoArchivo = Path.GetFullPath(a[++i]); break;
                    case "--workers": o.Workers = Math.Max(1, Math.Min(12, int.Parse(a[++i]))); break;
                    case "--force": o.Forzar = true; break;
                    case "--solo-grandes": o.SoloGrandes = true; break;
                    case "--years":
                        while (i + 1 < a.Length && !a[i + 1].StartsWith("--")) o.Anios.Add(int.Parse(a[++i]));
                        break;
                    default: throw new ArgumentException("Argumento desconocido: " + a[i]);
                }
            }
            if (string.IsNullOrEmpty(o.Salida)) throw new ArgumentException("Falta --out <carpeta App_Data\\pulso>");
            if (o.EstadoArchivo == null) o.EstadoArchivo = Path.Combine(o.Salida, "etl_status.json");
            o.Anios.Sort();
            return o;
        }
    }

    /// <summary>Avance de la ejecución, escrito a etl_status.json para que el portal lo muestre.</summary>
    internal class Estado
    {
        private readonly string _ruta;
        private readonly object _l = new object();
        private readonly List<string> _log = new List<string>();
        private DateTime _ultimo = DateTime.MinValue;
        public string Valor = "corriendo", Fase = "Iniciando", Mensaje = "", Mensajes = "";
        public int Progreso;
        public DateTime Inicio = DateTime.Now;
        public DateTime? Fin;
        public int Empresas;
        public string FuenteActualizado = "", Anios = "";

        public Estado(string ruta) { _ruta = ruta; Guardar(true); }

        public void Paso(string fase, int progreso, string mensaje = null)
        {
            lock (_l) { Fase = fase; Progreso = Math.Max(Progreso, Math.Min(100, progreso)); if (mensaje != null) Mensaje = mensaje; }
            Log(fase + (mensaje != null ? " – " + mensaje : ""));
            Guardar(true);
        }

        public void Avance(int progreso, string mensaje)
        {
            lock (_l) { Progreso = Math.Max(Progreso, Math.Min(100, progreso)); Mensaje = mensaje; }
            Guardar(false);
        }

        public void Log(string linea)
        {
            var t = DateTime.Now.ToString("HH:mm:ss") + "  " + linea;
            lock (_l) { _log.Add(t); if (_log.Count > 60) _log.RemoveAt(0); }
            Console.WriteLine(t);
        }

        public void Termina(string valor, string mensaje)
        {
            lock (_l) { Valor = valor; Mensaje = mensaje; Fin = DateTime.Now; Progreso = 100; Fase = valor == "ok" ? "Terminado" : "Sin cambios"; }
            Log(mensaje);
            Guardar(true);
        }

        public void Error(string mensaje)
        {
            lock (_l) { Valor = "error"; Mensaje = mensaje; Fin = DateTime.Now; Fase = "Error"; }
            Log("ERROR: " + mensaje);
            Guardar(true);
        }

        public void Guardar(bool forzar)
        {
            lock (_l)
            {
                if (!forzar && (DateTime.Now - _ultimo).TotalMilliseconds < 1000) return;
                _ultimo = DateTime.Now;
                var d = new Dictionary<string, object>
                {
                    { "estado", Valor }, { "fase", Fase }, { "progreso", Progreso }, { "mensaje", Mensaje },
                    { "inicio", Inicio.ToString("yyyy-MM-ddTHH:mm:ss") }, { "fin", Fin.HasValue ? Fin.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null },
                    { "pid", Process.GetCurrentProcess().Id }, { "empresas", Empresas }, { "fuenteActualizado", FuenteActualizado }, { "anios", Anios },
                    { "log", _log.ToArray() }
                };
                try
                {
                    var tmp = _ruta + ".tmp";
                    File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(d), new UTF8Encoding(false));
                    File.Copy(tmp, _ruta, true);
                    File.Delete(tmp);
                }
                catch (IOException) { /* el lector puede tenerlo abierto un instante */ }
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ reglas de mapeo

    internal static class Reglas
    {
        public static readonly Dictionary<string, string> Datasets = new Dictionary<string, string>
        {
            { "car", "6hqw-m3dm" }, { "bal", "pfdp-zks5" }, { "eri", "prwj-nzxa" }, { "flu", "ctcp-462n" }
        };

        // concepto normalizado (sin tildes, minúsculas) -> clave interna
        public static readonly Dictionary<string, Dictionary<string, string>> Mapa = new Dictionary<string, Dictionary<string, string>>
        {
            { "eri", new Dictionary<string, string> {
                { "ingresos de actividades ordinarias", "ing" }, { "costo de ventas", "costo" }, { "ganancia bruta", "ub" },
                { "gastos de administracion", "gadm" }, { "gastos de ventas", "gven" }, { "costos de distribucion", "gdis" },
                { "otros ingresos", "oing" }, { "otros gastos", "ogas" }, { "otras ganancias (perdidas)", "ogan" },
                { "ganancia (perdida) por actividades de operacion", "uop" }, { "ingresos financieros", "ifin" }, { "costos financieros", "cfin" },
                { "ganancia (perdida), antes de impuestos", "uai" }, { "ingreso (gasto) por impuestos", "imp" }, { "ganancia (perdida)", "un" } } },
            { "bal", new Dictionary<string, string> {
                { "efectivo y equivalentes al efectivo", "caja" }, { "cuentas comerciales por cobrar y otras cuentas por cobrar corrientes", "cxc" },
                { "inventarios corrientes", "inv" }, { "activos por impuestos corrientes, corriente", "aimp" }, { "otros activos no financieros corrientes", "oanf" },
                { "activos corrientes totales", "ac" }, { "propiedades, planta y equipo", "ppye" }, { "activos intangibles distintos de la plusvalia", "intang" },
                { "plusvalia", "plus" }, { "total de activos no corrientes", "anc" }, { "total de activos", "act" },
                { "cuentas por pagar comerciales y otras cuentas por pagar corrientes", "cxp" }, { "pasivos por impuestos corrientes, corriente", "pimp" },
                { "provisiones corrientes por beneficios a los empleados", "pbe" }, { "otras provisiones corrientes", "oprov" },
                { "otros pasivos no financieros corrientes", "opnf" }, { "otros pasivos financieros corrientes", "opfc" }, { "prestamos corrientes", "pcor" },
                { "parte corriente de prestamos no corrientes", "pcpnc" }, { "otros pasivos financieros no corrientes", "opfnc" },
                { "parte no corriente de prestamos no corrientes", "pnc" }, { "pasivos corrientes totales", "pc" }, { "total de pasivos no corrientes", "pnct" },
                { "total pasivos", "pas" }, { "patrimonio total", "pat" }, { "capital emitido", "capital" }, { "ganancias acumuladas", "gacum" } } },
            { "flu", new Dictionary<string, string> {
                { "( + ) ajustes por gastos de depreciacion y amortizacion", "da" },
                { "flujos de efectivo netos procedentes de (utilizados en) actividades de operacion", "fco" },
                { "flujos de efectivo netos procedentes de (utilizados en) actividades de inversion", "fci" },
                { "flujos de efectivo netos procedentes de (utilizados en) actividades de financiacion", "fcf" },
                { "(-) compras de propiedades, planta y equipo", "capex" }, { "efectivo y equivalentes al efectivo al final del periodo", "cajaf" },
                { "efectivo y equivalentes al efectivo al principio del periodo", "cajai" },
                { "incremento (disminucion) neto de efectivo y equivalentes al efectivo", "dcaja" }, { "(-) intereses pagados", "intpag" },
                { "(-) dividendos pagados", "divpag" }, { "(+) importes procedentes de prestamos", "prest_in" }, { "(-) reembolsos de prestamos", "prest_out" } } },
            { "car", new Dictionary<string, string> {
                { "razon social de la sociedad", "razon" }, { "clasificacion industrial internacional uniforme version 4 a.c (ciiu)", "ciiu" },
                { "fecha de constitucion (aaaa-mm-dd)", "fconst" }, { "objeto social principal", "objeto" }, { "estado actual", "estado" },
                { "ciudad de la direccion del domicilio", "ciudad" }, { "departamento de la direccion del domicilio", "depto" }, { "tipo societario", "tipo" },
                { "la sociedad es", "clase" }, { "unidad de diligenciamiento utilizada en este reporte", "unidad" },
                { "concepto del revisor fiscal en su informe", "rfop" }, { "la compania esta obligada a tener revisor fiscal?", "rf" } } }
        };

        // Métricas del índice (miles de COP), en este orden: debe coincidir con M en Models/Pulso/PulsoModels.cs
        public static readonly string[] Metricas =
        {
            "ing", "ub", "gop", "uop", "ebitda", "un", "act", "pas", "pat", "ktn", "deuda", "cxc", "inv", "cxp", "fco",
            "caja", "costo", "da", "cfin", "ac", "pc", "ppye", "gacum"
        };

        public static string Norm(string s)
        {
            var f = (s ?? "").Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in f) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return Regex.Replace(sb.ToString().ToLowerInvariant(), @"\s+", " ").Trim();
        }

        /// <summary>La fuente trae U+FFFD donde iba una letra con tilde o eñe: se acepta como comodín de 1 carácter.</summary>
        private static bool Coincide(string regla, string normalizado)
        {
            if (regla.Length != normalizado.Length) return false;
            for (var i = 0; i < regla.Length; i++) if (regla[i] != normalizado[i] && normalizado[i] != '\ufffd') return false;
            return true;
        }

        public static string ClaveDe(string ds, string concepto)
        {
            var n = Norm(concepto);
            string k;
            if (Mapa[ds].TryGetValue(n, out k)) return k;
            if (n.IndexOf('\ufffd') >= 0)
                foreach (var kv in Mapa[ds]) if (Coincide(kv.Key, n)) return kv.Value;
            return null;
        }

        public static double FactorMiles(string unidad)
        {
            var u = Norm(unidad);
            if (u.StartsWith("millones")) return 1000.0;
            if (u.StartsWith("miles")) return 1.0;
            if (u.StartsWith("pesos")) return 0.001;
            return 1.0;
        }
    }

    // ------------------------------------------------------------------------------------------------ reparación de textos

    internal static class Textos
    {
        private static readonly Regex Palabra = new Regex("[A-Za-zÁÉÍÓÚÜÑáéíóúüñ\ufffd]+", RegexOptions.Compiled);
        private const string Mayus = "ÁÉÍÓÚÑÜ";
        private const string Minus = "áéíóúñü";

        private static readonly string[] Semilla = ("anónima anónimo reestructuración ejecución trámite liquidación obligatoria judicial económica extranjera extranjeras régimen " +
            "inspección vigilancia constitución compañía compañías operación operaciones información administración construcción " +
            "distribución comercialización producción fabricación transformación explotación prestación educación comunicación " +
            "organización organizaciones tecnología tecnologías asesoría consultoría ingeniería inversión inversiones importación " +
            "exportación participación sociedad sociedades corporación fundación cooperativa asociación mercancía mercancías " +
            "artículos máquinas maquinaria vehículos energía alimentación alimenticios químicos plásticos ópticos eléctricos " +
            "electrónicos metálicos métodos servicios jurídicos públicos públicas básicos").Split(' ');

        public static Dictionary<string, int> Vocabulario(IEnumerable<string> textos)
        {
            var c = new Dictionary<string, int>();
            foreach (var s in Semilla) c[s] = 1;
            foreach (var t in textos)
            {
                if (string.IsNullOrEmpty(t)) continue;
                foreach (Match m in Palabra.Matches(t))
                {
                    var tok = m.Value;
                    if (tok.IndexOf('\ufffd') >= 0 || !tok.Any(ch => ch > 127)) continue;
                    var k = tok.ToLowerInvariant();
                    int n; c.TryGetValue(k, out n); c[k] = n + 1;
                }
            }
            return c;
        }

        private static string ReparaToken(string tok, Dictionary<string, int> vocab)
        {
            var pos = new List<int>();
            for (var i = 0; i < tok.Length; i++) if (tok[i] == '\ufffd') pos.Add(i);
            if (pos.Count == 0) return tok;
            var letras = tok.Where(ch => ch != '\ufffd').ToList();
            var mayus = letras.Count > 0 && letras.All(char.IsUpper);
            var juego = mayus ? Mayus : Minus;
            if (pos.Count <= 3)
            {
                string mejor = null; var mejorN = 0;
                var total = (int)Math.Pow(juego.Length, pos.Count);
                for (var combo = 0; combo < total; combo++)
                {
                    var cand = tok.ToCharArray();
                    var x = combo;
                    for (var p = pos.Count - 1; p >= 0; p--) { cand[pos[p]] = juego[x % juego.Length]; x /= juego.Length; }
                    var s = new string(cand);
                    int n;
                    if (vocab.TryGetValue(s.ToLowerInvariant(), out n) && n > mejorN) { mejor = s; mejorN = n; }
                }
                if (mejor != null) return mejor;
            }
            // reglas de respaldo
            var sb = new StringBuilder();
            for (var i = 0; i < tok.Length; i++)
            {
                if (tok[i] != '\ufffd') { sb.Append(tok[i]); continue; }
                var prev = i > 0 ? char.ToLowerInvariant(tok[i - 1]).ToString() : "";
                var nxt = i + 1 < tok.Length ? char.ToLowerInvariant(tok[i + 1]).ToString() : "";
                string r;
                if (nxt == "n" && i + 2 == tok.Length) r = "o";
                else if ((prev == "" || "aeiou".Contains(prev)) && nxt != "" && "aeiou".Contains(nxt)) r = "n";
                else r = "i";
                if (mayus) r = r.ToUpperInvariant();
                sb.Append(r.Replace("O", "Ó").Replace("o", "ó").Replace("N", "Ñ").Replace("n", "ñ").Replace("I", "Í").Replace("i", "í"));
            }
            return sb.ToString();
        }

        public static string Repara(string texto, Dictionary<string, int> vocab)
        {
            if (string.IsNullOrEmpty(texto) || texto.IndexOf('\ufffd') < 0) return texto;
            return Palabra.Replace(texto, m => ReparaToken(m.Value, vocab)).Replace("\ufffd", "");
        }
    }

    // ------------------------------------------------------------------------------------------------ cliente Socrata

    internal class Socrata
    {
        private const string Base = "https://www.datos.gov.co";
        private static readonly Random Azar = new Random();
        private readonly string _cache;
        private readonly string _token = Environment.GetEnvironmentVariable("SOCRATA_APP_TOKEN");
        public const int Pagina = 50000;

        public Socrata(string cache) { _cache = cache; if (cache != null) Directory.CreateDirectory(cache); }

        private byte[] Get(string url)
        {
            for (var intento = 0; ; intento++)
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    req.Timeout = 180000; req.ReadWriteTimeout = 180000;
                    req.UserAgent = "PulsoBufinsETL/2.0";
                    if (!string.IsNullOrEmpty(_token)) req.Headers.Add("X-App-Token", _token);
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var s = resp.GetResponseStream())
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    if (!(ex is WebException || ex is IOException || ex is TimeoutException) || intento >= 6) throw;
                    Thread.Sleep((int)(Math.Min(60, Math.Pow(2, intento) + Azar.NextDouble() * 2) * 1000));
                }
            }
        }

        private static string Url(string ds, string fmt, IEnumerable<KeyValuePair<string, string>> p)
        {
            return Base + "/resource/" + Reglas.Datasets[ds] + "." + fmt + "?" + string.Join("&", p.Select(kv => kv.Key + "=" + Uri.EscapeDataString(kv.Value)));
        }

        public static string RangoAnio(int y)
        {
            return string.Format("fecha_corte between '{0}-01-01T00:00:00' and '{0}-12-31T23:59:59' AND periodo='Periodo Actual'", y);
        }

        public object Json(string ds, params string[] kv)
        {
            var p = new List<KeyValuePair<string, string>>();
            for (var i = 0; i < kv.Length; i += 2) p.Add(new KeyValuePair<string, string>(kv[i], kv[i + 1]));
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return ser.DeserializeObject(Encoding.UTF8.GetString(Get(Url(ds, "json", p))));
        }

        /// <summary>Consulta JSON de cualquier dataset de datos.gov.co por su id (datos complementarios).</summary>
        public object JsonId(string id, params string[] kv)
        {
            var q = new List<string>();
            for (var i = 0; i < kv.Length; i += 2) q.Add(kv[i] + "=" + Uri.EscapeDataString(kv[i + 1]));
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return ser.DeserializeObject(Encoding.UTF8.GetString(Get(Base + "/resource/" + id + ".json?" + string.Join("&", q))));
        }

        public long RowsUpdatedAt(string ds)
        {
            var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var d = (Dictionary<string, object>)ser.DeserializeObject(Encoding.UTF8.GetString(Get(Base + "/api/views/" + Reglas.Datasets[ds] + ".json")));
            object v;
            return d.TryGetValue("rowsUpdatedAt", out v) && v != null ? Convert.ToInt64(v) : 0;
        }

        /// <summary>Páginas CSV de un dataset/año filtradas por conceptos. Devuelve el texto de cada página (con caché en disco opcional).</summary>
        public IEnumerable<string> Paginas(string ds, int y, IEnumerable<string> conceptos, string columnas)
        {
            var lista = string.Join(",", conceptos.Select(c => "'" + c.Replace("'", "''") + "'"));
            for (var offset = 0; ; offset += Pagina)
            {
                var ruta = _cache == null ? null : Path.Combine(_cache, string.Format("{0}_{1}_{2}.csv", ds, y, offset));
                string txt;
                if (ruta != null && File.Exists(ruta)) txt = File.ReadAllText(ruta, Encoding.UTF8);
                else
                {
                    var p = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("$select", columnas),
                        new KeyValuePair<string, string>("$where", RangoAnio(y) + " AND concepto in (" + lista + ")"),
                        new KeyValuePair<string, string>("$order", ":id"),
                        new KeyValuePair<string, string>("$limit", Pagina.ToString()),
                        new KeyValuePair<string, string>("$offset", offset.ToString())
                    };
                    txt = Encoding.UTF8.GetString(Get(Url(ds, "csv", p)));
                    if (ruta != null) { File.WriteAllText(ruta + ".tmp", txt, new UTF8Encoding(false)); if (File.Exists(ruta)) File.Delete(ruta); File.Move(ruta + ".tmp", ruta); }
                }
                yield return txt;
                if (Csv.ContarFilas(txt) - 1 < Pagina) yield break;
            }
        }
    }

    internal static class Csv
    {
        public static int ContarFilas(string t)
        {
            var n = 0; var comillas = false;
            for (var i = 0; i < t.Length; i++)
            {
                var c = t[i];
                if (c == '"') comillas = !comillas;
                else if (c == '\n' && !comillas) n++;
            }
            if (t.Length > 0 && t[t.Length - 1] != '\n') n++;
            return n;
        }

        /// <summary>Lector CSV (RFC 4180): campos entre comillas, "" como comilla escapada y saltos de línea dentro de campos.</summary>
        public static IEnumerable<string[]> Filas(string t)
        {
            var campos = new List<string>();
            var sb = new StringBuilder();
            var comillas = false; var hayDato = false;
            for (var i = 0; i < t.Length; i++)
            {
                var c = t[i];
                if (comillas)
                {
                    if (c == '"') { if (i + 1 < t.Length && t[i + 1] == '"') { sb.Append('"'); i++; } else comillas = false; }
                    else sb.Append(c);
                    continue;
                }
                if (c == '"') { comillas = true; hayDato = true; }
                else if (c == ',') { campos.Add(sb.ToString()); sb.Clear(); hayDato = true; }
                else if (c == '\r') { }
                else if (c == '\n')
                {
                    if (hayDato || sb.Length > 0) { campos.Add(sb.ToString()); yield return campos.ToArray(); }
                    campos = new List<string>(); sb.Clear(); hayDato = false;
                }
                else { sb.Append(c); hayDato = true; }
            }
            if (hayDato || sb.Length > 0) { campos.Add(sb.ToString()); yield return campos.ToArray(); }
        }
    }

    // ------------------------------------------------------------------------------------------------ proceso principal

    internal class Etl
    {
        private class Car { public string Nit, Pe, Inst; public int Anio; public Dictionary<string, string> F = new Dictionary<string, string>(); }
        private class Emp
        {
            public string Nit, R, K, C, D, S, F, T, G; public int Ult;
            public SortedDictionary<int, long[]> Y = new SortedDictionary<int, long[]>();
            public Dictionary<string, string> P = new Dictionary<string, string>();
            public SortedDictionary<int, SortedDictionary<string, long>> Det = new SortedDictionary<int, SortedDictionary<string, long>>();
        }

        private readonly Opciones _o;
        private readonly Estado _e;
        private readonly Socrata _s;

        public Etl(Opciones o, Estado e) { _o = o; _e = e; _s = new Socrata(o.Cache); }

        private string Ruta(string f) { return Path.Combine(_o.Salida, f); }

        public void Ejecutar()
        {
            if (_o.SoloGrandes) { SoloGrandes(); return; }
            var sw = Stopwatch.StartNew();
            _e.Paso("Revisando la fuente", 1, "Consultando datos.gov.co");

            // Años: 2020..último año con datos
            List<int> years;
            if (_o.Anios.Count > 0) years = _o.Anios;
            else
            {
                var top = (object[])_s.Json("car", "$select", "max(fecha_corte) as m");
                var m = (string)((Dictionary<string, object>)top[0])["m"];
                years = Enumerable.Range(2020, int.Parse(m.Substring(0, 4)) - 2020 + 1).ToList();
            }
            _e.Anios = string.Join(",", years);

            long act = 0;
            foreach (var ds in Reglas.Datasets.Keys) act = Math.Max(act, _s.RowsUpdatedAt(ds));
            var fuenteAct = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(act).ToString("yyyy-MM-dd");
            _e.FuenteActualizado = fuenteAct;
            _e.Log("Años " + _e.Anios + " · fuente actualizada " + fuenteAct);

            var metaPath = Ruta("meta.json");
            if (File.Exists(metaPath) && !_o.Forzar)
            {
                try
                {
                    var prev = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(metaPath, Encoding.UTF8));
                    var prevAnios = string.Join(",", ((object[])prev["anios"]).Select(x => x.ToString()));
                    if ((string)prev["fuenteActualizado"] == fuenteAct && prevAnios == _e.Anios)
                    {
                        _e.Empresas = Convert.ToInt32(prev["empresas"]);
                        _e.Termina("sin_cambios", "Los datos publicados ya están al día con la fuente (" + fuenteAct + ").");
                        return;
                    }
                }
                catch (Exception) { /* meta ilegible: se regenera */ }
            }

            // 1) Conceptos exactos por dataset (unión de todos los años)
            _e.Paso("Revisando conceptos", 4);
            var exact = Reglas.Datasets.Keys.ToDictionary(k => k, k => new ConcurrentDictionary<string, string>());
            Paralelo(Reglas.Datasets.Keys.SelectMany(ds => years.Select(y => new Action(() =>
            {
                var res = (object[])_s.Json(ds, "$select", "concepto,count(*) as n", "$where", Socrata.RangoAnio(y), "$group", "concepto", "$limit", "5000");
                foreach (Dictionary<string, object> r in res)
                {
                    var c = (string)r["concepto"];
                    var k = Reglas.ClaveDe(ds, c);
                    if (k != null) exact[ds][c] = k;
                }
            }))).ToList());
            foreach (var ds in exact.Keys)
            {
                var faltan = Reglas.Mapa[ds].Values.Except(exact[ds].Values).ToList();
                _e.Log(ds + ": " + exact[ds].Count + " conceptos" + (faltan.Count > 0 ? "; sin datos: " + string.Join(",", faltan) : ""));
            }

            // 2) Carátula -> instancia elegida por (nit, año)
            _e.Paso("Descargando carátulas", 6);
            var cars = new ConcurrentDictionary<string, Car>();
            var hechas = 0;
            Paralelo(years.Select(y => new Action(() =>
            {
                foreach (var pag in _s.Paginas("car", y, exact["car"].Keys, "nit,codigo_instancia,punto_entrada,concepto,valor"))
                {
                    var primera = true; int iN = 0, iI = 0, iP = 0, iC = 0, iV = 0;
                    foreach (var f in Csv.Filas(pag))
                    {
                        if (primera) { Cols(f, out iN, out iI, out iP, out iC, out iV); primera = false; continue; }
                        var inst = f[iI];
                        var car = cars.GetOrAdd(inst, k => new Car { Nit = f[iN], Inst = inst, Pe = iP >= 0 ? f[iP] : "", Anio = y });
                        string clave;
                        if (exact["car"].TryGetValue(f[iC], out clave)) lock (car) { if (!car.F.ContainsKey(clave)) car.F[clave] = f[iV]; }
                    }
                }
                _e.Avance(6 + 14 * Interlocked.Increment(ref hechas) / years.Count, "Carátula " + y + " lista");
            })).ToList());

            var mejor = new Dictionary<string, Tuple<long, Car>>();
            foreach (var car in cars.Values)
            {
                var individual = car.Pe.StartsWith("10") || car.Pe.StartsWith("40") ? 1L : 0L;
                long num; long.TryParse(car.Inst, out num);
                var puntaje = individual * 10000000000000L + num;
                var clave = car.Nit + "|" + car.Anio;
                Tuple<long, Car> actual;
                if (!mejor.TryGetValue(clave, out actual) || puntaje > actual.Item1) mejor[clave] = Tuple.Create(puntaje, car);
            }
            var elegidas = mejor.Values.Select(t => t.Item2).ToList();
            _e.Log("Instancias elegidas: " + elegidas.Count);

            // 3) Valores por instancia (matriz densa: instancia x rubro)
            var claves = Reglas.Mapa.Where(kv => kv.Key != "car").SelectMany(kv => kv.Value.Values).Distinct().ToList();
            var kIdx = claves.Select((c, i) => new { c, i }).ToDictionary(x => x.c, x => x.i);
            var K = claves.Count;
            var instIdx = new Dictionary<string, int>();
            for (var i = 0; i < elegidas.Count; i++) instIdx[elegidas[i].Inst] = i;
            var vals = new double[(long)elegidas.Count * K];
            for (long i = 0; i < vals.LongLength; i++) vals[i] = double.NaN;
            var factor = elegidas.Select(c => Reglas.FactorMiles(c.F.ContainsKey("unidad") ? c.F["unidad"] : "MILES DE PESOS")).ToArray();

            _e.Paso("Descargando estados financieros", 20);
            var tareas = new[] { "bal", "eri", "flu" }.SelectMany(ds => years.Select(y => new { ds, y })).ToList();
            var fin = 0;
            Paralelo(tareas.Select(t => new Action(() =>
            {
                var mapa = exact[t.ds];
                var paginas = 0;
                foreach (var pag in _s.Paginas(t.ds, t.y, mapa.Keys, "nit,codigo_instancia,concepto,valor"))
                {
                    var primera = true; int iN = 0, iI = 0, iP = 0, iC = 0, iV = 0;
                    foreach (var f in Csv.Filas(pag))
                    {
                        if (primera) { Cols(f, out iN, out iI, out iP, out iC, out iV); primera = false; continue; }
                        int idx;
                        if (!instIdx.TryGetValue(f[iI], out idx)) continue;
                        string clave;
                        if (!mapa.TryGetValue(f[iC], out clave)) continue;
                        double v;
                        if (!double.TryParse(f[iV].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                        var slot = (long)idx * K + kIdx[clave];
                        if (double.IsNaN(vals[slot])) vals[slot] = v * factor[idx];
                    }
                    paginas++;
                    _e.Avance(20 + (int)(65.0 * (Volatile.Read(ref fin) + Math.Min(0.9, paginas / 12.0)) / tareas.Count), t.ds + " " + t.y + ": página " + paginas);
                }
                _e.Avance(20 + (int)(65.0 * Interlocked.Increment(ref fin) / tareas.Count), t.ds + " " + t.y + " listo");
            })).ToList());

            // 4) Métricas por empresa y año
            _e.Paso("Calculando indicadores", 86);
            var emps = new Dictionary<string, Emp>();
            for (var i = 0; i < elegidas.Count; i++)
            {
                var c = elegidas[i];
                Func<string, bool> tiene = k => !double.IsNaN(vals[(long)i * K + kIdx[k]]);
                Func<string, double> g = k => { var x = vals[(long)i * K + kIdx[k]]; return double.IsNaN(x) ? 0.0 : x; };
                if (!tiene("ing") && !tiene("act")) continue;

                var ub = tiene("ub") ? g("ub") : g("ing") - g("costo");
                var gop = g("gadm") + g("gven") + g("gdis") + g("ogas");
                var oing = g("oing") + g("ogan");
                var uop = tiene("uop") ? g("uop") : ub - gop + oing;
                var ebitda = uop + g("da");
                var deudaC = tiene("opfc") ? g("opfc") : g("pcor") + g("pcpnc");
                var deudaNc = tiene("opfnc") ? g("opfnc") : g("pnc");
                var deuda = deudaC + deudaNc;
                var ktn = (g("cxc") + g("inv") + g("aimp") + g("oanf")) - (g("cxp") + g("pimp") + g("pbe") + g("oprov"));

                var der = new Dictionary<string, double>();
                foreach (var k in claves) if (tiene(k)) der[k] = g(k);
                der["ub"] = ub; der["gop"] = gop; der["oing_net"] = oing; der["uop"] = uop; der["ebitda"] = ebitda; der["deuda"] = deuda; der["ktn"] = ktn;

                var fila = Reglas.Metricas.Select(m => (long)Math.Round(der.ContainsKey(m) ? der[m] : 0.0, MidpointRounding.ToEven)).ToArray();

                Emp e;
                if (!emps.TryGetValue(c.Nit, out e)) emps[c.Nit] = e = new Emp { Nit = c.Nit };
                e.Y[c.Anio] = fila;
                var det = new SortedDictionary<string, long>(StringComparer.Ordinal);
                foreach (var kv in der) { var r = (long)Math.Round(kv.Value, MidpointRounding.ToEven); if (kv.Value != 0.0) det[kv.Key] = r; }
                e.Det[c.Anio] = det;
                if (c.Anio >= e.Ult)
                {
                    e.Ult = c.Anio;
                    Func<string, string> f = k => (c.F.ContainsKey(k) ? c.F[k] : "").Trim();
                    e.R = f("razon"); e.K = Regex.Replace((c.F.ContainsKey("ciiu") ? c.F["ciiu"] : "").Split('-')[0], @"\D", "");
                    e.C = f("ciudad"); e.D = f("depto"); e.S = f("estado");
                    var fc = c.F.ContainsKey("fconst") ? c.F["fconst"] : ""; e.F = (fc.Length > 10 ? fc.Substring(0, 10) : fc).Trim();
                    e.T = f("tipo"); e.G = c.Pe.StartsWith("10") || c.Pe.StartsWith("20") ? "1" : "2";
                    e.P = new Dictionary<string, string> { { "objeto", f("objeto") }, { "clase", f("clase") }, { "rf", f("rf") }, { "rfop", f("rfop") }, { "pe", c.Pe } };
                }
            }
            var lista = emps.Values.Where(e => !string.IsNullOrEmpty(e.R)).OrderBy(e => e.Nit, StringComparer.Ordinal).ToList();
            _e.Empresas = lista.Count;

            // 5) Reparar textos dañados (U+FFFD) con vocabulario de las propias palabras limpias + títulos CIIU
            _e.Paso("Reparando textos", 90);
            var textos = new List<string>();
            foreach (var e in lista) { textos.Add(e.P.ContainsKey("objeto") ? e.P["objeto"] : ""); textos.AddRange(new[] { e.R, e.T, e.S, e.C, e.D }); }
            var ciiu = Ruta("ciiu.json");
            if (File.Exists(ciiu))
            {
                var cj = (Dictionary<string, object>)new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(File.ReadAllText(ciiu, Encoding.UTF8));
                textos.AddRange(((Dictionary<string, object>)cj["clases"]).Values.Select(x => (string)x));
                textos.AddRange(((Dictionary<string, object>)cj["grupos"]).Values.Select(x => (string)x));
                textos.AddRange(((Dictionary<string, object>)cj["divisiones"]).Values.Select(x => (string)((Dictionary<string, object>)x)["n"]));
            }
            var vocab = Textos.Vocabulario(textos);
            foreach (var e in lista)
            {
                e.R = Textos.Repara(e.R, vocab); e.T = Textos.Repara(e.T, vocab); e.S = Textos.Repara(e.S, vocab);
                e.C = Textos.Repara(e.C, vocab); e.D = Textos.Repara(e.D, vocab);
                foreach (var k in new[] { "objeto", "clase", "rf", "rfop" }) if (e.P.ContainsKey(k)) e.P[k] = Textos.Repara(e.P[k], vocab);
            }

            // 6) Publicación atómica
            _e.Paso("Publicando archivos", 94);
            Publicar(lista, years, fuenteAct);
            Complementos(lista, years);
            _e.Termina("ok", string.Format("Datos actualizados: {0:N0} empresas, años {1}–{2}, fuente {3}. Duración {4:0} min.", lista.Count, years.First(), years.Last(), fuenteAct, sw.Elapsed.TotalMinutes));
        }

        private static void Cols(string[] f, out int iN, out int iI, out int iP, out int iC, out int iV)
        {
            Func<string, int> ix = n => Array.IndexOf(f, n);
            iN = ix("nit"); iI = ix("codigo_instancia"); iP = ix("punto_entrada"); iC = ix("concepto"); iV = ix("valor");
            if (iI < 0 || iC < 0 || iV < 0) throw new InvalidDataException("La respuesta CSV de datos.gov.co no trae las columnas esperadas.");
        }

        private void Paralelo(List<Action> acciones)
        {
            Exception primero = null;
            Parallel.ForEach(acciones, new ParallelOptions { MaxDegreeOfParallelism = _o.Workers }, a =>
            {
                try { if (primero == null) a(); }
                catch (Exception ex) { Interlocked.CompareExchange(ref primero, ex, null); }
            });
            if (primero != null) throw new AggregateException(primero);
        }

        // ---------------------------------------------------------------- salida

        private static string Esc(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); break;
                }
            }
            return sb.Append('"').ToString();
        }

        private void Publicar(List<Emp> lista, List<int> years, string fuenteAct)
        {
            var version = DateTime.Now.ToString("yyyyMMddHHmm");
            var detNombre = "detalle." + version + ".bin";
            var idxNombre = "detalle." + version + ".idx.json";

            // detalle.{version}.bin: un bloque gzip (JSON) por empresa + índice NIT -> [offset, longitud]
            var idx = new StringBuilder("{");
            using (var f = new FileStream(Ruta(detNombre + ".tmp"), FileMode.Create, FileAccess.Write))
            {
                var primero = true;
                foreach (var e in lista)
                {
                    var j = new StringBuilder("{\"p\":{");
                    j.Append(string.Join(",", e.P.Select(kv => Esc(kv.Key) + ":" + Esc(kv.Value))));
                    j.Append("},\"y\":{");
                    j.Append(string.Join(",", e.Det.Select(a => "\"" + a.Key + "\":{" + string.Join(",", a.Value.Select(kv => Esc(kv.Key) + ":" + kv.Value)) + "}")));
                    j.Append("}}");
                    byte[] blob;
                    using (var ms = new MemoryStream())
                    {
                        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true)) { var b = new UTF8Encoding(false).GetBytes(j.ToString()); gz.Write(b, 0, b.Length); }
                        blob = ms.ToArray();
                    }
                    if (!primero) idx.Append(',');
                    primero = false;
                    idx.Append(Esc(e.Nit)).Append(":[").Append(f.Position).Append(',').Append(blob.Length).Append(']');
                    f.Write(blob, 0, blob.Length);
                }
            }
            idx.Append('}');
            File.WriteAllText(Ruta(idxNombre + ".tmp"), idx.ToString(), new UTF8Encoding(false));

            // empresas.bin (formato binario; ver Services/PulsoService.Cargar)
            using (var f = new FileStream(Ruta("empresas.bin.tmp"), FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(f, Encoding.UTF8))
            {
                w.Write(Encoding.ASCII.GetBytes("BOBS"));
                w.Write(1); w.Write(years.Count); w.Write(Reglas.Metricas.Length); w.Write(lista.Count);
                foreach (var y in years) w.Write(y);
                Action<string> ws = s => { var b = Encoding.UTF8.GetBytes(s ?? ""); var n = Math.Min(65535, b.Length); w.Write((ushort)n); w.Write(b, 0, n); };
                foreach (var e in lista)
                {
                    ws(e.Nit); ws(e.R); ws(e.K); ws(e.C); ws(e.D); ws(e.S); ws(e.F); ws(e.T); ws(e.G);
                    ushort mask = 0;
                    for (var i = 0; i < years.Count; i++) if (e.Y.ContainsKey(years[i])) mask |= (ushort)(1 << i);
                    w.Write(mask);
                    for (var i = 0; i < years.Count; i++) if (e.Y.ContainsKey(years[i])) foreach (var v in e.Y[years[i]]) w.Write(v);
                }
            }

            var meta = "{\"fuente\":" + Esc("Superintendencia de Sociedades - datos.gov.co (Estados financieros NIIF)") +
                ",\"datasets\":{" + string.Join(",", Reglas.Datasets.Select(kv => Esc(kv.Key) + ":" + Esc(kv.Value))) + "}" +
                ",\"fuenteActualizado\":" + Esc(fuenteAct) + ",\"generado\":" + Esc(DateTime.Now.ToString("yyyy-MM-dd")) + ",\"unidad\":\"Miles de COP\"" +
                ",\"anios\":[" + string.Join(",", years) + "],\"empresas\":" + lista.Count +
                ",\"metricas\":[" + string.Join(",", Reglas.Metricas.Select(Esc)) + "]" +
                ",\"detalle\":" + Esc(detNombre) + ",\"indice\":" + Esc(idxNombre) + "}";
            File.WriteAllText(Ruta("meta.json.tmp"), meta, new UTF8Encoding(false));

            // Orden de publicación: archivos de detalle (nombres versionados) -> meta.json -> empresas.bin (lo que dispara la recarga del sitio)
            Mover(Ruta(detNombre + ".tmp"), Ruta(detNombre));
            Mover(Ruta(idxNombre + ".tmp"), Ruta(idxNombre));
            Mover(Ruta("meta.json.tmp"), Ruta("meta.json"));
            Mover(Ruta("empresas.bin.tmp"), Ruta("empresas.bin"));

            // limpieza de versiones anteriores y del formato antiguo (si siguen en uso se reintentará en la próxima ejecución)
            foreach (var viejo in Directory.GetFiles(_o.Salida, "detalle*").Where(p => !p.EndsWith(detNombre) && !p.EndsWith(idxNombre)))
            {
                try { File.Delete(viejo); } catch (IOException) { _e.Log("No se pudo borrar " + Path.GetFileName(viejo) + " (en uso); se limpiará la próxima vez."); }
            }
            var antiguo = Ruta("empresas.json");
            if (File.Exists(antiguo)) { try { File.Delete(antiguo); } catch (IOException) { } }
        }

        // ---------------------------------------------------------------- datos complementarios (no críticos)

        private void Complementos(List<Emp> lista, List<int> years)
        {
            _e.Paso("Contratación estatal y TRM", 96);
            try { Contratos(lista, years); } catch (Exception ex) { _e.Log("SECOP II no disponible (se conserva el archivo anterior): " + ex.GetBaseException().Message); }
            try { Trm(years); } catch (Exception ex) { _e.Log("TRM no disponible (se conserva el archivo anterior): " + ex.GetBaseException().Message); }
            try { Grandes(new HashSet<string>(lista.Select(e => e.Nit))); } catch (Exception ex) { _e.Log("10.000 más grandes no disponible (se conserva el archivo anterior): " + ex.GetBaseException().Message); }
        }

        /// <summary>Contratos con el Estado (SECOP II) por NIT de proveedor, de los últimos 3 años calendario.</summary>
        private void Contratos(List<Emp> lista, List<int> years)
        {
            var nits = new HashSet<string>(lista.Select(e => e.Nit));
            var desde = (years.Last() - 2) + "-01-01T00:00:00";
            var res = new SortedDictionary<string, long[]>(StringComparer.Ordinal);
            const double TopeValor = 2e13;   // 20 billones de COP por proveedor: por encima es casi seguro un error de digitación en la fuente
            for (var offset = 0; ; offset += 50000)
            {
                var pag = (object[])_s.JsonId("jbjy-vk9h", "$select", "documento_proveedor,count(*) as n,sum(valor_del_contrato) as v",
                    "$where", "fecha_de_firma >= '" + desde + "' AND tipodocproveedor='NIT'", "$group", "documento_proveedor",
                    "$order", "documento_proveedor", "$limit", "50000", "$offset", offset.ToString());
                foreach (Dictionary<string, object> r in pag)
                {
                    if (!r.ContainsKey("documento_proveedor") || r["documento_proveedor"] == null) continue;   // grupos sin NIT
                    var nit = Convert.ToString(r["documento_proveedor"]).Trim();
                    if (!nits.Contains(nit)) continue;
                    double v = 0; if (r.ContainsKey("v") && r["v"] != null) double.TryParse(Convert.ToString(r["v"]), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
                    res[nit] = new[] { r.ContainsKey("n") ? Convert.ToInt64(r["n"]) : 0L, v > 0 && v <= TopeValor ? (long)v : 0L };
                }
                if (pag.Length < 50000) break;
            }
            var sb = new StringBuilder("{\"fuente\":" + Esc("SECOP II - Contratos Electrónicos (datos.gov.co, Agencia Nacional de Contratación Pública)") +
                ",\"desde\":" + Esc(desde.Substring(0, 10)) + ",\"generado\":" + Esc(DateTime.Now.ToString("yyyy-MM-dd")) + ",\"d\":{");
            sb.Append(string.Join(",", res.Select(kv => Esc(kv.Key) + ":[" + kv.Value[0] + "," + kv.Value[1] + "]")));
            sb.Append("}}");
            File.WriteAllText(Ruta("contratos.json.tmp"), sb.ToString(), new UTF8Encoding(false));
            Mover(Ruta("contratos.json.tmp"), Ruta("contratos.json"));
            _e.Log("SECOP II: " + res.Count.ToString("N0") + " empresas del universo con contratos desde " + desde.Substring(0, 4));
        }

        /// <summary>TRM promedio y de cierre de cada año (Superintendencia Financiera).</summary>
        private void Trm(List<int> years)
        {
            var prom = ((object[])_s.JsonId("32sa-8pi3", "$select", "date_extract_y(vigenciadesde) as y,avg(valor) as trm", "$group", "y", "$order", "y"))
                .Cast<Dictionary<string, object>>().ToDictionary(r => int.Parse(Convert.ToString(r["y"])), r => double.Parse(Convert.ToString(r["trm"]), CultureInfo.InvariantCulture));
            var partes = new List<string>();
            foreach (var y in years)
            {
                double cierre = 0;
                var ult = (object[])_s.JsonId("32sa-8pi3", "$select", "valor", "$where", "vigenciadesde <= '" + y + "-12-31T23:59:59' AND vigenciadesde >= '" + y + "-12-01T00:00:00'",
                    "$order", "vigenciadesde DESC", "$limit", "1");
                if (ult.Length > 0) double.TryParse(Convert.ToString(((Dictionary<string, object>)ult[0])["valor"]), NumberStyles.Float, CultureInfo.InvariantCulture, out cierre);
                double p; if (!prom.TryGetValue(y, out p)) continue;
                partes.Add("\"" + y + "\":{\"prom\":" + p.ToString("0.##", CultureInfo.InvariantCulture) + ",\"cierre\":" + cierre.ToString("0.##", CultureInfo.InvariantCulture) + "}");
            }
            var json = "{\"fuente\":" + Esc("Superintendencia Financiera de Colombia - TRM (datos.gov.co 32sa-8pi3)") + ",\"generado\":" + Esc(DateTime.Now.ToString("yyyy-MM-dd")) + ",\"trm\":{" + string.Join(",", partes) + "}}";
            File.WriteAllText(Ruta("macro.json.tmp"), json, new UTF8Encoding(false));
            Mover(Ruta("macro.json.tmp"), Ruta("macro.json"));
            _e.Log("TRM: " + partes.Count + " años");
        }


        // ---------------------------------------------------------------- «10.000 empresas más grandes» (empresas que no reportan NIIF a la Supersociedades)

        private const string GrandesId = "6cat-2gcs";

        /// <summary>Solo regenera grandes.json: toma los NIT que ya hay en empresas.bin y no vuelve a descargar los estados financieros.</summary>
        private void SoloGrandes()
        {
            _e.Paso("Empresas grandes fuera de los NIIF", 5, "Leyendo empresas.bin");
            var bin = Ruta("empresas.bin");
            if (!File.Exists(bin)) throw new FileNotFoundException("No hay empresas.bin en " + _o.Salida + ". Ejecute primero la actualización completa.");
            var nits = NitsDeEmpresasBin(bin);
            _e.Log("empresas.bin: " + nits.Count.ToString("N0") + " NIT con estados NIIF");
            Grandes(nits);
            _e.Termina("ok", "grandes.json actualizado.");
        }

        private static HashSet<string> NitsDeEmpresasBin(string ruta)
        {
            var nits = new HashSet<string>();
            using (var f = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var r = new BinaryReader(f, Encoding.UTF8))
            {
                if (new string(r.ReadChars(4)) != "BOBS") throw new InvalidDataException("empresas.bin no es un archivo válido.");
                r.ReadInt32();
                int nA = r.ReadInt32(), nM = r.ReadInt32(), nE = r.ReadInt32();
                for (var i = 0; i < nA; i++) r.ReadInt32();
                Func<string> leer = () => { var n = r.ReadUInt16(); return n == 0 ? "" : Encoding.UTF8.GetString(r.ReadBytes(n)); };
                for (var k = 0; k < nE; k++)
                {
                    nits.Add(leer());
                    for (var j = 0; j < 8; j++) leer();                 // razón, CIIU, ciudad, departamento, estado, fecha, tipo, grupo
                    var mask = r.ReadUInt16(); var anios = 0;
                    for (var i = 0; i < 16; i++) if ((mask >> i & 1) != 0) anios++;
                    r.BaseStream.Seek((long)anios * nM * 8, SeekOrigin.Current);
                }
            }
            return nits;
        }

        /// <summary>
        /// Descarga «10.000 Empresas más Grandes del País» (Supersociedades, datos.gov.co; cifras en billones de COP con 2 decimales) y guarda en
        /// grandes.json las empresas cuyo NIT NO está en los estados NIIF (p. ej. las vigiladas por la Superfinanciera, como Ecopetrol).
        /// Solo trae ingresos, ganancia, activos, pasivos y patrimonio: la ficha es reducida y no entra en los análisis por sector.
        /// </summary>
        private void Grandes(HashSet<string> nitsNiif)
        {
            _e.Paso("Empresas grandes de otras superintendencias", 50, "Descargando " + GrandesId);
            var filas = new List<Dictionary<string, object>>();
            for (var offset = 0; ; offset += 50000)
            {
                var pag = (object[])_s.JsonId(GrandesId, "$order", "a_o_de_corte,nit", "$limit", "50000", "$offset", offset.ToString());
                foreach (Dictionary<string, object> r in pag) filas.Add(r);
                if (pag.Length < 50000) break;
            }
            Func<Dictionary<string, object>, string, string> S = (r, k) => r.ContainsKey(k) && r[k] != null ? Convert.ToString(r[k], CultureInfo.InvariantCulture).Trim() : "";
            // «$ 100.59» (billones de COP) -> miles de COP; los negativos pueden venir como «-0.31» o «(0.31)»
            Func<string, long> Miles = t =>
            {
                var neg = t.Contains("(") || t.Contains("-");
                var n = Regex.Replace(t, @"[^0-9.]", "");
                double v;
                if (n.Length == 0 || !double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return 0L;
                return (long)Math.Round((neg ? -v : v) * 1e9);
            };
            var porNit = new SortedDictionary<string, Tuple<int, Dictionary<string, object>>>(StringComparer.Ordinal);
            var cifras = new Dictionary<string, SortedDictionary<int, long[]>>();
            foreach (var r in filas)
            {
                var nit = Regex.Replace(S(r, "nit").Split('.')[0], @"\D", "");
                double ya;
                if (nit.Length < 5 || nitsNiif.Contains(nit) || !double.TryParse(S(r, "a_o_de_corte"), NumberStyles.Float, CultureInfo.InvariantCulture, out ya)) continue;
                var anio = (int)ya;
                if (anio < 2020) continue;
                SortedDictionary<int, long[]> ys;
                if (!cifras.TryGetValue(nit, out ys)) cifras[nit] = ys = new SortedDictionary<int, long[]>();
                ys[anio] = new[] { Miles(S(r, "ingresos_operacionales")), Miles(S(r, "ganancia_p_rdida")), Miles(S(r, "total_activos")), Miles(S(r, "total_pasivos")), Miles(S(r, "total_patrimonio")) };
                Tuple<int, Dictionary<string, object>> previo;
                if (!porNit.TryGetValue(nit, out previo) || anio >= previo.Item1) porNit[nit] = Tuple.Create(anio, r);   // textos del último año
            }
            var sb = new StringBuilder("{\"fuente\":" + Esc("Superintendencia de Sociedades - 10.000 Empresas más Grandes del País (datos.gov.co " + GrandesId + ")") +
                ",\"licencia\":\"CC BY-SA 4.0\",\"unidad\":\"Miles de COP\",\"precision\":\"Billones de COP con 2 decimales\",\"generado\":" + Esc(DateTime.Now.ToString("yyyy-MM-dd")) + ",\"d\":[");
            var primero = true; var porSup = new Dictionary<string, int>();
            foreach (var kv in porNit)
            {
                var r = kv.Value.Item2;
                var ciiu = Regex.Replace(S(r, "ciiu").Split('.')[0], @"\D", "");
                if (ciiu.Length > 0 && ciiu.Length < 4) ciiu = ciiu.PadLeft(4, '0');
                var sup = S(r, "supervisor").ToUpperInvariant();
                int c; porSup.TryGetValue(sup, out c); porSup[sup] = c + 1;
                if (!primero) sb.Append(',');
                primero = false;
                sb.Append("{\"n\":").Append(Esc(kv.Key)).Append(",\"r\":").Append(Esc(S(r, "raz_n_social"))).Append(",\"k\":").Append(Esc(ciiu))
                  .Append(",\"c\":").Append(Esc(S(r, "ciudad_domicilio"))).Append(",\"e\":").Append(Esc(S(r, "departamento_domicilio")))
                  .Append(",\"s\":").Append(Esc(sup)).Append(",\"m\":").Append(Esc(S(r, "macrosector"))).Append(",\"y\":{")
                  .Append(string.Join(",", cifras[kv.Key].Select(y => "\"" + y.Key + "\":[" + string.Join(",", y.Value) + "]"))).Append("}}");
            }
            sb.Append("]}");
            File.WriteAllText(Ruta("grandes.json.tmp"), sb.ToString(), new UTF8Encoding(false));
            Mover(Ruta("grandes.json.tmp"), Ruta("grandes.json"));
            _e.Log("10.000 más grandes: " + porNit.Count.ToString("N0") + " empresas fuera de los NIIF (" + string.Join(", ", porSup.OrderByDescending(x => x.Value).Select(x => x.Key + " " + x.Value)) + ")");
        }

        /// <summary>Reemplaza el destino con reintentos (el sitio puede tener el archivo abierto un instante).</summary>
        private static void Mover(string origen, string destino)
        {
            for (var i = 0; ; i++)
            {
                try
                {
                    if (File.Exists(destino)) File.Delete(destino);
                    File.Move(origen, destino);
                    return;
                }
                catch (IOException) when (i < 20) { Thread.Sleep(1500); }
                catch (UnauthorizedAccessException) when (i < 20) { Thread.Sleep(1500); }
            }
        }
    }
}
