using System;
using System.Collections.Generic;
using System.Linq;
using bufinsweb.Helpers;
using bufinsweb.Models.Pulso;

namespace bufinsweb.Services
{
    /// <summary>Parte 3 del servicio: libros de Excel (.xlsx) de la ficha y de las listas de sector.</summary>
    public partial class PulsoService
    {
        private const string Fuente = "Fuente: Superintendencia de Sociedades (datos.gov.co), estados financieros NIIF, licencia CC BY-SA 4.0 (creativecommons.org/licenses/by-sa/4.0). Clasificación CIIU Rev. 4 A.C.: Departamento Administrativo Nacional de Estadística: www.dane.gov.co. Cálculos de Bufins (bufins.com).";

        public byte[] XlsxEmpresa(string nit, Func<string, string> etiqueta, IList<string> orden)
        {
            var d = Datos;
            var e = PorNit(nit);
            if (e == null) return null;
            var cat = CiiuCatalogo.Actual;
            var anios = d.Meta.Anios.OrderBy(a => a).Where(a => e.En(a, d.MinAnio) != null).ToList();
            var x = new XlsxWriter();

            // Resumen: identificación + indicadores por año
            var h = x.Agregar("Resumen", 44, 16, 16, 16, 16, 16, 16);
            h.FilaTitulo = 0;
            h.Filas.Add(new object[] { e.Razon + " · NIT " + e.Nit });
            h.Filas.Add(new object[] { cat.Nombre(e.Ciiu) + " (CIIU " + e.Ciiu + ") · " + e.Ciudad + ", " + e.Depto + " · " + e.Estado });
            h.Filas.Add(new object[] { "Indicador" }.Concat(anios.Select(a => (object)a.ToString())).ToArray());
            Action<string, Func<long[], double?>> ind = (nombre, f) =>
                h.Filas.Add(new object[] { nombre }.Concat(anios.Select(a => (object)f(e.En(a, d.MinAnio)))).ToArray());
            Action<string, int> cifra = (nombre, m) =>
                h.Filas.Add(new object[] { nombre + " (millones COP)" }.Concat(anios.Select(a => (object)(e.En(a, d.MinAnio)[m] / 1000.0))).ToArray());
            cifra("Ingresos", M.Ing); cifra("EBITDA", M.Ebitda); cifra("Utilidad neta", M.Un); cifra("Activos", M.Act); cifra("Pasivos", M.Pas); cifra("Patrimonio", M.Pat); cifra("Deuda financiera", M.Deuda);
            foreach (var def in Ratio.Todos) { var f = def.F; ind(def.Es + " (" + def.Unidad + ")", f); }
            h.Filas.Add(new object[] { });
            h.Filas.Add(new object[] { Fuente });

            // Estados: resultados, balance y flujo (cifras en millones de COP)
            Dictionary<string, string> p;
            var rub = LeerDetalle(nit, out p);
            var anR = rub.Keys.OrderBy(k => k).ToList();
            Action<string, int, int> hoja = (nombre, desde, hasta) =>
            {
                var s = x.Agregar(nombre, 52, 16, 16, 16, 16, 16, 16);
                s.Filas.Add(new object[] { "Rubro (millones de COP)" }.Concat(anR.Select(a => (object)a)).ToArray());
                for (var i = desde; i <= hasta && i < orden.Count; i++)
                {
                    var k = orden[i];
                    s.Filas.Add(new object[] { etiqueta(k) }.Concat(anR.Select(a => { long v; return (object)(rub[a].TryGetValue(k, out v) ? v / 1000.0 : 0.0); })).ToArray());
                }
                s.Filas.Add(new object[] { });
                s.Filas.Add(new object[] { Fuente });
            };
            var iCaja = orden.IndexOf("caja"); var iFco = orden.IndexOf("fco");
            hoja("Resultados", 0, iCaja - 1);
            hoja("Balance", iCaja, iFco - 1);
            hoja("Flujo de efectivo", iFco, orden.Count - 1);

            // Frente al sector (último año)
            var anio = anios.Last();
            var v0 = e.En(anio, d.MinAnio);
            string nivel, codigo; int n;
            GrupoComparable(d, e, anio, out nivel, out codigo);
            var q = Cuartiles(d, nivel, codigo, anio, out n);
            var b = x.Agregar("Frente al sector", 48, 14, 14, 14, 14, 14, 16);
            b.Filas.Add(new object[] { "Indicador", "Empresa", "P25", "Mediana", "P75", "Empresas", "Mejor que (%)" });
            for (var i = 0; i < Ratio.Todos.Length; i++)
            {
                var def = Ratio.Todos[i]; var arr = q[i];
                if (arr.Length < 5) continue;
                var val = def.F(v0);
                double? mejor = null;
                if (val.HasValue) { var m = arr.Count(z => z < val.Value) + arr.Count(z => z == val.Value) / 2.0; var pct = m / arr.Length * 100; mejor = def.MayorMejor ? pct : 100 - pct; }
                b.Filas.Add(new object[] { def.Es + " (" + def.Unidad + ")", val, Percentil(arr, .25), Percentil(arr, .5), Percentil(arr, .75), (double)arr.Length, mejor });
            }
            b.Filas.Add(new object[] { });
            b.Filas.Add(new object[] { "Grupo comparable (" + anio + "): " + (codigo.Length > 0 ? cat.Nombre(codigo) : "todas las empresas") + " · " + n.ToString("N0") + " empresas." });
            b.Filas.Add(new object[] { Fuente });
            return x.Generar();
        }

