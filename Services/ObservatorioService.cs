using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using bufinsweb.Helpers;
using bufinsweb.Models.Observatorio;
using Newtonsoft.Json;

namespace bufinsweb.Services
{
    /// <summary>
    /// Observatorio financiero: estados financieros NIIF (Supersociedades) en memoria.
    /// Lee App_Data/observatorio/empresas.json (índice: perfil + 22 métricas por año) y, bajo demanda,
    /// detalle.bin (rubros completos por empresa, bloques gzip). Los archivos los genera Tools/ObservatorioEtl/build_dataset.py.
    /// </summary>
    public class ObservatorioService
    {
        private const string CacheKey = "observatorio:datos";
        private static readonly object Lock = new object();
        private static readonly ConcurrentDictionary<string, double[][]> CacheBench = new ConcurrentDictionary<string, double[][]>();

        // Tamaño de empresa por ingresos (millones COP): S < 20.000 · M < 100.000 · L < 1 billón · XL ≥ 1 billón
        public const double LimiteS = 20000, LimiteM = 100000, LimiteL = 1000000;
        public const int MaxExportar = 10000;   // tope de filas por CSV (evita exportaciones masivas)

        // ---------------------------------------------------------------- carga

        // HostingEnvironment (no HttpContext) para poder precargar desde Application_Start en un hilo de fondo
        private static string Ruta(string archivo) { return HostingEnvironment.MapPath("~/App_Data/observatorio/" + archivo); }

        public ObservatorioDatos Datos
        {
            get
            {
                var cached = HttpRuntime.Cache[CacheKey] as ObservatorioDatos;
                if (cached != null) return cached;
                lock (Lock)
                {
                    cached = HttpRuntime.Cache[CacheKey] as ObservatorioDatos;
                    if (cached != null) return cached;
                    var path = Ruta("empresas.bin");
                    var datos = Cargar(path, Ruta("meta.json"));
                    CacheBench.Clear();
                    HttpRuntime.Cache.Insert(CacheKey, datos, new CacheDependency(path), Cache.NoAbsoluteExpiration, Cache.NoSlidingExpiration);
                    return datos;
                }
            }
        }

        /// <summary>Precarga datos e índices (se llama desde Application_Start para que la primera visita no espere).</summary>
        public static void Precargar()
        {
            var s = new ObservatorioService();
            var d = s.Datos;
            CiiuCatalogo.Cargar();
        }

