using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using VitaMj.MatchGame;

/// <summary>
/// 读取关卡配置 .xlsx（首个工作表，列为 tag | level | JSON）。
/// </summary>
static class XlsxLevelConfigParser
{
    const string RelOfficeDocument =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    const string RelPackage =
        "http://schemas.openxmlformats.org/package/2006/relationships";
    const string MainSpreadsheetMl =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static List<LevelConfigRow> Parse(string xlsxPath)
    {
        try
        {
            return ParseFromDisk(xlsxPath);
        }
        catch (IOException ex)
        {
            return ParseViaTempCopy(xlsxPath, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ParseViaTempCopy(xlsxPath, ex);
        }
    }

    static List<LevelConfigRow> ParseViaTempCopy(string xlsxPath, Exception original)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "VitaMjLevelImport_" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            File.Copy(xlsxPath, tmp, overwrite: true);
            return ParseFromDisk(tmp);
        }
        catch (IOException inner)
        {
            throw new IOException(
                "无法读取关卡 xlsx（可能被占用或权限不足）。请先关闭 Excel 中该文件再导入，或将表格另存到项目目录后重试。\n"
                + original.Message,
                inner);
        }
        catch (UnauthorizedAccessException inner)
        {
            throw new IOException(
                "无法读取关卡 xlsx（可能被占用或权限不足）。请先关闭 Excel 中该文件再导入，或将表格另存到项目目录后重试。\n"
                + original.Message,
                inner);
        }
        finally
        {
            TryDeleteFile(tmp);
        }
    }

    static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            /* ignore */
        }
    }

    static List<LevelConfigRow> ParseFromDisk(string xlsxPath)
    {
        // Excel 打开工作簿时常持有写入锁；File.OpenRead 使用 FileShare.Read，会与独占写入冲突。
        using FileStream fs = new FileStream(
            xlsxPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

        List<string> sharedStrings = ReadSharedStrings(zip);
        string sheetPath = ResolveFirstWorksheetPath(zip);
        ZipArchiveEntry sheetEntry = zip.GetEntry(sheetPath)
            ?? throw new InvalidOperationException($"未找到工作表：{sheetPath}");

        XNamespace m = MainSpreadsheetMl;
        XDocument sheetDoc;
        using (Stream st = sheetEntry.Open())
            sheetDoc = XDocument.Load(st);

        var rows = new List<LevelConfigRow>();
        bool firstDataCandidate = true;

        foreach (XElement rowEl in sheetDoc.Descendants(m + "row"))
        {
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

            cellsByCol.TryGetValue(0, out string tag);
            cellsByCol.TryGetValue(1, out string levelStr);
            cellsByCol.TryGetValue(2, out string json);

            tag ??= string.Empty;
            levelStr ??= string.Empty;
            json ??= string.Empty;

            tag = tag.Trim();
            levelStr = levelStr.Trim();
            json = json.Trim();

            if (string.IsNullOrEmpty(tag) && string.IsNullOrEmpty(levelStr) && string.IsNullOrEmpty(json))
                continue;

            if (!TryParseLevelCell(levelStr, out int level))
            {
                if (firstDataCandidate)
                {
                    firstDataCandidate = false;
                    continue;
                }

                Debug.LogWarning($"[XlsxLevelConfigParser] 跳过无法解析 level 的行（Excel 行号≈{rowEl.Attribute("r")?.Value}）：{Truncate(levelStr, 80)}");
                continue;
            }

            firstDataCandidate = false;

            if (string.IsNullOrWhiteSpace(tag))
                Debug.LogWarning($"[XlsxLevelConfigParser] tag 为空（Excel 行≈{rowEl.Attribute("r")?.Value}）。");

            if (string.IsNullOrWhiteSpace(json))
                Debug.LogWarning($"[XlsxLevelConfigParser] JSON 为空（tag={tag}, level={level}）。");

            rows.Add(new LevelConfigRow
            {
                tag = tag,
                level = level,
                contentJson = json,
            });
        }

        return rows;
    }

    static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max)
            return s;
        return s.Substring(0, max);
    }

    static bool TryParseLevelCell(string levelStr, out int level)
    {
        level = 0;
        if (string.IsNullOrWhiteSpace(levelStr))
            return false;

        if (int.TryParse(levelStr, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out level))
            return true;

        // Excel 可能写出带小数的数字单元格
        if (double.TryParse(levelStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
        {
            level = (int)Math.Round(d);
            return true;
        }

        return false;
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
            foreach (XElement r in si.Elements(m + "r"))
            {
                XElement t = r.Element(m + "t");
                if (t != null)
                    sb.Append(t.Value);
            }

            list.Add(sb.ToString());
        }

        return list;
    }

    static string ResolveFirstWorksheetPath(ZipArchive zip)
    {
        ZipArchiveEntry wbEntry = zip.GetEntry("xl/workbook.xml")
            ?? throw new InvalidOperationException("无效的 xlsx：缺少 xl/workbook.xml");

        XNamespace m = MainSpreadsheetMl;
        XNamespace r = RelOfficeDocument;

        XDocument wbDoc;
        using (Stream st = wbEntry.Open())
            wbDoc = XDocument.Load(st);

        XElement firstSheet = wbDoc.Descendants(m + "sheet").FirstOrDefault()
            ?? throw new InvalidOperationException("工作簿中没有任何工作表。");

        XAttribute idAttr = firstSheet.Attribute(r + "id")
            ?? throw new InvalidOperationException("sheet 缺少 r:id。");
        string rid = idAttr.Value;

        ZipArchiveEntry relsEntry = zip.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidOperationException("缺少 xl/_rels/workbook.xml.rels");

        XNamespace relNs = RelPackage;
        XDocument relDoc;
        using (Stream st = relsEntry.Open())
            relDoc = XDocument.Load(st);

        XElement rel = relDoc.Descendants(relNs + "Relationship")
            .FirstOrDefault(e => e.Attribute("Id")?.Value == rid)
            ?? throw new InvalidOperationException($"找不到 Relationship Id={rid}");

        string target = rel.Attribute("Target")?.Value
            ?? throw new InvalidOperationException("Relationship 缺少 Target");

        target = target.Replace('\\', '/').TrimStart('/');
        if (target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            return target;

        // Target 相对 xl/workbook.xml，通常为 worksheets/sheet1.xml
        return "xl/" + target;
    }

    /// <summary>A1 / BC42 → 列从零开始、行号（仅校验格式）。</summary>
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

        // 数字、布尔、字符串（无 t）等：直接读 v 或内部文本
        if (v != null)
            return v.Value;

        return string.Empty;
    }
}
