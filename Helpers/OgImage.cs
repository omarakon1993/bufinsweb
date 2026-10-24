using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace bufinsweb.Helpers
{
    /// <summary>Genera la imagen para compartir (1200×630, JPEG) de una ficha o un sector, con la marca de Bufins.</summary>
    public static class OgImage
    {
        public class Kpi { public string Etiqueta, Valor; }

        private static Font Fuente(float px, FontStyle st)
        {
            foreach (var n in new[] { "Segoe UI", "Arial" })
            {
                try { return new Font(n, px, st, GraphicsUnit.Pixel); } catch (Exception) { }
            }
            return new Font(FontFamily.GenericSansSerif, px, st, GraphicsUnit.Pixel);
        }

        public static byte[] Generar(string titulo, string subtitulo, IList<Kpi> kpis, string pie, string rutaLogo)
        {
            using (var bmp = new Bitmap(1200, 630))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                using (var br = new LinearGradientBrush(new Rectangle(0, 0, 1200, 630), ColorTranslator.FromHtml("#160933"), ColorTranslator.FromHtml("#3b25a8"), 35f))
                    g.FillRectangle(br, 0, 0, 1200, 630);
                using (var cian = new SolidBrush(ColorTranslator.FromHtml("#01E1D7"))) g.FillRectangle(cian, 0, 0, 14, 630);

                var f = new StringFormat { Trimming = StringTrimming.EllipsisWord };
                using (var ft = Fuente(54, FontStyle.Bold))
                using (var fs = Fuente(26, FontStyle.Regular))
                using (var fv = Fuente(50, FontStyle.Bold))
                using (var fl = Fuente(22, FontStyle.Regular))
                using (var fp = Fuente(22, FontStyle.Regular))
                using (var blanco = new SolidBrush(Color.White))
                using (var suave = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                using (var cian = new SolidBrush(ColorTranslator.FromHtml("#01E1D7")))
                {
                    g.DrawString("Pulso Bufins", fs, cian, 70, 60);
                    var altoTit = g.MeasureString(titulo ?? "", ft, 1060).Height;
                    g.DrawString(titulo ?? "", ft, blanco, new RectangleF(70, 108, 1060, 140), f);
                    g.DrawString(subtitulo ?? "", fs, suave, new RectangleF(70, Math.Min(250, 108 + altoTit + 12), 1060, 70), f);

                    var n = Math.Max(1, Math.Min(4, kpis.Count));
                    var ancho = (1060 - (n - 1) * 20) / n;
                    for (var i = 0; i < kpis.Count && i < 4; i++)
                    {
                        var x = 70 + i * (ancho + 20);
                        using (var caja = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillRectangle(caja, x, 350, ancho, 130);
                        // el valor se reduce hasta caber en la caja
                        var tam = 50f; Font fit = null;
                        for (; tam >= 24f; tam -= 2f)
                        {
                            fit = Fuente(tam, FontStyle.Bold);
                            if (g.MeasureString(kpis[i].Valor ?? "", fit).Width <= ancho - 30) break;
                            fit.Dispose(); fit = null;
                        }
                        if (fit == null) fit = Fuente(24f, FontStyle.Bold);
                        g.DrawString(kpis[i].Valor ?? "", fit, blanco, x + 16, 366 + (50f - tam) / 2);
                        fit.Dispose();
                        g.DrawString(kpis[i].Etiqueta ?? "", fl, suave, x + 18, 436);
                    }
                    g.DrawString(pie ?? "", fp, suave, 70, 545);
                }
                try
                {
                    if (File.Exists(rutaLogo))
                        using (var logo = Image.FromFile(rutaLogo))
                        {
                            var h = 40; var w = (int)(logo.Width * (h / (double)logo.Height));
                            using (var pill = new SolidBrush(Color.White)) g.FillRectangle(pill, 1130 - w - 14, 536, w + 28, h + 12);
                            g.DrawImage(logo, 1130 - w, 542, w, h);
                        }
                }
                catch (Exception) { }
                // JPEG y no PNG: el degradado pesaba ~175 KB en PNG
                using (var ms = new MemoryStream())
                using (var prm = new EncoderParameters(1))
                {
                    prm.Param[0] = new EncoderParameter(Encoder.Quality, 85L);
                    var jpeg = Array.Find(ImageCodecInfo.GetImageEncoders(), c => c.FormatID == ImageFormat.Jpeg.Guid);
                    bmp.Save(ms, jpeg, prm);
                    return ms.ToArray();
                }
            }
        }
    }
}