        private static ObservatorioDatos Cargar(string path, string metaPath)
        {
            if (!File.Exists(path) || !File.Exists(metaPath))
                throw new FileNotFoundException("Faltan empresas.bin / meta.json. Ejecute Tools/ObservatorioEtl/build_dataset.py");

            var cat = CiiuCatalogo.Cargar();
            var d = new ObservatorioDatos();
            d.Meta = JsonConvert.DeserializeObject<ObservatorioMeta>(File.ReadAllText(metaPath, Encoding.UTF8));
            d.MinAnio = d.Meta.Anios.Min();
            d.MaxAnio = d.Meta.Anios.Max();
            d.Version = File.GetLastWriteTimeUtc(path).Ticks.ToString("x");
            d.DetalleArchivo = d.Meta.Detalle ?? "detalle.bin";
            var idxPath = Ruta(d.Meta.Indice ?? "detalle.idx.json");
            var nAnios = d.MaxAnio - d.MinAnio + 1;
            var internar = new Dictionary<string, string>();   // ciudad, departamento, estado, tipo: pocos valores distintos

            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
            using (var br = new BinaryReader(fs, Encoding.UTF8))
            {
                if (new string(br.ReadChars(4)) != "BOBS") throw new InvalidDataException("empresas.bin no es un archivo válido.");
                br.ReadInt32();                                   // versión
                var nA = br.ReadInt32(); var nM = br.ReadInt32(); var nE = br.ReadInt32();
                var anios = new int[nA];
                for (var i = 0; i < nA; i++) anios[i] = br.ReadInt32();
                if (nM != M.Count) throw new InvalidDataException("El archivo de datos no coincide con las métricas esperadas; regenere con build_dataset.py");
                d.Empresas.Capacity = nE;

                Func<string> leer = () => { var n = br.ReadUInt16(); return n == 0 ? "" : Encoding.UTF8.GetString(br.ReadBytes(n)); };
                Func<string, string> intern = v => { string r; if (!internar.TryGetValue(v, out r)) { internar[v] = v; r = v; } return r; };

                for (var k = 0; k < nE; k++)
                {
                    var e = new Empresa { Nit = leer(), Razon = leer() };
                    e.Ciiu = CiiuCatalogo.Clase(leer());
                    e.Ciudad = intern(leer()); e.Depto = intern(leer()); e.Estado = intern(leer());
                    e.FConst = leer(); e.Tipo = intern(leer()); e.Grupo = intern(leer());
                    e.Busqueda = Normalizar(e.Razon);
                    e.Division = CiiuCatalogo.Division(e.Ciiu);
                    e.Seccion = cat.Seccion(e.Division);
                    e.Y = new long[nAnios][];
                    var mask = br.ReadUInt16();
                    for (var i = 0; i < nA; i++)
                    {
                        if ((mask >> i & 1) == 0) continue;
                        var v = new long[nM];
                        for (var j = 0; j < nM; j++) v[j] = br.ReadInt64();
                        e.Y[anios[i] - d.MinAnio] = v;
                    }
                    for (var i = nAnios - 1; i >= 0; i--) if (e.Y[i] != null) { e.RankIng = e.Y[i][M.Ing]; break; }
                    d.Empresas.Add(e);
                    d.PorNit[e.Nit] = e;
                }
            }
            if (File.Exists(idxPath))
                d.Indice = JsonConvert.DeserializeObject<Dictionary<string, long[]>>(File.ReadAllText(idxPath, Encoding.UTF8));

            // Índices precalculados: empresas por clase CIIU y número de empresas por nodo (sección/división/grupo/clase) y año
            foreach (var e in d.Empresas)
            {
                if (e.Ciiu.Length != 4) continue;
                List<Empresa> lista;
                if (!d.PorClase.TryGetValue(e.Ciiu, out lista)) d.PorClase[e.Ciiu] = lista = new List<Empresa>();
                lista.Add(e);
                var claves = new[] { e.Seccion, e.Division, e.Ciiu.Substring(0, 3), e.Ciiu };
                for (var i = 0; i < nAnios; i++)
                {
                    if (e.Y[i] == null) continue;
                    foreach (var c in claves)
                    {
                        if (string.IsNullOrEmpty(c)) continue;
                        int[] cnt;
                        if (!d.ConteoNodo.TryGetValue(c, out cnt)) d.ConteoNodo[c] = cnt = new int[nAnios];
                        cnt[i]++;
                    }
                }
            }
            return d;
        }

        /// <summary>Clases CIIU (4 dígitos) que pertenecen a alguno de los nodos dados.</summary>
        private static HashSet<string> ClasesDe(ObservatorioDatos d, IEnumerable<string> nodos)
        {
            var cat = CiiuCatalogo.Actual;
            var lista = nodos.ToList();
            return new HashSet<string>(d.PorClase.Keys.Where(c => lista.Any(n => cat.Pertenece(c, n))));
        }

        // ---------------------------------------------------------------- utilidades

        public static string Normalizar(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var f = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in f) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().ToUpperInvariant();
        }

        public static string Tamano(long ingMiles)
        {
            var mm = ingMiles / 1000.0;
            return mm < LimiteS ? "S" : mm < LimiteM ? "M" : mm < LimiteL ? "L" : "XL";
        }

        private static double? Mm(long miles) { return miles / 1000.0; }
        private static double R(double v) { return Math.Round(v, 1); }
        private static double? R(double? v) { return v.HasValue ? (double?)Math.Round(v.Value, 2) : null; }

        private class Fila { public Empresa E; public long[] V; public double? Crec; }

        private IEnumerable<Fila> Filtrar(ObservatorioDatos d, FiltroSector f, int anio)
        {
            var q = Normalizar(f.Q ?? "").Trim();
            var qDigitos = new string((f.Q ?? "").Where(char.IsDigit).ToArray());
            var tams = f.Tamanos.Count > 0 ? new HashSet<string>(f.Tamanos) : null;
            var nodos = f.Ciiu.Where(x => !string.IsNullOrEmpty(x)).ToList();
            var clases = nodos.Count > 0 ? ClasesDe(d, nodos) : null;   // se resuelve una vez, no por empresa

            foreach (var e in d.Empresas)
            {
                var v = e.En(anio, d.MinAnio);
                if (v == null) continue;
                if (!string.IsNullOrEmpty(f.Departamento) && e.Depto != f.Departamento) continue;
                if (!string.IsNullOrEmpty(f.Estado) && e.Estado != f.Estado) continue;
                if (clases != null && !clases.Contains(e.Ciiu)) continue;
                if (tams != null && !tams.Contains(Tamano(v[M.Ing]))) continue;
                if (q.Length > 0)
                {
                    var porNit = qDigitos.Length >= 4 && e.Nit.StartsWith(qDigitos, StringComparison.Ordinal);
                    if (!porNit && !e.Busqueda.Contains(q)) continue;
                }
                var ingMm = v[M.Ing] / 1000.0;
                if (f.IngMin.HasValue && ingMm < f.IngMin.Value) continue;
                if (f.IngMax.HasValue && ingMm > f.IngMax.Value) continue;
                if (f.Resultado == "utilidad" && v[M.Un] <= 0) continue;
                if (f.Resultado == "perdida" && v[M.Un] >= 0) continue;

                var p = e.En(anio - 1, d.MinAnio);
                yield return new Fila { E = e, V = v, Crec = p != null && p[M.Ing] > 0 ? (double?)((v[M.Ing] - p[M.Ing]) / (double)p[M.Ing] * 100) : null };
            }
        }

