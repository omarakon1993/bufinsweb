using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using Newtonsoft.Json;

namespace bufinsweb.Services
{
    /// <summary>
    /// Analítica propia y ANÓNIMA del sitio público (Observatorio y su embudo de contacto). Nunca guarda IP, cookies, User-Agent,
    /// textos escritos por la persona ni datos personales. Tiene dos capas:
    ///
    ///  1. Contadores agregados día | tipo | clave (App_Data/observatorio/uso/yyyy-MM.json) — formato histórico, lo lee el portal.
    ///  2. Registro de eventos anónimos, una línea JSON por evento (App_Data/observatorio/uso/eventos/yyyy-MM-dd.jsonl), con:
    ///     · un identificador de visitante que se RENUEVA CADA DÍA (HMAC de IP + navegador con una sal diaria que se descarta):
    ///       permite contar visitantes y recorridos dentro del día, pero no reconocer a la misma persona otro día ni volver a la IP;
    ///     · contexto grueso: tipo de dispositivo, familia de navegador y sistema, idioma, zona horaria, origen de la visita
    ///       (directo / buscador / red social / otro sitio) y campaña UTM.
    ///     Se borra pasado el período de retención (ObsUsoRetencionDias, 180 por defecto).
    ///
    /// Los bots (buscadores, rastreadores de IA, previsualizadores) no entran en el registro de eventos: solo se cuentan por familia.
    /// Todo vive en memoria y se vuelca cada minuto (y al cerrar la aplicación). La analítica nunca debe afectar al sitio.
    /// </summary>
    public static class UsoService
    {
        /// <summary>Tipos de evento válidos (lista blanca). Los que empiezan por «vista_» los genera el servidor al servir la página.</summary>
        public static readonly HashSet<string> Tipos = new HashSet<string>
        {
            // páginas
            "vista_home", "vista_empresa", "vista_sector", "vista_ranking", "vista_comparar", "vista_sectores", "vista_contacto",
            // acciones
            "busqueda_sel", "cta_click", "export_csv", "export_xlsx", "informe_pdf", "filtro_sector", "comparar", "unidad",
            "copiar_enlace", "imprimir", "outbound", "contacto_enviado",
            // comportamiento y salud del sitio
            "perm", "error_404", "throttle_429", "bot"
        };

        /// <summary>Tipos que puede enviar el navegador (los demás solo los registra el servidor).</summary>
        public static readonly HashSet<string> TiposCliente = new HashSet<string>
        {
            "busqueda_sel", "cta_click", "filtro_sector", "comparar", "unidad", "copiar_enlace", "imprimir", "outbound", "perm"
        };

        private static readonly ConcurrentDictionary<string, long> Pendiente = new ConcurrentDictionary<string, long>();
        private static readonly ConcurrentQueue<KeyValuePair<string, string>> ColaEventos = new ConcurrentQueue<KeyValuePair<string, string>>();
        private static readonly ConcurrentDictionary<string, int> PorVisitante = new ConcurrentDictionary<string, int>();
        private static readonly object LockArchivo = new object();
        private static readonly object LockSal = new object();
        private static Timer _timer;
        private static string _salFecha;
        private static byte[] _sal;
        private static string _ultimaPurga = "";
        private static long _eventosDia;
        private static string _eventosDiaFecha = "";

        private const int MaxEventosPorVisitanteDia = 400;
        private const int MaxEventosPorDia = 200000;

