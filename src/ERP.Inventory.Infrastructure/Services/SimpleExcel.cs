using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace ERP.Inventory.Infrastructure.Services;

internal static class SimpleExcel
{
    public static async Task<IReadOnlyCollection<Dictionary<string, string>>> ReadTableAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
            var text = await reader.ReadToEndAsync();
            return ReadDelimited(text, fileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ? '\t' : ',');
        }

        if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return ReadXlsx(stream);
        }

        throw new InvalidOperationException("Only .xlsx, .csv and .tsv files are supported.");
    }

    public static byte[] CreateWorkbook(IReadOnlyCollection<string> headers, IReadOnlyCollection<IReadOnlyCollection<object?>> rows, string sheetName = "Data")
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            AddEntry(zip, "[Content_Types].xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>
  <Override PartName=""/xl/worksheets/sheet1.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>
  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>
</Types>");
            AddEntry(zip, "_rels/.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>");
            AddEntry(zip, "xl/_rels/workbook.xml.rels", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet1.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>");
            AddEntry(zip, "xl/workbook.xml", $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">
  <sheets><sheet name=""{EscapeXml(sheetName)}"" sheetId=""1"" r:id=""rId1""/></sheets>
</workbook>");
            AddEntry(zip, "xl/styles.xml", @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""1""><font><sz val=""11""/><name val=""Calibri""/></font></fonts>
  <fills count=""1""><fill><patternFill patternType=""none""/></fill></fills>
  <borders count=""1""><border><left/><right/><top/><bottom/><diagonal/></border></borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/></cellXfs>
</styleSheet>");
            AddEntry(zip, "xl/worksheets/sheet1.xml", WorksheetXml(headers, rows));
        }

        return output.ToArray();
    }

    private static IReadOnlyCollection<Dictionary<string, string>> ReadDelimited(string text, char delimiter)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return Array.Empty<Dictionary<string, string>>();
        }

        var headers = ParseDelimitedLine(lines[0], delimiter).Select(NormalizeHeader).ToArray();
        return lines.Skip(1).Select(line =>
        {
            var values = ParseDelimitedLine(line, delimiter);
            return headers.Select((header, index) => new { header, value = index < values.Count ? values[index] : string.Empty })
                .Where(x => !string.IsNullOrWhiteSpace(x.header))
                .ToDictionary(x => x.header, x => x.value.Trim(), StringComparer.OrdinalIgnoreCase);
        }).ToArray();
    }

    private static IReadOnlyCollection<Dictionary<string, string>> ReadXlsx(Stream stream)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var sharedStrings = ReadSharedStrings(zip);
        var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml") ?? zip.Entries.FirstOrDefault(x => x.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase));
        if (sheetEntry == null)
        {
            return Array.Empty<Dictionary<string, string>>();
        }

        using var sheetStream = sheetEntry.Open();
        var doc = XDocument.Load(sheetStream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = doc.Descendants(ns + "row").Select(row =>
        {
            var values = new SortedDictionary<int, string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = cell.Attribute("r")?.Value ?? string.Empty;
                var column = ColumnIndex(reference);
                var type = cell.Attribute("t")?.Value;
                var raw = cell.Element(ns + "v")?.Value ?? cell.Element(ns + "is")?.Element(ns + "t")?.Value ?? string.Empty;
                var value = type == "s" && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedIndex) && sharedIndex < sharedStrings.Count
                    ? sharedStrings[sharedIndex]
                    : raw;
                values[column] = value;
            }

            return values;
        }).Where(x => x.Count > 0).ToArray();

        if (rows.Length == 0)
        {
            return Array.Empty<Dictionary<string, string>>();
        }

        var headers = rows[0].Select(x => new { x.Key, Header = NormalizeHeader(x.Value) }).ToArray();
        return rows.Skip(1).Select(row => headers
                .Where(x => !string.IsNullOrWhiteSpace(x.Header))
                .ToDictionary(x => x.Header, x => row.TryGetValue(x.Key, out var value) ? value.Trim() : string.Empty, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
        {
            return new List<string>();
        }

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return doc.Descendants(ns + "si").Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value))).ToList();
    }

    private static List<string> ParseDelimitedLine(string line, char delimiter)
    {
        var result = new List<string>();
        var value = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    value.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                result.Add(value.ToString());
                value.Clear();
            }
            else
            {
                value.Append(ch);
            }
        }

        result.Add(value.ToString());
        return result;
    }

    private static string WorksheetXml(IReadOnlyCollection<string> headers, IReadOnlyCollection<IReadOnlyCollection<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""><sheetData>");
        AppendRow(sb, 1, headers.Cast<object?>().ToArray());
        var rowIndex = 2;
        foreach (var row in rows)
        {
            AppendRow(sb, rowIndex++, row.ToArray());
        }

        sb.AppendLine("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, int rowIndex, IReadOnlyList<object?> values)
    {
        sb.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowIndex}\">");
        for (var i = 0; i < values.Count; i++)
        {
            var cellRef = $"{ColumnName(i + 1)}{rowIndex}";
            var text = values[i] switch
            {
                null => string.Empty,
                DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTimeOffset d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                _ => Convert.ToString(values[i], CultureInfo.InvariantCulture) ?? string.Empty
            };
            sb.Append(CultureInfo.InvariantCulture, $"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t>{EscapeXml(text)}</t></is></c>");
        }

        sb.AppendLine("</row>");
    }

    private static void AddEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.TrimStart());
    }

    private static int ColumnIndex(string cellReference)
    {
        var letters = new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        var index = 0;
        foreach (var ch in letters)
        {
            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return index;
    }

    private static string ColumnName(int index)
    {
        var name = string.Empty;
        while (index > 0)
        {
            index--;
            name = (char)('A' + index % 26) + name;
            index /= 26;
        }

        return name;
    }

    private static string NormalizeHeader(string value)
    {
        return value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty);
    }

    private static string EscapeXml(string value)
    {
        return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