        private static long[] Sumar(IEnumerable<long[]> vs)
        {
            var s = new long[M.Count];
            foreach (var v in vs) for (var i = 0; i < M.Count; i++) s[i] += v[i];
            return s;
        }

        private static Dictionary<string, object> Metricas(long[] s, int n)
        {
            return new Dictionary<string, object>
            {
                { "n", n }, { "ing", Mm(s[M.Ing]) }, { "ub", Mm(s[M.Ub]) }, { "gop", Mm(s[M.Gop]) }, { "uop", Mm(s[M.Uop]) }, { "ebitda", Mm(s[M.Ebitda]) },
                { "un", Mm(s[M.Un]) }, { "act", Mm(s[M.Act]) }, { "pas", Mm(s[M.Pas]) }, { "pat", Mm(s[M.Pat]) }, { "ktn", Mm(s[M.Ktn]) }, { "deuda", Mm(s[M.Deuda]) },
                { "cxc", Mm(s[M.Cxc]) }, { "inv", Mm(s[M.Inv]) }, { "cxp", Mm(s[M.Cxp]) }, { "fco", Mm(s[M.Fco]) }, { "caja", Mm(s[M.Caja]) },
                { "mbruto", R(Ratio.MargenBruto(s)) }, { "moper", R(Ratio.MargenOperativo(s)) }, { "mebitda", R(Ratio.MargenEbitda(s)) },
                { "mneto", R(Ratio.MargenNeto(s)) }, { "gastos", R(Ratio.GastosSobreIngresos(s)) }, { "ktingresos", R(Ratio.KtSobreIngresos(s)) },
                { "deudaebitda", R(Ratio.DeudaEbitda(s)) }, { "endeud", R(Ratio.Endeudamiento(s)) }, { "roa", R(Ratio.Roa(s)) },
                { "diascxc", R(Ratio.DiasCxc(s)) }, { "diasinv", R(Ratio.DiasInv(s)) }, { "diascxp", R(Ratio.DiasCxp(s)) }
            };
        }

        private static double Percentil(double[] ordenado, double p)
        {
            if (ordenado.Length == 0) return double.NaN;
            var pos = p * (ordenado.Length - 1);
            int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
            return ordenado[lo] + (ordenado[hi] - ordenado[lo]) * (pos - lo);
        }

        // ---------------------------------------------------------------- opciones y búsqueda

        public object Opciones()
        {
            var d = Datos;
            var cat = CiiuCatalogo.Actual;
            var actuales = d.Empresas.Where(e => e.En(d.MaxAnio, d.MinAnio) != null).ToList();
            var conteo = actuales.Where(e => e.Ciiu.Length == 4).GroupBy(e => e.Ciiu).ToDictionary(g => g.Key, g => g.Count());
            return new
            {
                meta = d.Meta,
                anioActual = d.MaxAnio,
                anios = d.Meta.Anios.OrderByDescending(a => a),
                departamentos = actuales.Where(e => e.Depto.Length > 0).GroupBy(e => e.Depto).OrderBy(g => g.Key).Select(g => new { valor = g.Key, n = g.Count() }),
                estados = actuales.Where(e => e.Estado.Length > 0).GroupBy(e => e.Estado).OrderByDescending(g => g.Count()).Select(g => new { valor = g.Key, n = g.Count() }),
                tamanos = new[] { "S", "M", "L", "XL" }.Select(t => new { codigo = t, n = actuales.Count(e => Tamano(e.En(d.MaxAnio, d.MinAnio)[M.Ing]) == t) }),
                arbol = cat.Arbol(conteo)
            };
        }

