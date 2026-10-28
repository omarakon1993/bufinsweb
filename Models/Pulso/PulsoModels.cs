using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace bufinsweb.Models.Pulso
{
    /// <summary>Posición de cada métrica en el vector por empresa-año (valores en MILES de COP). Debe coincidir con METRICAS del ETL.</summary>
    public static class M
    {
        public const int Ing = 0, Ub = 1, Gop = 2, Uop = 3, Ebitda = 4, Un = 5, Act = 6, Pas = 7, Pat = 8, Ktn = 9, Deuda = 10,
            Cxc = 11, Inv = 12, Cxp = 13, Fco = 14, Caja = 15, Costo = 16, Da = 17, Cfin = 18, Ac = 19, Pc = 20, Ppye = 21, Gacum = 22;
        public const int Count = 23;
    }

    /// <summary>Indicadores derivados de un vector de cifras. Devuelven null cuando no son calculables.</summary>
    public static class Ratio
    {
        private static double? Div(double a, double b) { return b > 0 ? (double?)(a / b * 100) : null; }

        public static double? MargenBruto(long[] v) { return Div(v[M.Ub], v[M.Ing]); }
        public static double? MargenOperativo(long[] v) { return Div(v[M.Uop], v[M.Ing]); }
        public static double? MargenEbitda(long[] v) { return Div(v[M.Ebitda], v[M.Ing]); }
        public static double? MargenNeto(long[] v) { return Div(v[M.Un], v[M.Ing]); }
        public static double? GastosSobreIngresos(long[] v) { return Div(v[M.Gop], v[M.Ing]); }
        public static double? Roa(long[] v) { return Div(v[M.Un], v[M.Act]); }
        public static double? Roe(long[] v) { return Div(v[M.Un], v[M.Pat]); }
        public static double? Endeudamiento(long[] v) { return Div(v[M.Pas], v[M.Act]); }
        public static double? KtSobreIngresos(long[] v) { return v[M.Ing] > 0 ? (double?)(v[M.Ktn] / (double)v[M.Ing] * 100) : null; }
        public static double? DeudaEbitda(long[] v) { return v[M.Ebitda] > 0 ? (double?)(v[M.Deuda] / (double)v[M.Ebitda]) : null; }
        public static double? Cobertura(long[] v) { return v[M.Cfin] > 0 ? (double?)(v[M.Ebitda] / (double)v[M.Cfin]) : null; }
        public static double? Liquidez(long[] v) { return v[M.Pc] > 0 ? (double?)(v[M.Ac] / (double)v[M.Pc]) : null; }
        public static double? RazonAcida(long[] v) { return v[M.Pc] > 0 ? (double?)((v[M.Ac] - v[M.Inv]) / (double)v[M.Pc]) : null; }
        public static double? DiasCxc(long[] v) { return v[M.Ing] > 0 ? (double?)(v[M.Cxc] / (double)v[M.Ing] * 365) : null; }
        public static double? DiasInv(long[] v) { return v[M.Costo] > 0 ? (double?)(v[M.Inv] / (double)v[M.Costo] * 365) : null; }
        public static double? DiasCxp(long[] v) { return v[M.Costo] > 0 ? (double?)(v[M.Cxp] / (double)v[M.Costo] * 365) : null; }
        public static double? Ciclo(long[] v)
        {
            var a = DiasCxc(v); var b = DiasInv(v); var c = DiasCxp(v);
            return a.HasValue && c.HasValue ? (double?)(a.Value + (b ?? 0) - c.Value) : null;
        }

        // ---- DuPont: ROE = margen neto x rotación de activos x apalancamiento ----
        public static double? Rotacion(long[] v) { return v[M.Act] > 0 ? (double?)(v[M.Ing] / (double)v[M.Act]) : null; }
        public static double? Apalancamiento(long[] v) { return v[M.Pat] > 0 ? (double?)(v[M.Act] / (double)v[M.Pat]) : null; }

        /// <summary>
        /// Altman Z'' para mercados emergentes (empresas no manufactureras / privadas):
        /// Z'' = 3,25 + 6,56·X1 + 3,26·X2 + 6,72·X3 + 1,05·X4, con X1 = capital de trabajo/activos, X2 = utilidades retenidas/activos,
        /// X3 = utilidad operativa/activos y X4 = patrimonio/pasivos. Zonas: &gt; 5,85 segura, 4,15 a 5,85 gris, &lt; 4,15 alerta.
        /// </summary>
        public static double? Altman(long[] v)
        {
            if (v[M.Act] <= 0 || v[M.Pas] <= 0) return null;
            double a = v[M.Act];
            return 3.25 + 6.56 * ((v[M.Ac] - v[M.Pc]) / a) + 3.26 * (v[M.Gacum] / a) + 6.72 * (v[M.Uop] / a) + 1.05 * (v[M.Pat] / (double)v[M.Pas]);
        }

        public static string ZonaAltman(double z) { return z > 5.85 ? "segura" : z >= 4.15 ? "gris" : "alerta"; }

        /// <summary>Indicadores comparables contra el sector: clave, etiqueta (es/en), unidad y "mayor es mejor".</summary>
        public class Def
        {
            public string Clave, Es, En, Unidad; public bool MayorMejor; public Func<long[], double?> F;
        }

        public static readonly Def[] Todos =
        {
            new Def { Clave = "mbruto", Es = "Margen bruto", En = "Gross margin", Unidad = "%", MayorMejor = true, F = MargenBruto },
            new Def { Clave = "moper", Es = "Margen operativo", En = "Operating margin", Unidad = "%", MayorMejor = true, F = MargenOperativo },
            new Def { Clave = "mebitda", Es = "Margen EBITDA", En = "EBITDA margin", Unidad = "%", MayorMejor = true, F = MargenEbitda },
            new Def { Clave = "mneto", Es = "Margen neto", En = "Net margin", Unidad = "%", MayorMejor = true, F = MargenNeto },
            new Def { Clave = "roa", Es = "ROA", En = "ROA", Unidad = "%", MayorMejor = true, F = Roa },
            new Def { Clave = "roe", Es = "ROE", En = "ROE", Unidad = "%", MayorMejor = true, F = Roe },
            new Def { Clave = "rotacion", Es = "Rotación de activos (ingresos/activos)", En = "Asset turnover (revenue/assets)", Unidad = "x", MayorMejor = true, F = Rotacion },
            new Def { Clave = "apalanc", Es = "Apalancamiento (activos/patrimonio)", En = "Equity multiplier (assets/equity)", Unidad = "x", MayorMejor = false, F = Apalancamiento },
            new Def { Clave = "endeud", Es = "Endeudamiento (pasivo/activo)", En = "Leverage (liabilities/assets)", Unidad = "%", MayorMejor = false, F = Endeudamiento },
            new Def { Clave = "deudaebitda", Es = "Deuda / EBITDA", En = "Debt / EBITDA", Unidad = "x", MayorMejor = false, F = DeudaEbitda },
            new Def { Clave = "cobertura", Es = "Cobertura de intereses (EBITDA/costos fin.)", En = "Interest coverage (EBITDA/finance costs)", Unidad = "x", MayorMejor = true, F = Cobertura },
            new Def { Clave = "liquidez", Es = "Liquidez corriente", En = "Current ratio", Unidad = "x", MayorMejor = true, F = Liquidez },
            new Def { Clave = "acida", Es = "Razón ácida", En = "Quick ratio", Unidad = "x", MayorMejor = true, F = RazonAcida },
            new Def { Clave = "ktingresos", Es = "Capital de trabajo / ingresos", En = "Working capital / revenue", Unidad = "%", MayorMejor = false, F = KtSobreIngresos },
            new Def { Clave = "diascxc", Es = "Días de cartera", En = "Receivable days", Unidad = "d", MayorMejor = false, F = DiasCxc },
            new Def { Clave = "diasinv", Es = "Días de inventario", En = "Inventory days", Unidad = "d", MayorMejor = false, F = DiasInv },
            new Def { Clave = "diascxp", Es = "Días de proveedores", En = "Payable days", Unidad = "d", MayorMejor = true, F = DiasCxp },
            new Def { Clave = "ciclo", Es = "Ciclo de conversión de efectivo", En = "Cash conversion cycle", Unidad = "d", MayorMejor = false, F = Ciclo },
            new Def { Clave = "altman", Es = "Altman Z'' (riesgo de quiebra)", En = "Altman Z'' (distress risk)", Unidad = "x", MayorMejor = true, F = Altman },
            new Def { Clave = "score", Es = "Bufins Score de salud", En = "Bufins health score", Unidad = "pts", MayorMejor = true, F = v => Salud.Calcular(v).Total }
        };
    }

    /// <summary>
    /// Bufins Score de salud (0 a 100). Cuatro pilares de 25 puntos, cada uno el promedio de dos indicadores puntuados en forma lineal
    /// entre un piso (0 puntos) y un techo (puntaje completo). Las reglas son públicas y se muestran en la ficha.
    /// </summary>
    public static class Salud
    {
        public class Pilar { public string Clave, Es, En; public double Puntos, Max = 25; public string[] Detalle; }
        public class Resultado { public double Total; public string Nivel; public List<Pilar> Pilares = new List<Pilar>(); }

        private static double Lin(double? x, double piso, double techo)
        {
            if (!x.HasValue || double.IsNaN(x.Value)) return 0;
            return Math.Max(0, Math.Min(1, (x.Value - piso) / (techo - piso)));
        }

        public static string Nivel(double total) { return total >= 70 ? "solida" : total >= 50 ? "estable" : total >= 30 ? "vigilar" : "critica"; }

        public static Resultado Calcular(long[] v)
        {
            var r = new Resultado();
            // Rentabilidad: margen EBITDA (0 % a 15 %) y ROA (0 % a 8 %)
            var rent = (Lin(Ratio.MargenEbitda(v), 0, 15) + Lin(Ratio.Roa(v), 0, 8)) / 2;
            // Liquidez: liquidez corriente (0,6x a 1,5x) y capital de trabajo / ingresos (-10 % a 5 %)
            var liq = (Lin(Ratio.Liquidez(v), 0.6, 1.5) + Lin(Ratio.KtSobreIngresos(v), -10, 5)) / 2;
            // Endeudamiento: deuda / EBITDA (6x a 1x; sin deuda = completo; con deuda y EBITDA no positivo = 0) y pasivo/activo (95 % a 50 %)
            double sDeuda;
            if (v[M.Deuda] <= 0) sDeuda = 1;
            else if (v[M.Ebitda] <= 0) sDeuda = 0;
            else sDeuda = 1 - Lin(Ratio.DeudaEbitda(v), 1, 6);
            var endeud = (sDeuda + (Ratio.Endeudamiento(v).HasValue ? 1 - Lin(Ratio.Endeudamiento(v), 50, 95) : 0)) / 2;
            // Solvencia: cobertura de intereses (1x a 5x; sin costos financieros = completo) y Altman Z'' (4,15 a 5,85)
            var sCob = v[M.Cfin] <= 0 ? 1 : Lin(Ratio.Cobertura(v), 1, 5);
            var solv = (sCob + Lin(Ratio.Altman(v), 4.15, 5.85)) / 2;

            r.Pilares.Add(new Pilar { Clave = "rentabilidad", Es = "Rentabilidad", En = "Profitability", Puntos = Math.Round(rent * 25, 1) });
            r.Pilares.Add(new Pilar { Clave = "liquidez", Es = "Liquidez", En = "Liquidity", Puntos = Math.Round(liq * 25, 1) });
            r.Pilares.Add(new Pilar { Clave = "endeudamiento", Es = "Endeudamiento", En = "Leverage", Puntos = Math.Round(endeud * 25, 1) });
            r.Pilares.Add(new Pilar { Clave = "solvencia", Es = "Solvencia", En = "Solvency", Puntos = Math.Round(solv * 25, 1) });
            r.Total = Math.Round(r.Pilares.Sum(p => p.Puntos), 1);
            r.Nivel = Nivel(r.Total);
            return r;
        }
    }

    public class Empresa
    {
        public string Nit, Razon, Ciiu, Division, Seccion, Ciudad, Depto, Estado, FConst, Tipo, Grupo, Busqueda, Slug;
        /// <summary>Vectores por año (índice = año - MinAnio); null si no hay reporte ese año.</summary>
        public long[][] Y;
        /// <summary>Ingresos (miles COP) del último año reportado; se usa para ordenar resultados de búsqueda.</summary>
        public long RankIng;
        public long[] En(int anio, int minAnio) { var i = anio - minAnio; return i >= 0 && i < Y.Length ? Y[i] : null; }
    }

    public class PulsoMeta
    {
        [JsonProperty("fuente")] public string Fuente;
        [JsonProperty("fuenteActualizado")] public string FuenteActualizado;
        [JsonProperty("generado")] public string Generado;
        [JsonProperty("anios")] public List<int> Anios = new List<int>();
        [JsonProperty("empresas")] public int Empresas;
        [JsonProperty("unidad")] public string Unidad;
        /// <summary>Archivos de detalle versionados (los escribe el ETL); si faltan se usan los nombres antiguos.</summary>
        [JsonProperty("detalle")] public string Detalle;
        [JsonProperty("indice")] public string Indice;
    }

    // ---- Reglas del puntaje de prospecto (App_Data/pulso/reglas.json, editable) ----
    public class Senal
    {
        [JsonProperty("puntos")] public double Puntos;
        [JsonProperty("tipo")] public string Tipo;      // "presion" | "oportunidad"
        [JsonProperty("es")] public string Es;
        [JsonProperty("en")] public string En;
        [JsonProperty("regla")] public string Regla;
    }

    public class ReglasProspecto
    {
        [JsonProperty("tamano")] public Dictionary<string, double> Tamano = new Dictionary<string, double> { { "S", 0 }, { "M", 15 }, { "L", 20 }, { "XL", 10 } };
        [JsonProperty("senales")] public Dictionary<string, Senal> Senales = new Dictionary<string, Senal>();
    }

    /// <summary>Señales detectadas en una empresa-año y el potencial (0-100) que suman según las reglas.</summary>
    public class SenalesInfo
    {
        public List<string> Claves = new List<string>();
        public int Potencial;
    }

    public class PulsoDatos
    {
        public PulsoMeta Meta;
        public List<Empresa> Empresas = new List<Empresa>();
        public Dictionary<string, Empresa> PorNit = new Dictionary<string, Empresa>();
        public Dictionary<string, long[]> Indice = new Dictionary<string, long[]>();   // NIT -> [offset, longitud] en detalle.bin
        public int MinAnio, MaxAnio;
        /// <summary>Identifica la versión de los datos cargados (fecha de escritura del archivo); sirve para versionar las URL de la API en caché.</summary>
        public string Version;
        /// <summary>Nombre del archivo de detalle vigente (detalle.{version}.bin).</summary>
        public string DetalleArchivo = "detalle.bin";
        /// <summary>Empresas por clase CIIU (4 dígitos): evita recorrer todas las empresas para sectores y benchmarks.</summary>
        public Dictionary<string, List<Empresa>> PorClase = new Dictionary<string, List<Empresa>>();
        /// <summary>Empresas por nodo CIIU (sección, división, grupo o clase) y año (índice = año - MinAnio).</summary>
        public Dictionary<string, int[]> ConteoNodo = new Dictionary<string, int[]>();
        /// <summary>NIT -> [número de contratos, valor en COP] con el Estado (SECOP II) desde ContratosDesde.</summary>
        public Dictionary<string, long[]> Contratos = new Dictionary<string, long[]>();
        public string ContratosDesde;
        /// <summary>Año -> [TRM promedio, TRM de cierre].</summary>
        public Dictionary<int, double[]> Trm = new Dictionary<int, double[]>();
        /// <summary>Año -> inflación anual (%), tabla DANE.</summary>
        public Dictionary<int, double> Ipc = new Dictionary<int, double>();
        public ReglasProspecto Reglas = new ReglasProspecto();
    }

    public class FiltroSector
    {
        public int? Anio;
        public List<string> Ciiu = new List<string>();       // nodos: sección (letra), división, grupo o clase
        public List<string> Tamanos = new List<string>();    // S, M, L, XL (por ingresos del año)
        public string Departamento;
        public string Estado;
        public string Q;
        public double? IngMin, IngMax;                       // millones de COP
        public string Resultado;                             // "utilidad" | "perdida"
        public List<string> Senales = new List<string>();    // claves de reglas.json: la empresa debe tenerlas todas
        public int PotencialMin;                             // 0 a 100
        public string Orden = "ing";
        public bool Desc = true;
        public int Pagina = 1;
        public int Tamano = 25;
        public int Top;                                      // 0 = sin límite; N = solo las N primeras según el orden (tabla y descargas)
    }
}
