using System;
using System.Globalization;
using System.Threading;

namespace bufinsweb.Helpers
{
    /// <summary>Formato de cifras (entrada en MILLONES de COP) para las vistas del Observatorio.</summary>
    public static class ObservatorioFormat
    {
        private static bool En { get { return Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName == "en"; } }
        private static CultureInfo Cul { get { return CultureInfo.GetCultureInfo(En ? "en-US" : "es-CO"); } }

        public static string Moneda(double millones)
        {
            var a = Math.Abs(millones);
            var c = Cul;
            string v;
            if (a >= 1000000) v = (millones / 1000000).ToString("#,##0.##", c) + (En ? " T" : " B");           // billones (1e12)
            else if (a >= 1000) v = (millones / 1000).ToString("#,##0.#", c) + (En ? " B" : " mil M");           // mil millones (1e9)
            else v = millones.ToString("#,##0", c) + " M";
            return "$ " + v;
        }

        public static string Porcentaje(double? v)
        {
            return v.HasValue ? v.Value.ToString("0.0", Cul) + "%" : "—";
        }

        public static string Entero(double v) { return v.ToString("#,##0", Cul); }

        public static string Variacion(double actual, double anterior)
        {
            if (anterior <= 0) return "";
            var v = (actual - anterior) / anterior * 100;
            return (v >= 0 ? "▲ " : "▼ ") + Math.Abs(v).ToString("0.0", Cul) + "%";
        }

        /// <summary>Texto bilingüe en línea para las vistas del Observatorio.</summary>
        public static string T(string es, string en) { return En ? en : es; }
        public static bool EsIngles { get { return En; } }
    }
}