        private static readonly Regex Bots = new Regex("bot|crawl|spider|slurp|preview|facebookexternalhit|curl|wget|python|monitor|headless|lighthouse|gptbot|claude|perplexity|bytespider|ccbot|semrush|ahrefs|mj12|petalbot|scrapy|httpclient|okhttp|axios|go-http|java/", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ClaveOk = new Regex("^[a-z0-9\\-_.]{1,40}$", RegexOptions.Compiled);
        private static readonly Regex DetalleNo = new Regex("[^a-z0-9 ,\\-_.|/:]", RegexOptions.Compiled);
        private static readonly Regex NombreNo = new Regex("[^\\p{L}\\p{N} .,&\\-/()']", RegexOptions.Compiled);

        // (patrón, familia) en orden de prioridad
        private static readonly KeyValuePair<string, string>[] FamiliasBot =
        {
            new KeyValuePair<string,string>("gptbot|oai-searchbot|chatgpt", "ia-openai"),
            new KeyValuePair<string,string>("claudebot|claude-web|anthropic", "ia-anthropic"),
            new KeyValuePair<string,string>("perplexity", "ia-perplexity"),
            new KeyValuePair<string,string>("bytespider|ccbot|google-extended|cohere|amazonbot", "ia-otro"),
            new KeyValuePair<string,string>("googlebot|adsbot-google|mediapartners|google-inspectiontool|apis-google", "google"),
            new KeyValuePair<string,string>("bingbot|msnbot|bingpreview", "bing"),
            new KeyValuePair<string,string>("duckduckbot|yandex|baiduspider|applebot|seznam|sogou|ecosia", "buscador-otro"),
            new KeyValuePair<string,string>("facebookexternalhit|facebot|meta-external", "facebook"),
            new KeyValuePair<string,string>("linkedinbot", "linkedin"),
            new KeyValuePair<string,string>("twitterbot|x-bot", "x-twitter"),
            new KeyValuePair<string,string>("whatsapp|telegram|slackbot|discord|skypeuripreview", "mensajeria"),
            new KeyValuePair<string,string>("semrush|ahrefs|mj12|dotbot|petalbot|serpstat|screaming", "seo"),
            new KeyValuePair<string,string>("lighthouse|pagespeed|gtmetrix|uptime|pingdom|monitor", "monitoreo")
        };

        public static void Iniciar()
        {
            if (_timer == null) _timer = new Timer(_ => Vaciar(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public static bool EsBot(string userAgent) { return string.IsNullOrEmpty(userAgent) || Bots.IsMatch(userAgent); }

        private static string Hoy() { return DateTime.UtcNow.AddHours(-5).ToString("yyyy-MM-dd"); }   // hora de Colombia

        // ------------------------------------------------------------------ contadores (formato histórico)

        public static void Contar(string tipo, string clave)
        {
            if (!Tipos.Contains(tipo)) return;
            clave = LimpiarClave(clave);
            if (Pendiente.Count > 20000) return;   // protección ante abuso: no crece sin límite
            var k = Hoy() + "|" + tipo + "|" + clave;
            Pendiente.AddOrUpdate(k, 1, (_, v) => v + 1);
        }

        private static string LimpiarClave(string clave)
        {
            clave = (clave ?? "").Trim().ToLowerInvariant();
            if (clave.Length > 0 && !ClaveOk.IsMatch(clave)) clave = clave.Length > 40 ? "" : Regex.Replace(clave, "[^a-z0-9\\-_.]", "");
            if (clave.Length > 40) clave = clave.Substring(0, 40);
            return clave;
        }

        /// <summary>Cuenta una visita de un bot por familia (google, bing, ia-openai…) y tipo de página. No entra en el registro de eventos.</summary>
        public static void ContarBot(string userAgent, string pagina)
        {
            try
            {
                var ua = userAgent ?? "";
                var familia = "otro";
                foreach (var f in FamiliasBot) if (Regex.IsMatch(ua, f.Key, RegexOptions.IgnoreCase)) { familia = f.Value; break; }
                Contar("bot", familia + "." + LimpiarClave(pagina ?? ""));
            }
            catch (Exception) { }
        }

        // ------------------------------------------------------------------ registro de eventos anónimos

        /// <summary>
        /// Registra un evento anónimo. <paramref name="conOrigen"/> solo en vistas de página servidas por el servidor (en las
        /// llamadas del navegador el Referer es la propia página y no aporta).
        /// </summary>
        public static void Registrar(HttpRequestBase req, string tipo, string clave = "", string detalle = "", string nombre = "",
            int ms = 0, int scroll = 0, string tz = "", int ancho = 0, bool conOrigen = false)
        {
            try
            {
                if (req == null || !Tipos.Contains(tipo) || EsBot(req.UserAgent)) return;
                Contar(tipo == "perm" ? "perm" : tipo, tipo == "perm" ? CuboPermanencia(ms) : clave);

                var fecha = Hoy();
                if (_eventosDiaFecha != fecha) { _eventosDiaFecha = fecha; Interlocked.Exchange(ref _eventosDia, 0); }
                if (Interlocked.Increment(ref _eventosDia) > MaxEventosPorDia) return;

                var ip = req.UserHostAddress ?? "";
                var ua = req.UserAgent ?? "";
                var v = Visitante(ip, ua);
                if (PorVisitante.Count > 200000) PorVisitante.Clear();
                if (PorVisitante.AddOrUpdate(fecha + "|" + v, 1, (_, cuenta) => cuenta + 1) > MaxEventosPorVisitanteDia) return;

                var e = new Dictionary<string, object>();
                e["h"] = DateTime.UtcNow.AddHours(-5).ToString("HH:mm:ss");
                e["v"] = v;
                e["t"] = tipo;
                var k = LimpiarClave(clave); if (k.Length > 0) e["k"] = k;
                var x = LimpiarDetalle(detalle); if (x.Length > 0) e["x"] = x;
                var n = LimpiarNombre(nombre); if (n.Length > 0) e["n"] = n;
                e["d"] = Dispositivo(ua, ancho);
                var nav = Navegador(ua); e["b"] = nav;
                e["s"] = SistemaOperativo(ua);
                var l = Idioma(req.UserLanguages); if (l.Length > 0) e["l"] = l;
                var z = LimpiarZona(tz); if (z.Length > 0) e["z"] = z;
                if (conOrigen)
                {
                    string cat, host; Origen(req, out cat, out host);
                    e["r"] = cat;
                    if (host.Length > 0) e["rh"] = host;
                    var u = Utm(req); if (u.Length > 0) e["u"] = u;
                }
                if (ms > 0) e["ms"] = Math.Min(ms, 3600000);
                if (scroll > 0) e["sc"] = Math.Min(scroll, 100);

                ColaEventos.Enqueue(new KeyValuePair<string, string>(fecha, JsonConvert.SerializeObject(e)));
            }
            catch (Exception) { /* la analítica nunca debe afectar al sitio */ }
        }

        private static string CuboPermanencia(int ms)
        {
            var s = ms / 1000;
            return s < 10 ? "menos-10s" : s < 30 ? "10-30s" : s < 60 ? "30-60s" : s < 180 ? "1-3min" : "mas-3min";
        }

        private static string LimpiarDetalle(string d)
        {
            d = DetalleNo.Replace((d ?? "").Trim().ToLowerInvariant(), "");
            return d.Length > 100 ? d.Substring(0, 100) : d;
        }

        private static string LimpiarNombre(string n)
        {
            n = NombreNo.Replace((n ?? "").Trim(), "");
            return n.Length > 60 ? n.Substring(0, 60) : n;
        }

        private static string LimpiarZona(string z)
        {
            z = (z ?? "").Trim();
            return Regex.IsMatch(z, "^[A-Za-z_]+(/[A-Za-z_\\-+0-9]+){0,2}$") && z.Length <= 40 ? z : "";
        }

        // ---- identificador de visitante que se renueva a diario

        private static byte[] SalDelDia()
        {
            var fecha = Hoy();
            if (_sal != null && _salFecha == fecha) return _sal;
            lock (LockSal)
            {
                if (_sal != null && _salFecha == fecha) return _sal;
                var ruta = Path.Combine(CarpetaUso, ".sal");
                try
                {
                    if (File.Exists(ruta))
                    {
                        var partes = File.ReadAllText(ruta, Encoding.UTF8).Split('\n');
                        if (partes.Length >= 2 && partes[0].Trim() == fecha) { _sal = Convert.FromBase64String(partes[1].Trim()); _salFecha = fecha; return _sal; }
                    }
                }
                catch (Exception) { }
                var nueva = new byte[32];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(nueva);
                try
                {
                    Directory.CreateDirectory(CarpetaUso);
                    File.WriteAllText(ruta, fecha + "\n" + Convert.ToBase64String(nueva), new UTF8Encoding(false));   // sobrescribe la sal del día anterior: ya no se puede relacionar
                }
                catch (Exception) { }
                _sal = nueva; _salFecha = fecha;
                return _sal;
            }
        }

        private static string Visitante(string ip, string ua)
        {
            using (var h = new HMACSHA256(SalDelDia()))
            {
                var b = h.ComputeHash(Encoding.UTF8.GetBytes(ip + "|" + ua));
                return BitConverter.ToString(b, 0, 6).Replace("-", "").ToLowerInvariant();
            }
        }

        // ---- contexto grueso (nunca se guarda el User-Agent)

        private static string Dispositivo(string ua, int ancho)
        {
            if (ancho > 0) return ancho < 768 ? "m" : ancho < 1100 ? "t" : "d";
            if (Regex.IsMatch(ua, "iPad|Tablet|Nexus 7|SM-T", RegexOptions.IgnoreCase)) return "t";
            return Regex.IsMatch(ua, "Mobi|Android|iPhone|iPod", RegexOptions.IgnoreCase) ? "m" : "d";
        }

        private static string Navegador(string ua)
        {
            if (Regex.IsMatch(ua, "Edg/|EdgA/|EdgiOS/")) return "edge";
            if (Regex.IsMatch(ua, "OPR/|Opera")) return "opera";
            if (Regex.IsMatch(ua, "SamsungBrowser")) return "samsung";
            if (Regex.IsMatch(ua, "Firefox/|FxiOS")) return "firefox";
            if (Regex.IsMatch(ua, "Chrome/|CriOS")) return "chrome";
            if (Regex.IsMatch(ua, "Safari/")) return "safari";
            return "otro";
        }

        private static string SistemaOperativo(string ua)
        {
            if (Regex.IsMatch(ua, "Windows")) return "windows";
            if (Regex.IsMatch(ua, "Android")) return "android";
            if (Regex.IsMatch(ua, "iPhone|iPad|iPod")) return "ios";
            if (Regex.IsMatch(ua, "Mac OS X|Macintosh")) return "macos";
            if (Regex.IsMatch(ua, "Linux|X11|CrOS")) return "linux";
            return "otro";
        }

        private static string Idioma(string[] idiomas)
        {
            var i = idiomas != null && idiomas.Length > 0 ? (idiomas[0] ?? "") : "";
            i = i.Split(';')[0].Trim().ToLowerInvariant();
            if (i.Length > 5 || !Regex.IsMatch(i, "^[a-z]{2}(-[a-z0-9]{2,4})?$")) return "";
            return i.Length > 2 ? i.Substring(0, 2) : i;
        }

        private static readonly string[] Buscadores = { "google.", "bing.", "duckduckgo.", "yahoo.", "ecosia.", "yandex.", "baidu.", "brave.", "startpage.", "perplexity.", "chatgpt.", "openai.", "you.com", "kagi." };
        private static readonly string[] Redes = { "facebook.", "fb.com", "instagram.", "l.instagram", "t.co", "twitter.", "x.com", "linkedin.", "lnkd.in", "youtube.", "youtu.be", "whatsapp.", "wa.me", "tiktok.", "reddit.", "telegram.", "t.me", "pinterest." };
        private static readonly string[] Correos = { "mail.", "outlook.", "gmail.", "yahoo.com/mail", "proton." };

        private static void Origen(HttpRequestBase req, out string categoria, out string host)
        {
            categoria = "directo"; host = "";
            var r = req.UrlReferrer;
            if (r == null) return;
            var h = (r.Host ?? "").ToLowerInvariant();
            var propio = (req.Url != null ? req.Url.Host : "").ToLowerInvariant();
            if (h.Length == 0) return;
            if (h == propio || h.EndsWith(".bufins.com") || h == "bufins.com") { categoria = "interno"; return; }
            if (Buscadores.Any(x => h.Contains(x))) categoria = "buscador";
            else if (Redes.Any(x => h.Contains(x))) categoria = "red-social";
            else if (Correos.Any(x => h.Contains(x))) categoria = "correo";
            else categoria = "otro-sitio";
            host = Regex.Replace(h, "[^a-z0-9.\\-]", "");
            if (host.StartsWith("www.")) host = host.Substring(4);
            if (host.Length > 60) host = host.Substring(0, 60);
        }

        private static string Utm(HttpRequestBase req)
        {
            Func<string, string> g = k => Regex.Replace((req.QueryString[k] ?? "").ToLowerInvariant(), "[^a-z0-9_\\-]", "");
            string a = g("utm_source"), b = g("utm_medium"), c = g("utm_campaign");
            if (a.Length == 0 && b.Length == 0 && c.Length == 0) return "";
            Func<string, string> t = s => s.Length > 25 ? s.Substring(0, 25) : s;
            return t(a) + "|" + t(b) + "|" + t(c);
        }

        // ------------------------------------------------------------------ volcado a disco

        private static string CarpetaUso { get { return HostingEnvironment.MapPath("~/App_Data/observatorio/uso"); } }

        public static void Vaciar()
        {
            try { VaciarContadores(); } catch (Exception) { }
            try { VaciarEventos(); } catch (Exception) { }
            try { Purgar(); } catch (Exception) { }
        }

        private static void VaciarContadores()
        {
            if (Pendiente.IsEmpty) return;
            var lote = new Dictionary<string, long>();
            foreach (var k in Pendiente.Keys.ToList()) { long v; if (Pendiente.TryRemove(k, out v)) lote[k] = v; }
            if (lote.Count == 0) return;
            lock (LockArchivo)
            {
                Directory.CreateDirectory(CarpetaUso);
                foreach (var grupo in lote.GroupBy(x => x.Key.Substring(0, 7)))
                {
                    var ruta = Path.Combine(CarpetaUso, grupo.Key + ".json");
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

        private static void VaciarEventos()
        {
            if (ColaEventos.IsEmpty) return;
            var porDia = new Dictionary<string, StringBuilder>();
            KeyValuePair<string, string> it;
            int n = 0;
            while (n < 50000 && ColaEventos.TryDequeue(out it))
            {
                StringBuilder sb;
                if (!porDia.TryGetValue(it.Key, out sb)) porDia[it.Key] = sb = new StringBuilder();
                sb.Append(it.Value).Append('\n');
                n++;
            }
            if (porDia.Count == 0) return;
            lock (LockArchivo)
            {
                var carpeta = Path.Combine(CarpetaUso, "eventos");
                Directory.CreateDirectory(carpeta);
                foreach (var kv in porDia)
                    File.AppendAllText(Path.Combine(carpeta, kv.Key + ".jsonl"), kv.Value.ToString(), new UTF8Encoding(false));
            }
        }

        /// <summary>Borra los archivos de eventos más viejos que el período de retención (una vez al día).</summary>
        private static void Purgar()
        {
            var hoy = Hoy();
            if (_ultimaPurga == hoy) return;
            _ultimaPurga = hoy;
            int dias;
            if (!int.TryParse(System.Configuration.ConfigurationManager.AppSettings["ObsUsoRetencionDias"], out dias)) dias = 180;
            dias = Math.Max(30, dias);
            var carpeta = Path.Combine(CarpetaUso, "eventos");
            if (!Directory.Exists(carpeta)) return;
            var limite = DateTime.UtcNow.AddHours(-5).Date.AddDays(-dias);
            foreach (var f in Directory.GetFiles(carpeta, "*.jsonl"))
            {
                DateTime d;
                if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d) && d < limite)
                    try { File.Delete(f); } catch (Exception) { }
            }
        }
    }
}
