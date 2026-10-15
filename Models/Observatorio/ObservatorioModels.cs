using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace bufinsweb.Models.Observatorio
{
    /// <summary>Posición de cada métrica en el vector por empresa-año (valores en MILES de COP). Debe coincidir con METRICAS del ETL.</summary>
    public static class M
    {
        public const int Ing = 0, Ub = 1, Gop = 2, Uop = 3, Ebitda = 4, Un = 5, Act = 6, Pas = 7, Pat = 8, Ktn = 9, Deuda = 10,
            Cxc = 11, Inv = 12, Cxp = 13, Fco = 14, Caja = 15, Costo = 16, Da = 17, Cfin = 18, Ac = 19, Pc = 20, Ppye = 21;
        public const int Count = 22;
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
            new Def { Clave = "endeud", Es = "Endeudamiento (pasivo/activo)", En = "Leverage (liabilities/assets)", Unidad = "%", MayorMejor = false, F = Endeudamiento },
            new Def { Clave = "deudaebitda", Es = "Deuda / EBITDA", En = "Debt / EBITDA", Unidad = "x", MayorMejor = false, F = DeudaEbitda },
            new Def { Clave = "cobertura", Es = "Cobertura de intereses (EBITDA/costos fin.)", En = "Interest coverage (EBITDA/finance costs)", Unidad = "x", MayorMejor = true, F = Cobertura },
            new Def { Clave = "liquidez", Es = "Liquidez corriente", En = "Current ratio", Unidad = "x", MayorMejor = true, F = Liquidez },
            new Def { Clave = "acida", Es = "Razón ácida", En = "Quick ratio", Unidad = "x", MayorMejor = true, F = RazonAcida },
            new Def { Clave = "ktingresos", Es = "Capital de trabajo / ingresos", En = "Working capital / revenue", Unidad = "%", MayorMejor = false, F = KtSobreIngresos },
            new Def { Clave = "diascxc", Es = "Días de cartera", En = "Receivable days", Unidad = "d", MayorMejor = false, F = DiasCxc },
            new Def { Clave = "diasinv", Es = "Días de inventario", En = "Inventory days", Unidad = "d", MayorMejor = false, F = DiasInv },
            new Def { Clave = "diascxp", Es = "Días de proveedores", En = "Payable days", Unidad = "d", MayorMejor = true, F = DiasCxp },
            new Def { Clave = "ciclo", Es = "Ciclo de conversión de efectivo", En = "Cash conversion cycle", Unidad = "d", MayorMejor = false, F = Ciclo }
        };
    }

    public class Empresa
    {
        public string Nit, Razon, Ciiu, Division, Seccion, Ciudad, Depto, Estado, FConst, Tipo, Grupo, Busqueda;
        /// <summary>Vectores por año (índice = año - MinAnio); null si no hay reporte ese año.</summary>
        public long[][] Y;
        /// <summary>Ingresos (miles COP) del último año reportado; se usa para ordenar resultados de búsqueda.</summary>
        public long RankIng;
        public long[] En(int anio, int minAnio) { var i = anio - minAnio; return i >= 0 && i < Y.Length ? Y[i] : null; }
    }

    public class ObservatorioMeta
    {
        [JsonProperty("fuente")] public string Fuente;
        [JsonProperty("fuenteActualizado")] public string FuenteActualizado;
        [JsonProperty("generado")] public string Generado;
        [JsonProperty("anios")] public List<int> Anios = new List<int>();
        [JsonProperty("empresas")] public int Empresas;
        [JsonProperty("unidad")] public string Unidad;
    }

    public class ObservatorioDatos
    {
        public ObservatorioMeta Meta;
        public List<Empresa> Empresas = new List<Empresa>();
        public Dictionary<string, Empresa> PorNit = new Dictionary<string, Empresa>();
        public Dictionary<string, long[]> Indice = new Dictionary<string, long[]>();   // NIT -> [offset, longitud] en detalle.bin
        public int MinAnio, MaxAnio;
    }

    // Formato crudo de empresas.json
    public class RawArchivo
    {
        [JsonProperty("meta")] public ObservatorioMeta Meta;
    }

    public class RawEmpresa
    {
        [JsonProperty("n")] public string N;
        [JsonProperty("r")] public string R;
        [JsonProperty("k")] public string K;
        [JsonProperty("c")] public string C;
        [JsonProperty("d")] public string D;
        [JsonProperty("s")] public string S;
        [JsonProperty("f")] public string F;
        [JsonProperty("t")] public string T;
        [JsonProperty("g")] public string G;
        [JsonProperty("y")] public Dictionary<string, long[]> Y;
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
        public string Orden = "ing";
        public bool Desc = true;
        public int Pagina = 1;
        public int Tamano = 25;
    }
}