        public byte[] XlsxSector(FiltroSector f)
        {
            var d = Datos;
            var cat = CiiuCatalogo.Actual;
            var anio = f.Anio.HasValue && d.Meta.Anios.Contains(f.Anio.Value) ? f.Anio.Value : d.MaxAnio;
            var x = new XlsxWriter();
            var h = x.Agregar("Empresas " + anio, 14, 42, 8, 36, 18, 18, 18, 8, 14, 14, 14, 14, 14, 14, 14, 14, 14, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11, 11);
            h.Filas.Add(new object[] { "NIT", "Razón social", "CIIU", "Actividad", "Ciudad", "Departamento", "Estado", "Tamaño", "Ingresos (millones COP)", "Utilidad bruta", "Utilidad operativa", "EBITDA", "Utilidad neta",
                "Activos", "Pasivos", "Patrimonio", "Deuda financiera", "Margen bruto %", "Margen operativo %", "Margen EBITDA %", "Margen neto %", "Deuda/EBITDA (x)", "KT/Ingresos %", "Crecimiento %", "Score salud", "Potencial", "Señales" });
            var conSen = true;
            foreach (var r in Ordenar(Filtrar(d, f, anio), f.Orden, f.Desc).Take(MaxExportar))
            {
                var v = r.V;
                var sen = r.Sen ?? (conSen ? CalcularSenales(d, r.E, v, r.V1, r.V2) : null);
                Func<long, object> mm = z => z / 1000.0;
                h.Filas.Add(new object[] { r.E.Nit, r.E.Razon, r.E.Ciiu, cat.Nombre(r.E.Ciiu), r.E.Ciudad, r.E.Depto, r.E.Estado, Tamano(v[M.Ing]),
                    mm(v[M.Ing]), mm(v[M.Ub]), mm(v[M.Uop]), mm(v[M.Ebitda]), mm(v[M.Un]), mm(v[M.Act]), mm(v[M.Pas]), mm(v[M.Pat]), mm(v[M.Deuda]),
                    Ratio.MargenBruto(v), Ratio.MargenOperativo(v), Ratio.MargenEbitda(v), Ratio.MargenNeto(v), Ratio.DeudaEbitda(v), Ratio.KtSobreIngresos(v), r.Crec,
                    Salud.Calcular(v).Total, (double)sen.Potencial, string.Join(", ", sen.Claves.Select(c => { Senal s; return d.Reglas.Senales.TryGetValue(c, out s) ? s.Es : c; })) });
            }
            h.Filas.Add(new object[] { });
            h.Filas.Add(new object[] { Fuente + " Año " + anio + "." });
            return x.Generar();
        }
    }
}