        public object Buscar(string q, int take)
        {
            var d = Datos;
            var cat = CiiuCatalogo.Actual;
            q = (q ?? "").Trim();
            if (q.Length < 2) return new object[0];
            var qn = Normalizar(q);
            var digitos = new string(q.Where(char.IsDigit).ToArray());
            var tokens = qn.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            IEnumerable<Empresa> hits;
            if (digitos.Length >= 3 && digitos.Length >= q.Replace(" ", "").Replace(".", "").Replace("-", "").Length - 1)
                hits = d.Empresas.Where(e => e.Nit.StartsWith(digitos, StringComparison.Ordinal));
            else
                hits = d.Empresas.Where(e => tokens.All(t => e.Busqueda.Contains(t)));

            return hits.OrderByDescending(e => e.Busqueda.StartsWith(qn, StringComparison.Ordinal)).ThenByDescending(e => e.RankIng)
                .Take(Math.Max(1, Math.Min(take, 15)))
                .Select(e => new
                {
                    nit = e.Nit, razon = e.Razon, ciiu = e.Ciiu, actividad = cat.Nombre(e.Ciiu), ciudad = e.Ciudad, estado = e.Estado,
                    ing = Mm(e.RankIng), tam = Tamano(e.RankIng)
                }).ToList();
        }

        // ---------------------------------------------------------------- análisis por sector

        public object Sector(FiltroSector f)
        {
            var d = Datos;
            var cat = CiiuCatalogo.Actual;
            var anio = f.Anio.HasValue && d.Meta.Anios.Contains(f.Anio.Value) ? f.Anio.Value : d.MaxAnio;
            var filas = Filtrar(d, f, anio).ToList();
            var suma = Sumar(filas.Select(x => x.V));

            var tendencia = d.Meta.Anios.OrderBy(a => a).Select(a =>
            {
                var rs = a == anio ? filas : Filtrar(d, f, a).ToList();
                var m = Metricas(Sumar(rs.Select(x => x.V)), rs.Count);
                m["anio"] = a;
                return m;
            }).ToList();

            // Mediana y cuartiles de los indicadores entre las empresas filtradas
            var estad = new Dictionary<string, object>();
            foreach (var def in Ratio.Todos)
            {
                var vals = filas.Select(x => def.F(x.V)).Where(x => x.HasValue && !double.IsNaN(x.Value) && !double.IsInfinity(x.Value)).Select(x => x.Value).OrderBy(x => x).ToArray();
                if (vals.Length == 0) continue;
                estad[def.Clave] = new { p25 = R(Percentil(vals, .25)), med = R(Percentil(vals, .5)), p75 = R(Percentil(vals, .75)), n = vals.Length };
            }

            var porTam = new[] { "S", "M", "L", "XL" }.Select(t =>
            {
                var g = filas.Where(x => Tamano(x.V[M.Ing]) == t).ToList();
                return new { codigo = t, n = g.Count, ing = Mm(g.Sum(x => x.V[M.Ing])) };
            }).ToList();

            Func<Func<Fila, string>, Func<string, string>, int, List<object>> agrupar = (clave, etiqueta, top) =>
                filas.Where(x => !string.IsNullOrEmpty(clave(x))).GroupBy(clave).Select(g => new
                {
                    k = g.Key, etiqueta = etiqueta(g.Key), n = g.Count(), ing = Mm(g.Sum(x => x.V[M.Ing])), ebitda = Mm(g.Sum(x => x.V[M.Ebitda])),
                    mebitda = R(Ratio.MargenEbitda(Sumar(g.Select(x => x.V))))
                }).OrderByDescending(x => x.ing).Take(top).Cast<object>().ToList();

            var orden = Ordenar(filas, f.Orden, f.Desc).ToList();
            var tam = Math.Max(5, Math.Min(f.Tamano, 100));
            var paginas = Math.Max(1, (int)Math.Ceiling(filas.Count / (double)tam));
            var pagina = Math.Max(1, Math.Min(f.Pagina, paginas));

            var dispersion = filas.OrderByDescending(x => x.V[M.Ing]).Take(300).Where(x => Ratio.MargenEbitda(x.V).HasValue && Ratio.Endeudamiento(x.V).HasValue)
                .Select(x => new
                {
                    nit = x.E.Nit, razon = x.E.Razon, x = Math.Max(0, Math.Min(Ratio.Endeudamiento(x.V).Value, 200)),
                    y = Math.Max(-60, Math.Min(Ratio.MargenEbitda(x.V).Value, 80)), r = Mm(x.V[M.Act]), t = x.E.Seccion
                }).ToList();

