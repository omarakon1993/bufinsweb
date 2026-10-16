using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using bufinsweb.Helpers;
using bufinsweb.Models.Observatorio;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bufinsweb.Services
{
    /// <summary>Parte 2 del servicio: URLs con nombre, señales de prospecto, rankings, textos automáticos y datos complementarios.</summary>
    public partial class ObservatorioService
    {
        // ---------------------------------------------------------------- slugs y URLs

        public static string Slug(string s)
        {
            var n = Normalizar(s).ToLowerInvariant();
            var sb = new StringBuilder();
            foreach (var c in n) sb.Append(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' ? c : '-');
            return Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        }

        private static string SlugCorto(string razon)
        {
            var s = Slug(razon);
            if (s.Length > 60) { s = s.Substring(0, 60); var i = s.LastIndexOf('-'); if (i > 20) s = s.Substring(0, i); }
            return s;
        }

        /// <summary>URL canónica de la ficha: /observatorio/empresa/{nit}-{nombre}.</summary>
        public static string UrlEmpresa(Empresa e) { return "/observatorio/empresa/" + e.Nit + (string.IsNullOrEmpty(e.Slug) ? "" : "-" + SlugCorto(e.Razon)); }

        /// <summary>Extrae el NIT de "860076919-crepes-y-waffles" (o de "860076919").</summary>
        public static string NitDeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var m = Regex.Match(id, @"^(\d{5,12})(?:-.*)?$");
            return m.Success ? m.Groups[1].Value : null;
        }

        // ---------------------------------------------------------------- datos complementarios

        private static void CargarComplementos(ObservatorioDatos d)
        {
            try
            {
                var rc = Ruta("contratos.json");
                if (File.Exists(rc))
                {
                    var j = JObject.Parse(File.ReadAllText(rc, Encoding.UTF8));
                    d.ContratosDesde = (string)j["desde"];
                    foreach (var kv in (JObject)j["d"]) d.Contratos[kv.Key] = new[] { (long)kv.Value[0], (long)kv.Value[1] };
                }
            }
            catch (Exception) { /* dato complementario: si falla se ignora */ }
            try
            {
                var rm = Ruta("macro.json");
                if (File.Exists(rm))
                {
                    var j = JObject.Parse(File.ReadAllText(rm, Encoding.UTF8));
                    foreach (var kv in (JObject)j["trm"]) d.Trm[int.Parse(kv.Key)] = new[] { (double)kv.Value["prom"], (double)kv.Value["cierre"] };
                }
                var ri = Ruta("ipc.json");
                if (File.Exists(ri))
                {
                    var j = JObject.Parse(File.ReadAllText(ri, Encoding.UTF8));
                    foreach (var kv in (JObject)j["anual"]) d.Ipc[int.Parse(kv.Key)] = (double)kv.Value;
                }
            }
            catch (Exception) { }
            try
            {
                var rr = Ruta("reglas.json");
                if (File.Exists(rr)) d.Reglas = JsonConvert.DeserializeObject<ReglasProspecto>(File.ReadAllText(rr, Encoding.UTF8)) ?? new ReglasProspecto();
            }
            catch (Exception) { d.Reglas = new ReglasProspecto(); }
        }

        // ---------------------------------------------------------------- señales y potencial de prospecto

        public object SenalesDefs(ObservatorioDatos d)
        {
            return d.Reglas.Senales.Select(kv => new { clave = kv.Key, es = kv.Value.Es, en = kv.Value.En, tipo = kv.Value.Tipo, regla = kv.Value.Regla, puntos = kv.Value.Puntos }).ToList();
        }

        private static bool EnReorganizacion(string estado)
        {
            var n = Normalizar(estado);
            return n.Contains("REORGANIZ") || n.Contains("REESTRUCTUR") || n.Contains("CONCORDATO") || n.Contains("LIQUIDACION");
        }

        /// <summary>Detecta las señales de la empresa en el año y suma el potencial (0-100) según reglas.json. Solo usa cifras públicas de la empresa.</summary>
        public SenalesInfo CalcularSenales(ObservatorioDatos d, Empresa e, long[] v, long[] p, long[] p2)
        {
            var si = new SenalesInfo();
            var liq = Ratio.Liquidez(v);
            if (liq.HasValue && liq.Value < 1) si.Claves.Add("liquidez");
            if (v[M.Ktn] < 0) si.Claves.Add("ktneg");
            var de = Ratio.DeudaEbitda(v);
            if ((de.HasValue && de.Value > 4) || (v[M.Ebitda] <= 0 && v[M.Deuda] > 0)) si.Claves.Add("deuda");
            if (v[M.Ebitda] < 0) si.Claves.Add("ebitdaneg");
            if (v[M.Un] < 0) si.Claves.Add("perdida");
            if (EnReorganizacion(e.Estado)) si.Claves.Add("reorg");
            if (p != null && p2 != null && p2[M.Ing] > 0 && p[M.Ing] > 0 && p[M.Ing] > p2[M.Ing] * 1.2 && v[M.Ing] > p[M.Ing] * 1.2) si.Claves.Add("crece");
            var me = Ratio.MargenEbitda(v);
            if (me.HasValue && me.Value > 10) si.Claves.Add("rentable");

            double pts;
            d.Reglas.Tamano.TryGetValue(Tamano(v[M.Ing]), out pts);
            foreach (var c in si.Claves) { Senal s; if (d.Reglas.Senales.TryGetValue(c, out s)) pts += s.Puntos; }
            si.Potencial = (int)Math.Round(Math.Min(100, Math.Max(0, pts)));
            return si;
        }

        // ---------------------------------------------------------------- distribución por tamaño (cajas)

        private static readonly string[] MetricasCaja = { "mebitda", "mneto", "endeud", "liquidez", "score" };

        private object Cajas(List<long[]> vs, List<string> tams)
        {
            var res = new Dictionary<string, object>();
            foreach (var clave in MetricasCaja)
            {
                var def = Ratio.Todos.First(x => x.Clave == clave);
                var grupos = new List<object>();
                foreach (var t in new[] { "S", "M", "L", "XL" })
                {
                    var vals = new List<double>();
                    for (var i = 0; i < vs.Count; i++)
                    {
                        if (tams[i] != t) continue;
                        var x = def.F(vs[i]);
                        if (x.HasValue && !double.IsNaN(x.Value) && !double.IsInfinity(x.Value)) vals.Add(x.Value);
                    }
                    if (vals.Count < 5) continue;
                    var a = vals.OrderBy(z => z).ToArray();
                    grupos.Add(new { t, n = a.Length, p10 = R(Percentil(a, .1)), p25 = R(Percentil(a, .25)), med = R(Percentil(a, .5)), p75 = R(Percentil(a, .75)), p90 = R(Percentil(a, .9)) });
                }
                res[clave] = grupos;
            }
            return res;
        }

        // ---------------------------------------------------------------- rankings

        public class RankDef
        {
            public string Clave, Es, En, Unidad; public bool Desc = true; public double MinIngMm; public bool SinAnomalias;
            public Func<long[], long[], double?> Valor;
        }

        private static double? Cre(long[] v, long[] p) { return p != null && p[M.Ing] > 0 ? (double?)((v[M.Ing] - p[M.Ing]) / (double)p[M.Ing] * 100) : null; }

        public static readonly RankDef[] Rankings =
        {
            new RankDef { Clave = "ingresos", Es = "mayores ingresos", En = "highest revenue", Unidad = "mm", Valor = (v, p) => v[M.Ing] / 1000.0 },
            new RankDef { Clave = "crecimiento", Es = "mayor crecimiento de ingresos", En = "fastest revenue growth", Unidad = "%", MinIngMm = 5000, SinAnomalias = true, Valor = (v, p) => Cre(v, p) },
            new RankDef { Clave = "margen-ebitda", Es = "mayor margen EBITDA", En = "highest EBITDA margin", Unidad = "%", MinIngMm = 5000, SinAnomalias = true, Valor = (v, p) => Ratio.MargenEbitda(v) },
            new RankDef { Clave = "ebitda", Es = "mayor EBITDA", En = "highest EBITDA", Unidad = "mm", Valor = (v, p) => v[M.Ebitda] / 1000.0 },
            new RankDef { Clave = "utilidad", Es = "mayor utilidad neta", En = "highest net profit", Unidad = "mm", SinAnomalias = true, Valor = (v, p) => v[M.Un] / 1000.0 },
            new RankDef { Clave = "activos", Es = "mayores activos", En = "largest assets", Unidad = "mm", Valor = (v, p) => v[M.Act] / 1000.0 },
            new RankDef { Clave = "menor-deuda", Es = "menor deuda sobre EBITDA", En = "lowest debt to EBITDA", Unidad = "x", Desc = false, MinIngMm = 5000, SinAnomalias = true,
                Valor = (v, p) => v[M.Deuda] > 0 && v[M.Ebitda] > 0 ? Ratio.DeudaEbitda(v) : null },
            new RankDef { Clave = "salud", Es = "mejor Bufins Score de salud", En = "best Bufins health score", Unidad = "pts", MinIngMm = 5000, SinAnomalias = true, Valor = (v, p) => Salud.Calcular(v).Total }
        };

        public static RankDef DefRanking(string clave) { return Rankings.FirstOrDefault(x => x.Clave == clave); }

        public static bool Anomalia(long[] v) { return v[M.Ing] <= 0 || Math.Abs(v[M.Un]) > 3 * v[M.Ing] || v[M.Act] <= 0; }

        public class RankFila { public int Pos; public Empresa E; public double Valor; public double IngMm; public string Tam; public double Score; }

        public class RankingResultado
        {
            public RankDef Def; public string Tipo, Clave, Titulo, Anclaje; public int Anio, Total; public List<RankFila> Filas = new List<RankFila>();
        }

        /// <summary>Top N por métrica. tipo: nacional | sector (slug seccion-x / ciiu-nn) | departamento | ciudad (slug).</summary>
        public RankingResultado Ranking(string metrica, string tipo, string clave, int top)
        {
            var d = Datos;
            var cat = CiiuCatalogo.Actual;
            var def = DefRanking(metrica);
            if (def == null) return null;
            Func<Empresa, bool> ambito; string titulo = null;
            switch (tipo)
            {
                case "sector":
                    string cod;
                    if (clave != null && clave.StartsWith("seccion-")) cod = clave.Substring(8).ToUpperInvariant();
                    else if (clave != null && clave.StartsWith("ciiu-")) cod = clave.Substring(5);
                    else return null;
                    if (cod.Length == 0 || cod.Length > 4) return null;
                    var clases = new HashSet<string>(d.PorClase.Keys.Where(c => cat.Pertenece(c, cod)));
                    if (clases.Count == 0) return null;
                    ambito = e => clases.Contains(e.Ciiu); titulo = cat.Nombre(cod); break;
                case "departamento":
                    var ed = d.Empresas.FirstOrDefault(e => Slug(e.Depto) == clave);
                    if (ed == null) return null;
                    ambito = e => e.Depto == ed.Depto; titulo = ed.Depto; break;
                case "ciudad":
                    var ec = d.Empresas.FirstOrDefault(e => Slug(e.Ciudad) == clave);
                    if (ec == null) return null;
                    ambito = e => e.Ciudad == ec.Ciudad; titulo = ec.Ciudad; break;
                default: tipo = "nacional"; ambito = e => true; break;
            }
            var anio = d.MaxAnio;
            var r = new RankingResultado { Def = def, Tipo = tipo, Clave = clave, Titulo = titulo, Anio = anio };
            var cand = new List<RankFila>();
            foreach (var e in d.Empresas)
            {
                var v = e.En(anio, d.MinAnio);
                if (v == null || !ambito(e)) continue;
                if (def.SinAnomalias && Anomalia(v)) continue;
                if (v[M.Ing] / 1000.0 < def.MinIngMm) continue;
                var x = def.Valor(v, e.En(anio - 1, d.MinAnio));
                if (!x.HasValue || double.IsNaN(x.Value) || double.IsInfinity(x.Value)) continue;
                cand.Add(new RankFila { E = e, Valor = x.Value, IngMm = v[M.Ing] / 1000.0, Tam = Tamano(v[M.Ing]) });
            }
            r.Total = cand.Count;
            var orden = def.Desc ? cand.OrderByDescending(f => f.Valor) : cand.OrderBy(f => f.Valor);
            r.Filas = orden.ThenByDescending(f => f.IngMm).Take(Math.Max(5, Math.Min(top, 100))).ToList();
            for (var i = 0; i < r.Filas.Count; i++)
            {
                r.Filas[i].Pos = i + 1;
                var v = r.Filas[i].E.En(anio, d.MinAnio);
                r.Filas[i].Score = Salud.Calcular(v).Total;
            }
            return r;
        }

        public class HubRanking { public List<KeyValuePair<string, int>> Ciudades, Departamentos; public List<KeyValuePair<string, int>> Secciones; public Dictionary<string, string> NombreCiudad, NombreDepto; }

        public HubRanking HubRankings()
        {
            var d = Datos;
            var act = d.Empresas.Where(e => e.En(d.MaxAnio, d.MinAnio) != null).ToList();
            var h = new HubRanking { NombreCiudad = new Dictionary<string, string>(), NombreDepto = new Dictionary<string, string>() };
            h.Ciudades = act.Where(e => e.Ciudad.Length > 0).GroupBy(e => Slug(e.Ciudad)).Where(g => g.Key.Length > 0).OrderByDescending(g => g.Count()).Take(14)
                .Select(g => { h.NombreCiudad[g.Key] = g.First().Ciudad; return new KeyValuePair<string, int>(g.Key, g.Count()); }).ToList();
            h.Departamentos = act.Where(e => e.Depto.Length > 0).GroupBy(e => Slug(e.Depto)).Where(g => g.Key.Length > 0).OrderByDescending(g => g.Count()).Take(14)
                .Select(g => { h.NombreDepto[g.Key] = g.First().Depto; return new KeyValuePair<string, int>(g.Key, g.Count()); }).ToList();
            h.Secciones = act.Where(e => e.Seccion.Length > 0).GroupBy(e => e.Seccion).OrderByDescending(g => g.Count()).Select(g => new KeyValuePair<string, int>(g.Key, g.Count())).ToList();
            return h;
        }

        /// <summary>Claves (slug) de ciudades, departamentos y secciones con al menos 15 empresas: páginas de ranking indexables.</summary>
        public IEnumerable<string> RutasRanking()
        {
            var d = Datos;
            var act = d.Empresas.Where(e => e.En(d.MaxAnio, d.MinAnio) != null).ToList();
            var cs = act.Where(e => e.Ciudad.Length > 0).GroupBy(e => Slug(e.Ciudad)).Where(g => g.Key.Length > 0 && g.Count() >= 15).Select(g => "ciudad/" + g.Key);
            var ds = act.Where(e => e.Depto.Length > 0).GroupBy(e => Slug(e.Depto)).Where(g => g.Key.Length > 0 && g.Count() >= 15).Select(g => "departamento/" + g.Key);
            var ss = act.Where(e => e.Seccion.Length > 0).GroupBy(e => e.Seccion).Select(g => "sector/seccion-" + g.Key.ToLowerInvariant());
            return new[] { "nacional" }.Concat(cs).Concat(ds).Concat(ss).ToList();
        }

        // ---------------------------------------------------------------- textos automáticos (contenido único + preguntas frecuentes)

        public class Faq { public string P, R; }
        public class Narrativa { public List<string> Parrafos = new List<string>(); public List<Faq> Faqs = new List<Faq>(); public string Resumen; }

        private static string T(string es, string en) { return ObservatorioFormat.T(es, en); }
        private static string Mn(double millones) { return ObservatorioFormat.Moneda(millones); }
        private static string Pc(double? v) { return ObservatorioFormat.Porcentaje(v); }
        private static string Nx(double? v) { return v.HasValue ? v.Value.ToString("0.0", CultureInfo.CurrentCulture) + "x" : "—"; }

        public Narrativa NarrativaEmpresa(Empresa e)
        {
            var d = Datos;
            var cat = CiiuCatalogo.Actual;
            var anios = d.Meta.Anios.OrderBy(a => a).Where(a => e.En(a, d.MinAnio) != null).ToList();
            var anio = anios.Last();
            var v = e.En(anio, d.MinAnio);
            var pv = e.En(anio - 1, d.MinAnio);
            var n = new Narrativa();
            var act = cat.Nombre(e.Ciiu);
            var tam = Tamano(v[M.Ing]);
            var tamTxt = tam == "S" ? T("pequeña", "small") : tam == "M" ? T("mediana", "mid-sized") : tam == "L" ? T("grande", "large") : T("de las mayores del país", "among the country's largest");

            // posiciones por ingresos
            Func<IEnumerable<Empresa>, int[]> pos = grupo =>
            {
                var l = grupo.Where(x => x.En(anio, d.MinAnio) != null).ToList();
                return new[] { l.Count(x => x.En(anio, d.MinAnio)[M.Ing] > v[M.Ing]) + 1, l.Count };
            };
            var pNac = pos(d.Empresas);
            var pDiv = pos(d.PorClase.Where(kv => e.Division.Length > 0 && kv.Key.StartsWith(e.Division)).SelectMany(kv => kv.Value));

            var p1 = T(e.Razon + " (NIT " + e.Nit + ") es una empresa " + tamTxt + " de " + e.Ciudad + ", " + e.Depto + ", dedicada a «" + act + "» (CIIU " + e.Ciiu + "). ",
                       e.Razon + " (Tax ID " + e.Nit + ") is a " + tamTxt + " company based in " + e.Ciudad + ", " + e.Depto + ", engaged in “" + act + "” (ISIC " + e.Ciiu + "). ") +
                      T("En " + anio + " reportó a la Supersociedades ingresos por " + Mn(v[M.Ing] / 1000.0) + ", lo que la ubica en el puesto " + pNac[0].ToString("N0") + " de " + pNac[1].ToString("N0") + " empresas del país" +
                        (pDiv[1] > 1 ? " y en el " + pDiv[0].ToString("N0") + " de " + pDiv[1].ToString("N0") + " en su división económica." : "."),
                        "In " + anio + " it reported revenue of " + Mn(v[M.Ing] / 1000.0) + " to the Superintendencia de Sociedades, ranking #" + pNac[0].ToString("N0") + " of " + pNac[1].ToString("N0") + " companies nationwide" +
                        (pDiv[1] > 1 ? " and #" + pDiv[0].ToString("N0") + " of " + pDiv[1].ToString("N0") + " in its economic division." : "."));
            n.Parrafos.Add(p1);

            var crec = Cre(v, pv);
            double? real = null; double ipc;
            if (crec.HasValue && d.Ipc.TryGetValue(anio, out ipc)) real = ((1 + crec.Value / 100) / (1 + ipc / 100) - 1) * 100;
            var p2 = T("Su EBITDA fue de " + Mn(v[M.Ebitda] / 1000.0) + " (margen " + Pc(Ratio.MargenEbitda(v)) + ") y su utilidad neta de " + Mn(v[M.Un] / 1000.0) + " (margen " + Pc(Ratio.MargenNeto(v)) + "). ",
                       "Its EBITDA was " + Mn(v[M.Ebitda] / 1000.0) + " (margin " + Pc(Ratio.MargenEbitda(v)) + ") and net profit " + Mn(v[M.Un] / 1000.0) + " (margin " + Pc(Ratio.MargenNeto(v)) + "). ");
            if (crec.HasValue)
                p2 += T("Frente a " + (anio - 1) + ", los ingresos " + (crec.Value >= 0 ? "crecieron " : "cayeron ") + Pc(Math.Abs(crec.Value)) +
                        (real.HasValue ? " (" + (real.Value >= 0 ? "+" : "−") + Pc(Math.Abs(real.Value)) + " en términos reales, descontando una inflación de " + Pc(d.Ipc[anio]) + ")." : "."),
                        "Versus " + (anio - 1) + ", revenue " + (crec.Value >= 0 ? "grew " : "fell ") + Pc(Math.Abs(crec.Value)) +
                        (real.HasValue ? " (" + (real.Value >= 0 ? "+" : "−") + Pc(Math.Abs(real.Value)) + " in real terms, net of " + Pc(d.Ipc[anio]) + " inflation)." : "."));
            n.Parrafos.Add(p2);

            string nivel, codigo; int nGrupo;
            GrupoComparable(d, e, anio, out nivel, out codigo);
            var q = Cuartiles(d, nivel, codigo, anio, out nGrupo);
            Func<string, double> med = k => { var i = Array.FindIndex(Ratio.Todos, x => x.Clave == k); return q[i].Length > 0 ? Percentil(q[i], .5) : double.NaN; };
            var me = Ratio.MargenEbitda(v); var medMe = med("mebitda");
            if (me.HasValue && !double.IsNaN(medMe))
                n.Parrafos.Add(T("Frente a las " + nGrupo.ToString("N0") + " empresas comparables" + (codigo.Length > 0 ? " de «" + cat.Nombre(codigo) + "»" : "") + ", su margen EBITDA está " + (me.Value >= medMe ? "por encima" : "por debajo") +
                                 " de la mediana (" + Pc(me.Value) + " frente a " + Pc(medMe) + ")" + (Ratio.DeudaEbitda(v).HasValue && !double.IsNaN(med("deudaebitda")) ? " y su deuda sobre EBITDA es de " + Nx(Ratio.DeudaEbitda(v)) + " (mediana " + Nx(med("deudaebitda")) + ")." : "."),
                                 "Against " + nGrupo.ToString("N0") + " comparable companies" + (codigo.Length > 0 ? " in “" + cat.Nombre(codigo) + "”" : "") + ", its EBITDA margin is " + (me.Value >= medMe ? "above" : "below") +
                                 " the median (" + Pc(me.Value) + " vs. " + Pc(medMe) + ")" + (Ratio.DeudaEbitda(v).HasValue && !double.IsNaN(med("deudaebitda")) ? " and its debt to EBITDA is " + Nx(Ratio.DeudaEbitda(v)) + " (median " + Nx(med("deudaebitda")) + ")." : ".")));

            var sal = Salud.Calcular(v); var z = Ratio.Altman(v);
            var nivTxt = sal.Nivel == "solida" ? T("sólida", "strong") : sal.Nivel == "estable" ? T("estable", "stable") : sal.Nivel == "vigilar" ? T("a vigilar", "to watch") : T("crítica", "critical");
            n.Parrafos.Add(T("Su Bufins Score de salud financiera es de " + sal.Total.ToString("0") + " sobre 100 (" + nivTxt + ")" +
                             (z.HasValue ? "; el Altman Z'' es " + z.Value.ToString("0.0") + " (zona " + (Ratio.ZonaAltman(z.Value) == "segura" ? "segura" : Ratio.ZonaAltman(z.Value) == "gris" ? "gris" : "de alerta") + ")" : "") +
                             ". Su liquidez corriente es de " + Nx(Ratio.Liquidez(v)) + " y su endeudamiento (pasivo/activo) de " + Pc(Ratio.Endeudamiento(v)) + ".",
                             "Its Bufins financial health score is " + sal.Total.ToString("0") + " out of 100 (" + nivTxt + ")" +
                             (z.HasValue ? "; the Altman Z'' is " + z.Value.ToString("0.0") + " (" + Ratio.ZonaAltman(z.Value) + " zone)" : "") +
                             ". Its current ratio is " + Nx(Ratio.Liquidez(v)) + " and leverage (liabilities/assets) " + Pc(Ratio.Endeudamiento(v)) + "."));

            long[] c;
            if (d.Contratos.TryGetValue(e.Nit, out c) && c[0] > 0)
                n.Parrafos.Add(T("En el SECOP II registra " + c[0].ToString("N0") + " contratos con entidades del Estado desde " + d.ContratosDesde.Substring(0, 4) + (c[1] > 0 ? ", por un valor reportado de " + Mn(c[1] / 1e6) + "." : "."),
                                 "In SECOP II it has " + c[0].ToString("N0") + " contracts with public entities since " + d.ContratosDesde.Substring(0, 4) + (c[1] > 0 ? ", with a reported value of " + Mn(c[1] / 1e6) + "." : ".")));

            n.Resumen = e.Razon + ": " + T("ingresos " + Mn(v[M.Ing] / 1000.0) + ", EBITDA " + Mn(v[M.Ebitda] / 1000.0) + " y score de salud " + sal.Total.ToString("0") + "/100 en " + anio + ".",
                                            "revenue " + Mn(v[M.Ing] / 1000.0) + ", EBITDA " + Mn(v[M.Ebitda] / 1000.0) + " and health score " + sal.Total.ToString("0") + "/100 in " + anio + ".");

            n.Faqs.Add(new Faq { P = T("¿Cuánto vendió " + e.Razon + " en " + anio + "?", "How much did " + e.Razon + " sell in " + anio + "?"),
                R = T("Sus ingresos operacionales fueron de " + Mn(v[M.Ing] / 1000.0) + (crec.HasValue ? ", " + (crec.Value >= 0 ? "un aumento" : "una caída") + " de " + Pc(Math.Abs(crec.Value)) + " frente a " + (anio - 1) + "." : "."),
                     "Its operating revenue was " + Mn(v[M.Ing] / 1000.0) + (crec.HasValue ? ", " + (crec.Value >= 0 ? "up" : "down") + " " + Pc(Math.Abs(crec.Value)) + " versus " + (anio - 1) + "." : ".")) });
            n.Faqs.Add(new Faq { P = T("¿Cuál es el EBITDA de " + e.Razon + "?", "What is the EBITDA of " + e.Razon + "?"),
                R = T("En " + anio + " su EBITDA fue de " + Mn(v[M.Ebitda] / 1000.0) + ", un margen de " + Pc(Ratio.MargenEbitda(v)) + " sobre ingresos.",
                     "In " + anio + " its EBITDA was " + Mn(v[M.Ebitda] / 1000.0) + ", a margin of " + Pc(Ratio.MargenEbitda(v)) + " on revenue.") });
            n.Faqs.Add(new Faq { P = T("¿Qué tan endeudada está " + e.Razon + "?", "How leveraged is " + e.Razon + "?"),
                R = T("Su deuda financiera es de " + Mn(v[M.Deuda] / 1000.0) + " (" + Nx(Ratio.DeudaEbitda(v)) + " su EBITDA), sus pasivos equivalen a " + Pc(Ratio.Endeudamiento(v)) + " de sus activos y su liquidez corriente es de " + Nx(Ratio.Liquidez(v)) + ".",
                     "Its financial debt is " + Mn(v[M.Deuda] / 1000.0) + " (" + Nx(Ratio.DeudaEbitda(v)) + " its EBITDA), liabilities equal " + Pc(Ratio.Endeudamiento(v)) + " of assets and its current ratio is " + Nx(Ratio.Liquidez(v)) + ".") });
            n.Faqs.Add(new Faq { P = T("¿A qué se dedica " + e.Razon + " y dónde está?", "What does " + e.Razon + " do and where is it based?"),
                R = T("Su actividad principal es «" + act + "» (CIIU " + e.Ciiu + "). Tiene domicilio en " + e.Ciudad + ", " + e.Depto + (string.IsNullOrEmpty(e.FConst) ? "" : " y se constituyó el " + e.FConst) + ". Estado actual: " + (string.IsNullOrEmpty(e.Estado) ? "sin dato" : e.Estado) + ".",
                     "Its main activity is “" + act + "” (ISIC " + e.Ciiu + "). It is based in " + e.Ciudad + ", " + e.Depto + (string.IsNullOrEmpty(e.FConst) ? "" : " and was incorporated on " + e.FConst) + ". Current status: " + (string.IsNullOrEmpty(e.Estado) ? "n/a" : e.Estado) + ".") });
            n.Faqs.Add(new Faq { P = T("¿Cómo se compara " + e.Razon + " con su sector?", "How does " + e.Razon + " compare with its sector?"),
                R = T("Entre " + nGrupo.ToString("N0") + " empresas comparables, la mediana de margen EBITDA es " + Pc(medMe) + " y la suya " + Pc(me) + ". Su Bufins Score de salud es " + sal.Total.ToString("0") + "/100.",
                     "Among " + nGrupo.ToString("N0") + " comparable companies, the median EBITDA margin is " + Pc(medMe) + " versus " + Pc(me) + " for this company. Its Bufins health score is " + sal.Total.ToString("0") + "/100.") });
            return n;
        }

        public Narrativa NarrativaNodo(ResumenNodo s)
        {
            var d = Datos;
            var cat = CiiuCatalogo.Actual;
            var n = new Narrativa();
            var cur = s.Suma; var prev = s.Prev;
            var crec = prev[M.Ing] > 0 ? (double?)((cur[M.Ing] - prev[M.Ing]) / (double)prev[M.Ing] * 100) : null;
            var top = s.Top.FirstOrDefault();
            var miembros = d.PorClase.Where(kv => cat.Pertenece(kv.Key, s.Codigo)).SelectMany(kv => kv.Value).Select(e => e.En(d.MaxAnio, d.MinAnio)).Where(x => x != null).ToList();
            double[] mm = miembros.Select(x => Ratio.MargenEbitda(x)).Where(x => x.HasValue).Select(x => x.Value).OrderBy(x => x).ToArray();
            var medMe = mm.Length > 0 ? (double?)Percentil(mm, .5) : null;
            var conUtil = miembros.Count(x => x[M.Un] > 0);

            n.Parrafos.Add(T("En " + s.Anio + " reportaron estados financieros a la Supersociedades " + s.Total.ToString("N0") + " empresas del " + s.Nivel.ToLowerInvariant() + " «" + s.Titulo + "» (CIIU " + s.Codigo + "). Sus ingresos sumaron " + Mn(cur[M.Ing] / 1000.0) +
                             (crec.HasValue ? ", " + (crec.Value >= 0 ? "un crecimiento" : "una caída") + " de " + Pc(Math.Abs(crec.Value)) + " frente a " + (s.Anio - 1) + "." : "."),
                             "In " + s.Anio + ", " + s.Total.ToString("N0") + " companies in the " + s.Nivel.ToLowerInvariant() + " “" + s.Titulo + "” (ISIC " + s.Codigo + ") filed financial statements with the Superintendencia de Sociedades. Their combined revenue was " + Mn(cur[M.Ing] / 1000.0) +
                             (crec.HasValue ? ", " + (crec.Value >= 0 ? "up" : "down") + " " + Pc(Math.Abs(crec.Value)) + " versus " + (s.Anio - 1) + "." : ".")));
            n.Parrafos.Add(T("El EBITDA conjunto fue de " + Mn(cur[M.Ebitda] / 1000.0) + " (margen " + Pc(Ratio.MargenEbitda(cur)) + ") y la utilidad neta de " + Mn(cur[M.Un] / 1000.0) + ". La empresa mediana del sector tiene un margen EBITDA de " + Pc(medMe) +
                             " y " + conUtil.ToString("N0") + " de las " + miembros.Count.ToString("N0") + " empresas cerraron con utilidad.",
                             "Combined EBITDA was " + Mn(cur[M.Ebitda] / 1000.0) + " (margin " + Pc(Ratio.MargenEbitda(cur)) + ") and net profit " + Mn(cur[M.Un] / 1000.0) + ". The median company has an EBITDA margin of " + Pc(medMe) +
                             " and " + conUtil.ToString("N0") + " of " + miembros.Count.ToString("N0") + " companies closed with a profit."));
            if (top.Key != null)
                n.Parrafos.Add(T("La empresa con mayores ingresos es " + top.Key.Razon + " (" + Mn(top.Value[M.Ing] / 1000.0) + "), seguida por " + string.Join(" y ", s.Top.Skip(1).Take(2).Select(x => x.Key.Razon)) + ". Su deuda financiera agregada es de " + Mn(cur[M.Deuda] / 1000.0) + " (" + Nx(Ratio.DeudaEbitda(cur)) + " el EBITDA).",
                                 "The largest company by revenue is " + top.Key.Razon + " (" + Mn(top.Value[M.Ing] / 1000.0) + "), followed by " + string.Join(" and ", s.Top.Skip(1).Take(2).Select(x => x.Key.Razon)) + ". Aggregate financial debt is " + Mn(cur[M.Deuda] / 1000.0) + " (" + Nx(Ratio.DeudaEbitda(cur)) + " EBITDA)."));
            n.Resumen = s.Titulo + ": " + T(s.Total.ToString("N0") + " empresas, ingresos " + Mn(cur[M.Ing] / 1000.0) + " en " + s.Anio + ".", s.Total.ToString("N0") + " companies, revenue " + Mn(cur[M.Ing] / 1000.0) + " in " + s.Anio + ".");

            n.Faqs.Add(new Faq { P = T("¿Cuántas empresas hay en " + s.Titulo + "?", "How many companies are in " + s.Titulo + "?"),
                R = T("En " + s.Anio + " hay " + s.Total.ToString("N0") + " empresas de este " + s.Nivel.ToLowerInvariant() + " que reportaron a la Supersociedades.", "In " + s.Anio + " there are " + s.Total.ToString("N0") + " companies in this " + s.Nivel.ToLowerInvariant() + " that filed with the Superintendencia de Sociedades.") });
            n.Faqs.Add(new Faq { P = T("¿Cuánto venden las empresas de " + s.Titulo + "?", "How much do companies in " + s.Titulo + " sell?"),
                R = T("Sus ingresos conjuntos fueron de " + Mn(cur[M.Ing] / 1000.0) + " en " + s.Anio + (crec.HasValue ? ", " + Pc(crec) + " frente al año anterior." : "."), "Combined revenue was " + Mn(cur[M.Ing] / 1000.0) + " in " + s.Anio + (crec.HasValue ? ", " + Pc(crec) + " versus the prior year." : ".")) });
            n.Faqs.Add(new Faq { P = T("¿Cuál es el margen EBITDA típico en " + s.Titulo + "?", "What is the typical EBITDA margin in " + s.Titulo + "?"),
                R = T("La mediana es de " + Pc(medMe) + "; el sector en conjunto tiene un margen de " + Pc(Ratio.MargenEbitda(cur)) + ".", "The median is " + Pc(medMe) + "; the sector as a whole has a margin of " + Pc(Ratio.MargenEbitda(cur)) + ".") });
            if (top.Key != null)
                n.Faqs.Add(new Faq { P = T("¿Cuál es la empresa más grande de " + s.Titulo + "?", "What is the largest company in " + s.Titulo + "?"),
                    R = T(top.Key.Razon + ", con ingresos de " + Mn(top.Value[M.Ing] / 1000.0) + " en " + s.Anio + ".", top.Key.Razon + ", with revenue of " + Mn(top.Value[M.Ing] / 1000.0) + " in " + s.Anio + ".") });
            return n;
        }
    }
}
