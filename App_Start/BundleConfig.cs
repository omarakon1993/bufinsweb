using System.Web;
using System.Web.Optimization;

namespace bufinsweb
{
    public class BundleConfig
    {
        // Para obtener más información sobre las uniones, visite https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            // Bundles para archivos locales en Assets
            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Assets/js/bootstrap.bundle.min.js"));

            bundles.Add(new ScriptBundle("~/bundles/aos").Include(
                      "~/Assets/js/aos.js"));

            bundles.Add(new StyleBundle("~/bundles/css").Include(
                      "~/Assets/css/bootstrap.min.css",
                      "~/Assets/css/font-awesome.min.css",
                      "~/Assets/css/aos.css",
                      "~/Assets/css/inter-font.css"));
        }
    }
}
