using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace VitaMj.Config.Editor
{
    /// <summary>
    /// 读取 .xlsx 中所有工作表，按行解析为「列索引 → 单元格字符串」。
    /// </summary>
    static class ConfigXlsxBookReader
    {
        const string RelOfficeDocument =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string RelPackage =
            "http://schemas.openxmlformats.org/package/2006/relationships";
        const string MainSpreadsheetMl =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        public sealed class SheetData
        {
            public string Name;
            public string PathInZip;
            /// <summary>1-based Excel 行号 → 列0起 → 文本</summary>
            public SortedDictionary<int, Dictionary<int, string>> Rows = new SortedDictionary<int, Dictionary<int, string>>();
        }

        public static List<SheetData> ReadAllSheets(string xlsxPath)
        {
            try
            {
                return ReadFromDisk(xlsxPath);
            }
            catch (IOException ex)
            {
                return ReadViaTempCopy(xlsxPath, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ReadViaTempCopy(xlsxPath, ex);
            }
        }

        static List<SheetData> ReadViaTempCopy(string xlsxPath, Exception original)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "VitaMjConfig_" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                File.Copy(xlsxPath, tmp, overwrite: true);
                return ReadFromDisk(tmp);
            }
            catch (Exception inner)
            {
                Debug.LogError($"[Config] 无法读取 xlsx：{original.Message}\n{inner}");
                return new List<SheetData>();
            }
            finally
            {
                try
                {
                    if (File.Exists(tmp))
                        File.Delete(tmp);
                }
                catch { /* ignore */ }
            }
        }

        static List<SheetData> ReadFromDisk(string xlsxPath)
        {
            using FileStream fs = new FileStream(
                xlsxPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

            List<string> sharedStrings = ReadSharedStrings(zip);
            List<(string name, string path)> sheets = EnumerateSheets(zip);
            var result = new List<SheetData>();

            XNamespace m = MainSpreadsheetMl;
            foreach (var (name, path) in sheets)
            {
                ZipArchiveEntry entry = zip.GetEntry(path.Replace('\\', '/'));
                if (entry == null)
                {
                    Debug.LogWarning($"[Config] 找不到工作表文件：{path}");
                    continue;
                }

                XDocument sheetDoc;
                using (Stream st = entry.Open())
                    sheetDoc = XDocument.Load(st);

                var sd = new SheetData { Name = name, PathInZip = path };
                foreach (XElement rowEl in sheetDoc.Descendants(m + "row"))
                {
                    if (!int.TryParse(rowEl.Attribute("r")?.Value, out int row1Based))
                        continue;

                    var cellsByCol = new Dictionary<int, string>();
                    foreach (XElement c in rowEl.Elements(m + "c"))
                    {
                        string refAttr = c.Attribute("r")?.Value;
                        if (string.IsNullOrEmpty(refAttr))
                            continue;
                        if (!TryParseCellReference(refAttr, out int col0, out _))
                            continue;
                        cellsByCol[col0] = ReadCellString(c, sharedStrings, m);
                    }

                    if (cellsByCol.Count > 0)
                        sd.Rows[row1Based] = cellsByCol;
                }

                result.Add(sd);
            }

            return result;
        }

        static List<(string name, string path)> EnumerateSheets(ZipArchive zip)
        {
            ZipArchiveEntry wbEntry = zip.GetEntry("xl/workbook.xml")
                ?? throw new InvalidOperationException("无效的 xlsx：缺少 xl/workbook.xml");

            XNamespace m = MainSpreadsheetMl;
            XNamespace r = RelOfficeDocument;

            XDocument wbDoc;
            using (Stream st = wbEntry.Open())
                wbDoc = XDocument.Load(st);

            ZipArchiveEntry relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels")
                ?? throw new InvalidOperationException("缺少 xl/_rels/workbook.xml.rels");

            XNamespace relNs = RelPackage;
            XDocument relDoc;
            using (Stream st = relsEntry.Open())
                relDoc = XDocument.Load(st);

            var ridToTarget = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XElement rel in relDoc.Descendants(relNs + "Relationship"))
            {
                string id = rel.Attribute("Id")?.Value;
                string target = rel.Attribute("Target")?.Value;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                    ridToTarget[id] = target.Replace('\\', '/').TrimStart('/');
            }

            var list = new List<(string name, string path)>();
            foreach (XElement sheet in wbDoc.Descendants(m + "sheet"))
            {
                string name = sheet.Attribute("name")?.Value ?? "Sheet";
                XAttribute idAttr = sheet.Attribute(r + "id");
                if (idAttr == null)
                    continue;
                if (!ridToTarget.TryGetValue(idAttr.Value, out string target))
                    continue;

                string full = target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                    ? target
                    : "xl/" + target;
                list.Add((name, full));
            }

            return list;
        }

        static List<string> ReadSharedStrings(ZipArchive zip)
        {
            ZipArchiveEntry entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return new List<string>();

            XNamespace m = MainSpreadsheetMl;
            using Stream st = entry.Open();
            XDocument doc = XDocument.Load(st);

            var list = new List<string>();
            foreach (XElement si in doc.Descendants(m + "si"))
            {
                XElement plain = si.Element(m + "t");
                if (plain != null)
                {
                    list.Add(plain.Value);
                    continue;
                }

                var sb = new System.Text.StringBuilder();
                foreach (XElement rt in si.Elements(m + "r"))
                {
                    XElement t = rt.Element(m + "t");
                    if (t != null)
                        sb.Append(t.Value);
                }

                list.Add(sb.ToString());
            }

            return list;
        }

        static bool TryParseCellReference(string cellRef, out int col0, out int row1Based)
        {
            col0 = 0;
            row1Based = 0;
            if (string.IsNullOrEmpty(cellRef))
                return false;

            int i = 0;
            while (i < cellRef.Length && char.IsLetter(cellRef[i]))
                i++;

            if (i == 0 || i >= cellRef.Length)
                return false;

            string colLetters = cellRef.Substring(0, i).ToUpperInvariant();
            string rowPart = cellRef.Substring(i);

            int col = 0;
            foreach (char ch in colLetters)
            {
                if (ch < 'A' || ch > 'Z')
                    return false;
                col = col * 26 + (ch - 'A' + 1);
            }

            col0 = col - 1;
            return int.TryParse(rowPart, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out row1Based);
        }

        static string ReadCellString(XElement c, List<string> sharedStrings, XNamespace m)
        {
            XAttribute tAttr = c.Attribute("t");
            string cellType = tAttr?.Value;

            XElement v = c.Element(m + "v");
            XElement isEl = c.Element(m + "is");

            if (cellType == "s")
            {
                if (v != null && int.TryParse(v.Value, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int idx))
                {
                    if ((uint)idx < (uint)sharedStrings.Count)
                        return sharedStrings[idx];
                }

                return string.Empty;
            }

            if (cellType == "inlineStr" && isEl != null)
            {
                XElement t = isEl.Descendants(m + "t").FirstOrDefault();
                return t?.Value ?? string.Empty;
            }

            if (v != null)
                return v.Value;

            return string.Empty;
        }
    }
}