            return new
            {
                anio,
                total = filas.Count,
                kpis = Metricas(suma, filas.Count),
                tendencia,
                estadisticas = estad,
                tamanos = porTam,
                divisiones = agrupar(x => x.E.Division, k => cat.Nombre(k), 12),
                departamentos = agrupar(x => x.E.Depto, k => k, 12),
                dispersion,
                tabla = orden.Skip((pagina - 1) * tam).Take(tam).Select(FilaTabla),
                pagina, paginas, tamano = tam
            };
        }

        private static object FilaTabla(Fila r)
        {
            var v = r.V;
            return new
            {
                nit = r.E.Nit, razon = r.E.Razon, ciiu = r.E.Ciiu, ciudad = r.E.Ciudad, depto = r.E.Depto, estado = r.E.Estado, tam = Tamano(v[M.Ing]),
                ing = Mm(v[M.Ing]), uop = Mm(v[M.Uop]), ebitda = Mm(v[M.Ebitda]), un = Mm(v[M.Un]), act = Mm(v[M.Act]), deuda = Mm(v[M.Deuda]),
                mbruto = R(Ratio.MargenBruto(v)), moper = R(Ratio.MargenOperativo(v)), mebitda = R(Ratio.MargenEbitda(v)),
                deudaebitda = R(Ratio.DeudaEbitda(v)), ktingresos = R(Ratio.KtSobreIngresos(v)), crec = R(r.Crec)
            };
        }

        private static IEnumerable<Fila> Ordenar(IEnumerable<Fila> rows, string orden, bool desc)
        {
            Func<Fila, double> num;
            switch (orden)
            {
                case "nombre": return desc ? rows.OrderByDescending(r => r.E.Razon) : rows.OrderBy(r => r.E.Razon);
                case "uop": num = r => r.V[M.Uop]; break;
                case "ebitda": num = r => r.V[M.Ebitda]; break;
                case "un": num = r => r.V[M.Un]; break;
                case "act": num = r => r.V[M.Act]; break;
                case "deuda": num = r => r.V[M.Deuda]; break;
                case "mbruto": num = r => Ratio.MargenBruto(r.V) ?? double.NegativeInfinity; break;
                case "moper": num = r => Ratio.MargenOperativo(r.V) ?? double.NegativeInfinity; break;
                case "mebitda": num = r => Ratio.MargenEbitda(r.V) ?? double.NegativeInfinity; break;
                case "deudaebitda": num = r => Ratio.DeudaEbitda(r.V) ?? double.NegativeInfinity; break;
                case "ktingresos": num = r => Ratio.KtSobreIngresos(r.V) ?? double.NegativeInfinity; break;
                case "crec": num = r => r.Crec ?? double.NegativeInfinity; break;
                default: num = r => r.V[M.Ing]; break;
            }
            return desc ? rows.OrderByDescending(num) : rows.OrderBy(num);
        }

        public IEnumerable<string[]> Exportar(FiltroSector f, out int anio)
        {
            var d = Datos;
            var cat = CiiuCatalogo.Actual;
            anio = f.Anio.HasValue && d.Meta.Anios.Contains(f.Anio.Value) ? f.Anio.Value : d.MaxAnio;
            var es = CultureInfo.GetCultureInfo("es-CO");
            Func<double?, string> n1 = v => v.HasValue ? v.Value.ToString("0.0", es) : "";
            Func<long, string> mm = v => (v / 1000.0).ToString("0.0", es);
            return Ordenar(Filtrar(d, f, anio), f.Orden, f.Desc).Take(MaxExportar).Select(r => new[]
            {
                r.E.Nit, r.E.Razon, r.E.Ciiu, cat.Nombre(r.E.Ciiu), r.E.Ciudad, r.E.Depto, r.E.Estado, Tamano(r.V[M.Ing]),
                mm(r.V[M.Ing]), mm(r.V[M.Ub]), mm(r.V[M.Uop]), mm(r.V[M.Ebitda]), mm(r.V[M.Un]), mm(r.V[M.Act]), mm(r.V[M.Pas]), mm(r.V[M.Pat]), mm(r.V[M.Deuda]), mm(r.V[M.Ktn]),
                n1(Ratio.MargenBruto(r.V)), n1(Ratio.MargenOperativo(r.V)), n1(Ratio.MargenEbitda(r.V)), n1(Ratio.MargenNeto(r.V)),
                n1(Ratio.DeudaEbitda(r.V)), n1(Ratio.KtSobreIngresos(r.V)), n1(r.Crec)
            }).ToList();
        }

        // ---------------------------------------------------------------- benchmark (cuartiles por grupo y año)

