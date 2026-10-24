using System.Web;
using System.Web.Optimization;

namespace bufinsweb
{
    public class BundleConfig
    {
        // Para obtener más información sobre las uniones, visite https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            // Sitio: CSS y JS del layout en bundles para que salgan comprimidos (gzip de Filters/Compresion; el IIS del hosting
            // no comprime los estáticos) y con caché larga. El CSS vive en ~/Assets/css/ para que sus url(../fonts|../webfonts) sigan valiendo.
            bundles.Add(new Bundle("~/Assets/css/base").Include("~/Assets/css/bootstrap.min.css", "~/Assets/css/font-awesome.min.css",
                "~/Assets/css/aos.css", "~/Assets/css/inter-font.css", "~/Assets/css/bufins-custom.css"));
            bundles.Add(new Bundle("~/bundles/base-js").Include("~/Assets/js/bootstrap.bundle.min.js"));
            bundles.Add(new Bundle("~/bundles/aos").Include("~/Assets/js/aos.js"));

            // Pulso: paquetes versionados (?v=hash) con caché larga; el JS se minifica y se concatena por página
            bundles.Add(new Bundle("~/bundles/obs-chart").Include("~/Assets/js/chart.umd.min.js"));
            bundles.Add(new Bundle("~/bundles/obs-css").Include("~/Assets/css/pulso-bufins.css"));
            bundles.Add(new ScriptBundle("~/bundles/obs-core").Include("~/Assets/js/obs-common.js"));
            bundles.Add(new ScriptBundle("~/bundles/obs-sectores").Include("~/Assets/js/obs-common.js", "~/Assets/js/obs-sectores.js"));
            bundles.Add(new ScriptBundle("~/bundles/obs-empresa").Include("~/Assets/js/obs-common.js", "~/Assets/js/obs-empresa.js"));
            bundles.Add(new Bundle("~/bundles/obs-informe-css").Include("~/Assets/css/obs-informe.css"));
            bundles.Add(new ScriptBundle("~/bundles/obs-informe").Include("~/Assets/js/obs-common.js", "~/Assets/js/obs-informe.js"));
            bundles.Add(new ScriptBundle("~/bundles/obs-comparar").Include("~/Assets/js/obs-common.js", "~/Assets/js/obs-comparar.js"));
        }
    }
}
