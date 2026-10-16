using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace bufinsweb.Helpers
{
    /// <summary>Escritor mínimo de archivos .xlsx (OpenXML) sin librerías externas: hojas con texto y números, encabezado en negrita y anchos de columna.</summary>
    public class XlsxWriter
    {
        public class Hoja { public string Nombre; public List<object[]> Filas = new List<object[]>(); public double[] Anchos; public int FilaTitulo = -1; }

        private readonly List<Hoja> _hojas = new List<Hoja>();

        public Hoja Agregar(string nombre, params double[] anchos)
        {
            var n = new string(nombre.Where(c => "[]:*?/\\".IndexOf(c) < 0).ToArray());
            if (n.Length > 31) n = n.Substring(0, 31);
            var h = new Hoja { Nombre = n.Length == 0 ? "Hoja" + (_hojas.Count + 1) : n, Anchos = anchos };
            _hojas.Add(h);
            return h;
        }

        private static string Col(int i)
        {
            var s = "";
            for (i++; i > 0; i = (i - 1) / 26) s = (char)('A' + (i - 1) % 26) + s;
            return s;
        }

        private static string Esc(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in SecurityElement.Escape(s ?? "")) if (c >= ' ' || c == '\t' || c == '\n') sb.Append(c);
            return sb.ToString();
        }

        private static void Entrada(ZipArchive z, string nombre, string contenido)
        {
            var e = z.CreateEntry(nombre, CompressionLevel.Optimal);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(contenido);
        }

        public byte[] Generar()
        {
            using (var ms = new MemoryStream())
            {
                using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var ct = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
                    for (var i = 0; i < _hojas.Count; i++) ct.Append("<Override PartName=\"/xl/worksheets/sheet" + (i + 1) + ".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                    ct.Append("</Types>");
                    Entrada(z, "[Content_Types].xml", ct.ToString());
                    Entrada(z, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");

                    var wb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
                    var rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
                    for (var i = 0; i < _hojas.Count; i++)
                    {
                        wb.Append("<sheet name=\"" + Esc(_hojas[i].Nombre) + "\" sheetId=\"" + (i + 1) + "\" r:id=\"rId" + (i + 1) + "\"/>");
                        rels.Append("<Relationship Id=\"rId" + (i + 1) + "\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet" + (i + 1) + ".xml\"/>");
                    }
                    wb.Append("</sheets></workbook>");
                    rels.Append("<Relationship Id=\"rIdS\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                    Entrada(z, "xl/workbook.xml", wb.ToString());
                    Entrada(z, "xl/_rels/workbook.xml.rels", rels.ToString());
                    // estilos: 0 normal, 1 negrita con fondo, 2 número #,##0.0, 3 título grande
                    Entrada(z, "xl/styles.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"#,##0.0\"/></numFmts><fonts count=\"3\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"14\"/><color rgb=\"FF160933\"/><name val=\"Calibri\"/></font></fonts><fills count=\"3\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF583AFF\"/></patternFill></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"4\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/><xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/><xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs></styleSheet>");

                    for (var i = 0; i < _hojas.Count; i++)
                    {
                        var h = _hojas[i];
                        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
                        if (h.Anchos != null && h.Anchos.Length > 0)
                        {
                            sb.Append("<cols>");
                            for (var c = 0; c < h.Anchos.Length; c++) sb.Append("<col min=\"" + (c + 1) + "\" max=\"" + (c + 1) + "\" width=\"" + h.Anchos[c].ToString(CultureInfo.InvariantCulture) + "\" customWidth=\"1\"/>");
                            sb.Append("</cols>");
                        }
                        sb.Append("<sheetData>");
                        for (var r = 0; r < h.Filas.Count; r++)
                        {
                            var fila = h.Filas[r];
                            sb.Append("<row r=\"" + (r + 1) + "\">");
                            for (var c = 0; c < fila.Length; c++)
                            {
                                var v = fila[c];
                                if (v == null) continue;
                                var celda = Col(c) + (r + 1);
                                if (v is double || v is float || v is int || v is long || v is decimal)
                                {
                                    var d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                                    if (double.IsNaN(d) || double.IsInfinity(d)) continue;
                                    sb.Append("<c r=\"" + celda + "\" s=\"2\"><v>" + d.ToString("R", CultureInfo.InvariantCulture) + "</v></c>");
                                }
                                else
                                {
                                    var estilo = r == 0 && h.FilaTitulo == 0 ? 3 : EsEncabezado(h, r) ? 1 : 0;
                                    sb.Append("<c r=\"" + celda + "\" t=\"inlineStr\" s=\"" + estilo + "\"><is><t xml:space=\"preserve\">" + Esc(v.ToString()) + "</t></is></c>");
                                }
                            }
                            sb.Append("</row>");
                        }
                        sb.Append("</sheetData></worksheet>");
                        Entrada(z, "xl/worksheets/sheet" + (i + 1) + ".xml", sb.ToString());
                    }
                }
                return ms.ToArray();
            }
        }

        /// <summary>Marca como encabezado la primera fila de la hoja, o la siguiente al título si la hoja empieza con uno (FilaTitulo = 0).</summary>
        private static bool EsEncabezado(Hoja h, int fila) { return fila == (h.FilaTitulo == 0 ? 2 : 0); }
    }
}