        private double[][] Cuartiles(ObservatorioDatos d, string nivel, string codigo, int anio, out int n)
        {
            n = Contar(d, codigo, anio);
            var key = nivel + "|" + codigo + "|" + anio;
            double[][] hit;
            if (CacheBench.TryGetValue(key, out hit)) return hit;
            if (CacheBench.Count > 1500) CacheBench.Clear();   // tope de memoria: ~500 clases x 6 años en el peor caso

            // Solo se recorren las clases del nodo (índice por clase), no las 37 mil empresas
            var cat = CiiuCatalogo.Actual;
            var miembros = (codigo.Length == 0 ? d.Empresas : d.PorClase.Where(kv => cat.Pertenece(kv.Key, codigo)).SelectMany(kv => kv.Value))
                .Select(e => e.En(anio, d.MinAnio)).Where(v => v != null).ToList();
            var res = Ratio.Todos.Select(def =>
                miembros.Select(v => def.F(v)).Where(x => x.HasValue && !double.IsNaN(x.Value) && !double.IsInfinity(x.Value)).Select(x => x.Value).OrderBy(x => x).ToArray()).ToArray();
            CacheBench[key] = res;
            return res;
        }

        /// <summary>Número de empresas de un nodo CIIU en un año (precalculado al cargar).</summary>
        private static int Contar(ObservatorioDatos d, string codigo, int anio)
        {
            if (string.IsNullOrEmpty(codigo)) return d.Empresas.Count(e => e.En(anio, d.MinAnio) != null);
            int[] cnt;
            var i = anio - d.MinAnio;
            return d.ConteoNodo.TryGetValue(codigo, out cnt) && i >= 0 && i < cnt.Length ? cnt[i] : 0;
        }

        /// <summary>Elige el grupo de comparación más específico con al menos 15 empresas: clase, grupo, división o sección.</summary>
        private void GrupoComparable(ObservatorioDatos d, Empresa e, int anio, out string nivel, out string codigo)
        {
            var candidatos = new[] { Tuple.Create("clase", e.Ciiu), Tuple.Create("grupo", e.Ciiu.Length == 4 ? e.Ciiu.Substring(0, 3) : ""), Tuple.Create("division", e.Division), Tuple.Create("seccion", e.Seccion) };
            foreach (var c in candidatos)
            {
                if (string.IsNullOrEmpty(c.Item2)) continue;
                if (Contar(d, c.Item2, anio) >= 15) { nivel = c.Item1; codigo = c.Item2; return; }
            }
            nivel = "todas"; codigo = "";
        }

        // ---------------------------------------------------------------- empresa individual

