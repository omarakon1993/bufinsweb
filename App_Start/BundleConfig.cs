using System.Web;
using System.Web.Optimization;

namespace bufinsweb
{
    public class BundleConfig
    {
        // Para obtener más información sobre las uniones, visite https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            // Observatorio: paquetes versionados (?v=hash) con caché larga; el JS se minifica y se concatena por página
            bundles.Add(new Bundle("~/bundles/obs-chart").Include("~/Assets/js/chart.umd.min.js"));
            bundles.Add(new Bundle("~/bundles/obs-css").Include("~/Assets/css/observatorio.css"));
            bundles.Add(new ScriptBundle("~/bundles/obs-core").Include("~/Assets/js/obs-common.js"));
            bundles.Add(new ScriptBundle("~/bundles/obs-sectores").Include("~/Assets/js/obs-common.js", "~/Assets/js/obs-sectores.js"));
            bundles.Add(new ScriptBundle("~/bundles/obs-empresa").Include("~/Assets/js/obs-common.js", "~/Assets/js/obs-empresa.js"));
            bundles.Add(new ScriptBundle("~/bundles/obs-comparar").Include("~/Assets/js/obs-common.js", "~/Assets/js/obs-comparar.js"));
        }
    }
}