        public Dictionary<string, Dictionary<string, long>> LeerDetalle(string nit, out Dictionary<string, string> perfil)
        {
            perfil = new Dictionary<string, string>();
            var d = Datos;
            long[] pos;
            if (!d.Indice.TryGetValue(nit, out pos)) return new Dictionary<string, Dictionary<string, long>>();
            var buf = new byte[pos[1]];
            using (var fs = new FileStream(Ruta(d.DetalleArchivo), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Seek(pos[0], SeekOrigin.Begin);
                var leidos = 0;
                while (leidos < buf.Length) { var r = fs.Read(buf, leidos, buf.Length - leidos); if (r <= 0) break; leidos += r; }
            }
            using (var gz = new GZipStream(new MemoryStream(buf), CompressionMode.Decompress))
            using (var sr = new StreamReader(gz, Encoding.UTF8))
            {
                var bloque = JsonConvert.DeserializeObject<DetalleBloque>(sr.ReadToEnd());
                perfil = bloque.P ?? perfil;
                return bloque.Y ?? new Dictionary<string, Dictionary<string, long>>();
            }
        }

        private class DetalleBloque
        {
            [JsonProperty("p")] public Dictionary<string, string> P = null;
            [JsonProperty("y")] public Dictionary<string, Dictionary<string, long>> Y = null;
        }

        public Empresa PorNit(string nit)
        {
            Empresa e;
            return !string.IsNullOrEmpty(nit) && Datos.PorNit.TryGetValue(nit, out e) ? e : null;
        }

        private static object Serie(Empresa e, ObservatorioDatos d)
        {
            var anios = d.Meta.Anios.OrderBy(a => a).ToList();
            Func<Func<long[], double?>, List<double?>> s = f => anios.Select(a => { var v = e.En(a, d.MinAnio); return v == null ? null : f(v); }).ToList();
            var dic = new Dictionary<string, object>();
            dic["anios"] = anios;
            dic["ing"] = s(v => Mm(v[M.Ing])); dic["ub"] = s(v => Mm(v[M.Ub])); dic["gop"] = s(v => Mm(v[M.Gop])); dic["uop"] = s(v => Mm(v[M.Uop]));
            dic["ebitda"] = s(v => Mm(v[M.Ebitda])); dic["un"] = s(v => Mm(v[M.Un])); dic["act"] = s(v => Mm(v[M.Act])); dic["pas"] = s(v => Mm(v[M.Pas]));
            dic["pat"] = s(v => Mm(v[M.Pat])); dic["ktn"] = s(v => Mm(v[M.Ktn])); dic["deuda"] = s(v => Mm(v[M.Deuda])); dic["cxc"] = s(v => Mm(v[M.Cxc]));
            dic["inv"] = s(v => Mm(v[M.Inv])); dic["cxp"] = s(v => Mm(v[M.Cxp])); dic["fco"] = s(v => Mm(v[M.Fco])); dic["caja"] = s(v => Mm(v[M.Caja]));
            foreach (var def in Ratio.Todos)
            {
                var f = def.F;
                dic[def.Clave] = s(v => R(f(v)));
            }
            dic["crec"] = anios.Select(a =>
            {
                var v = e.En(a, d.MinAnio); var p = e.En(a - 1, d.MinAnio);
                return v != null && p != null ? R(Crec(v[M.Ing], p[M.Ing])) : null;
            }).ToList();
            return dic;
        }

        private static double? Crec(long act, long prev) { return prev > 0 ? (double?)((act - prev) / (double)prev * 100) : null; }

        public object Detalle(string nit)
        {
            var d = Datos;
            var e = PorNit(nit);
            if (e == null) return null;
            var cat = CiiuCatalogo.Actual;
            Dictionary<string, string> p;
            var rubros = LeerDetalle(nit, out p);
            Func<string, string> pv = k => { string v; return p.TryGetValue(k, out v) ? v : ""; };

            // Benchmark por año
            var bench = new Dictionary<string, object>();
            foreach (var a in d.Meta.Anios)
            {
                var v = e.En(a, d.MinAnio);
                if (v == null) continue;
                string nivel, codigo;
                GrupoComparable(d, e, a, out nivel, out codigo);
                int n;
                var q = Cuartiles(d, nivel, codigo, a, out n);
                var ratios = new Dictionary<string, object>();
                for (var i = 0; i < Ratio.Todos.Length; i++)
                {
                    var def = Ratio.Todos[i];
                    var arr = q[i];
                    if (arr.Length < 5) continue;
                    var val = def.F(v);
                    double? mejor = null;
                    if (val.HasValue)
                    {
                        var menores = arr.Count(x => x < val.Value) + arr.Count(x => x == val.Value) / 2.0;
                        var pct = menores / arr.Length * 100;
                        mejor = R(def.MayorMejor ? pct : 100 - pct);
                    }
                    ratios[def.Clave] = new { valor = R(val), p25 = R(Percentil(arr, .25)), med = R(Percentil(arr, .5)), p75 = R(Percentil(arr, .75)), n = arr.Length, mejorQue = mejor };
                }
                bench[a.ToString()] = new { nivel, codigo, nombre = codigo.Length > 0 ? cat.Nombre(codigo) : "", empresas = n, ratios };
            }

            return new
            {
                perfil = new
                {
                    nit = e.Nit, razon = e.Razon, ciiu = e.Ciiu, actividad = cat.Nombre(e.Ciiu), division = e.Division, divisionNombre = cat.Nombre(e.Division),
                    seccion = e.Seccion, seccionNombre = cat.Nombre(e.Seccion), ciudad = e.Ciudad, depto = e.Depto, estado = e.Estado, fconst = e.FConst,
                    tipo = e.Tipo, grupo = e.Grupo == "1" ? "NIIF Plenas (Grupo 1)" : "NIIF Pymes (Grupo 2)", objeto = pv("objeto"), clase = pv("clase"), rf = pv("rf"), rfop = pv("rfop")
                },
                serie = Serie(e, d),
                rubros = rubros.ToDictionary(kv => kv.Key, kv => kv.Value.ToDictionary(x => x.Key, x => Math.Round(x.Value / 1000.0, 3))),
                bench,
                tamano = Tamano(e.RankIng),
                fuente = new { d.Meta.Fuente, d.Meta.FuenteActualizado }
            };
        }

        public object Comparar(IEnumerable<string> nits)
        {
            var d = Datos;
            var cat = CiiuCatalogo.Actual;
            var anios = d.Meta.Anios.OrderBy(a => a).ToList();
            var items = nits.Distinct().Take(4).Select(n => PorNit(n)).Where(e => e != null).Select(e => new
            {
                nit = e.Nit, razon = e.Razon, ciiu = e.Ciiu, actividad = cat.Nombre(e.Ciiu), ciudad = e.Ciudad, estado = e.Estado, serie = Serie(e, d)
            }).ToList();
            return new { anios, items };
        }

        // Exportación de los estados de una empresa (CSV)
        public List<string[]> ExportarEmpresa(string nit, Func<string, string> etiqueta, IEnumerable<string> orden)
        {
            Dictionary<string, string> p;
            var rub = LeerDetalle(nit, out p);
            var anios = rub.Keys.OrderBy(k => k).ToList();
            var filas = new List<string[]>();
            filas.Add(new[] { "Rubro (miles de COP)" }.Concat(anios).ToArray());
            var es = CultureInfo.GetCultureInfo("es-CO");
            foreach (var k in orden)
            {
                var vals = anios.Select(a => { long v; return rub[a].TryGetValue(k, out v) ? v.ToString("0", es) : "0"; });
                filas.Add(new[] { etiqueta(k) }.Concat(vals).ToArray());
            }
            return filas;
        }

        // ---------------------------------------------------------------- páginas SSR

        public class ResumenNodo
        {
            public string Codigo, Titulo, Nivel, Slug; public int Anio, Total; public long[] Suma, Prev;
            public List<KeyValuePair<Empresa, long[]>> Top = new List<KeyValuePair<Empresa, long[]>>();
            public List<int> Anios = new List<int>(); public List<double> SerieIng = new List<double>(), SerieEbitda = new List<double>(); public List<int> SerieN = new List<int>();
        }

        /// <summary>Slug "seccion-g" o "ciiu-47" (división) / "ciiu-4711" (clase).</summary>
        public ResumenNodo Nodo(string slug)
        {
            var d = Datos;
            var cat = CiiuCatalogo.Actual;
            if (string.IsNullOrEmpty(slug)) return null;
            string codigo, nivel;
            if (slug.StartsWith("seccion-", StringComparison.Ordinal)) { codigo = slug.Substring(8).ToUpperInvariant(); nivel = "Sección"; }
            else if (slug.StartsWith("ciiu-", StringComparison.Ordinal)) { codigo = slug.Substring(5); nivel = codigo.Length == 2 ? "División" : codigo.Length == 3 ? "Grupo" : "Clase"; }
            else return null;
            if (codigo.Length == 0 || codigo.Length > 4) return null;
            var miembros = d.PorClase.Where(kv => cat.Pertenece(kv.Key, codigo)).SelectMany(kv => kv.Value).ToList();
            if (miembros.Count == 0) return null;

            var r = new ResumenNodo { Codigo = codigo, Titulo = cat.Nombre(codigo), Nivel = nivel, Slug = slug, Anio = d.MaxAnio };
            foreach (var a in d.Meta.Anios.OrderBy(a => a))
            {
                var vs = miembros.Select(e => e.En(a, d.MinAnio)).Where(v => v != null).ToList();
                var s = Sumar(vs);
                r.Anios.Add(a); r.SerieN.Add(vs.Count); r.SerieIng.Add(s[M.Ing] / 1e6); r.SerieEbitda.Add(s[M.Ebitda] / 1e6);
            }
            var act = miembros.Where(e => e.En(d.MaxAnio, d.MinAnio) != null).ToList();
            r.Total = act.Count;
            r.Suma = Sumar(act.Select(e => e.En(d.MaxAnio, d.MinAnio)));
            r.Prev = Sumar(miembros.Select(e => e.En(d.MaxAnio - 1, d.MinAnio)).Where(v => v != null));
            r.Top = act.OrderByDescending(e => e.En(d.MaxAnio, d.MinAnio)[M.Ing]).Take(15)
                .Select(e => new KeyValuePair<Empresa, long[]>(e, e.En(d.MaxAnio, d.MinAnio))).ToList();
            return r;
        }

        public List<KeyValuePair<Empresa, long[]>> TopNacional(int n)
        {
            var d = Datos;
            return d.Empresas.Where(e => e.En(d.MaxAnio, d.MinAnio) != null).OrderByDescending(e => e.En(d.MaxAnio, d.MinAnio)[M.Ing]).Take(n)
                .Select(e => new KeyValuePair<Empresa, long[]>(e, e.En(d.MaxAnio, d.MinAnio))).ToList();
        }

        public IEnumerable<string> SlugsSector()
        {
            var d = Datos;
            return d.Empresas.Where(e => e.Seccion.Length > 0).Select(e => "seccion-" + e.Seccion.ToLowerInvariant()).Distinct()
                .Concat(d.Empresas.Where(e => e.Division.Length > 0).Select(e => "ciiu-" + e.Division).Distinct()).ToList();
        }
    }
}
