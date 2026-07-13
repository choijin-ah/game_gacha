using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace StarfallAcademy.Lobby.Editor
{
    internal sealed class StorySheetTable
    {
        public string Name { get; }
        public IReadOnlyList<string> Headers { get; }
        public IReadOnlyList<Dictionary<string, string>> Rows { get; }

        public StorySheetTable(string name, IReadOnlyList<string> headers,
            IReadOnlyList<Dictionary<string, string>> rows)
        {
            Name = name;
            Headers = headers;
            Rows = rows;
        }
    }

    internal static class StorySpreadsheetReader
    {
        public static IReadOnlyList<StorySheetTable> Read(string path, bool allSheets)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("가져올 스토리 파일을 찾을 수 없습니다.", path);

            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".csv": return new[] { ReadDelimited(path, ',') };
                case ".tsv":
                case ".txt": return new[] { ReadDelimited(path, '\t') };
                case ".xlsx": return ReadXlsx(path, allSheets);
                default: throw new NotSupportedException(".xlsx, .csv, .tsv 파일만 지원합니다.");
            }
        }

        static StorySheetTable ReadDelimited(string path, char delimiter)
        {
            string content;
            using (var reader = new StreamReader(path, Encoding.UTF8, true)) content = reader.ReadToEnd();
            List<List<string>> records = ParseDelimited(content, delimiter);
            return BuildTable(Path.GetFileNameWithoutExtension(path), records);
        }

        static List<List<string>> ParseDelimited(string content, char delimiter)
        {
            var result = new List<List<string>>();
            var row = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < content.Length; i++)
            {
                char current = content[i];
                if (quoted)
                {
                    if (current == '"')
                    {
                        if (i + 1 < content.Length && content[i + 1] == '"')
                        {
                            value.Append('"');
                            i++;
                        }
                        else quoted = false;
                    }
                    else value.Append(current);
                    continue;
                }

                if (current == '"' && value.Length == 0) quoted = true;
                else if (current == delimiter)
                {
                    row.Add(value.ToString());
                    value.Clear();
                }
                else if (current == '\r' || current == '\n')
                {
                    if (current == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                    row.Add(value.ToString());
                    value.Clear();
                    result.Add(row);
                    row = new List<string>();
                }
                else value.Append(current);
            }

            if (value.Length > 0 || row.Count > 0)
            {
                row.Add(value.ToString());
                result.Add(row);
            }
            return result;
        }

        static IReadOnlyList<StorySheetTable> ReadXlsx(string path, bool allSheets)
        {
            using (var stream = File.OpenRead(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                ZipArchiveEntry workbookEntry = GetEntry(archive, "xl/workbook.xml");
                ZipArchiveEntry relationshipEntry = GetEntry(archive, "xl/_rels/workbook.xml.rels");
                if (workbookEntry == null || relationshipEntry == null)
                    throw new InvalidDataException("유효한 Excel 통합 문서가 아닙니다.");

                XDocument workbook = LoadXml(workbookEntry);
                XDocument relationships = LoadXml(relationshipEntry);
                XNamespace sheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                XNamespace documentRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                XNamespace packageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

                var targets = relationships.Descendants(packageRelNs + "Relationship")
                    .Where(node => node.Attribute("Id") != null && node.Attribute("Target") != null)
                    .ToDictionary(node => node.Attribute("Id").Value, node => node.Attribute("Target").Value);

                var tables = new List<StorySheetTable>();
                foreach (XElement sheet in workbook.Descendants(sheetNs + "sheet"))
                {
                    string name = (string)sheet.Attribute("name") ?? "Sheet";
                    string relationshipId = (string)sheet.Attribute(documentRelNs + "id");
                    if (string.IsNullOrWhiteSpace(relationshipId) || !targets.TryGetValue(relationshipId, out string target))
                        continue;

                    string entryPath = NormalizeWorksheetPath(target);
                    ZipArchiveEntry worksheetEntry = GetEntry(archive, entryPath);
                    if (worksheetEntry == null) continue;
                    tables.Add(ReadWorksheet(name, worksheetEntry, sharedStrings));
                    if (!allSheets) break;
                }

                if (tables.Count == 0) throw new InvalidDataException("Excel 파일에서 읽을 수 있는 시트를 찾지 못했습니다.");
                return tables;
            }
        }

        static StorySheetTable ReadWorksheet(string name, ZipArchiveEntry entry, IReadOnlyList<string> sharedStrings)
        {
            XDocument document = LoadXml(entry);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var records = new List<List<string>>();

            foreach (XElement row in document.Descendants(ns + "row"))
            {
                var values = new List<string>();
                int sequentialColumn = 0;
                foreach (XElement cell in row.Elements(ns + "c"))
                {
                    string reference = (string)cell.Attribute("r");
                    int column = string.IsNullOrWhiteSpace(reference)
                        ? sequentialColumn : ColumnIndex(reference);
                    while (values.Count <= column) values.Add(string.Empty);
                    values[column] = ReadCell(cell, ns, sharedStrings);
                    sequentialColumn = column + 1;
                }
                records.Add(values);
            }
            return BuildTable(name, records);
        }

        static string ReadCell(XElement cell, XNamespace ns, IReadOnlyList<string> sharedStrings)
        {
            string type = (string)cell.Attribute("t") ?? string.Empty;
            if (type == "inlineStr")
                return string.Concat(cell.Descendants(ns + "t").Select(node => node.Value));

            string value = cell.Element(ns + "v")?.Value ?? string.Empty;
            if (type == "s" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) &&
                index >= 0 && index < sharedStrings.Count)
                return sharedStrings[index];
            if (type == "b") return value == "1" ? "true" : "false";
            return value;
        }

        static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry entry = GetEntry(archive, "xl/sharedStrings.xml");
            if (entry == null) return new List<string>();
            XDocument document = LoadXml(entry);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            return document.Descendants(ns + "si")
                .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
                .ToList();
        }

        static StorySheetTable BuildTable(string name, IReadOnlyList<List<string>> records)
        {
            int headerIndex = -1;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    headerIndex = i;
                    break;
                }
            }
            if (headerIndex < 0) return new StorySheetTable(name, Array.Empty<string>(),
                Array.Empty<Dictionary<string, string>>());

            var headers = records[headerIndex].Select(NormalizeHeader).ToList();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(headers[i])) headers[i] = "column_" + (i + 1);
                string original = headers[i];
                int suffix = 2;
                while (!seen.Add(headers[i])) headers[i] = original + "_" + suffix++;
            }

            var rows = new List<Dictionary<string, string>>();
            for (int rowIndex = headerIndex + 1; rowIndex < records.Count; rowIndex++)
            {
                List<string> record = records[rowIndex];
                if (!record.Any(value => !string.IsNullOrWhiteSpace(value))) continue;
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int column = 0; column < headers.Count; column++)
                    row[headers[column]] = column < record.Count ? record[column] : string.Empty;
                rows.Add(row);
            }
            return new StorySheetTable(name, headers, rows);
        }

        static string NormalizeHeader(string header)
        {
            return (header ?? string.Empty).Trim().TrimStart('\uFEFF').ToLowerInvariant().Replace(' ', '_');
        }

        static int ColumnIndex(string reference)
        {
            int result = 0;
            foreach (char character in reference)
            {
                if (!char.IsLetter(character)) break;
                result = result * 26 + (char.ToUpperInvariant(character) - 'A' + 1);
            }
            return Math.Max(0, result - 1);
        }

        static string NormalizeWorksheetPath(string target)
        {
            string path = (target ?? string.Empty).Replace('\\', '/').TrimStart('/');
            if (path.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) return path;
            while (path.StartsWith("../", StringComparison.Ordinal)) path = path.Substring(3);
            return "xl/" + path;
        }

        static ZipArchiveEntry GetEntry(ZipArchive archive, string path)
        {
            return archive.GetEntry(path) ?? archive.Entries.FirstOrDefault(entry =>
                string.Equals(entry.FullName, path, StringComparison.OrdinalIgnoreCase));
        }

        static XDocument LoadXml(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open()) return XDocument.Load(stream, LoadOptions.None);
        }
    }

    internal static class StorySpreadsheetTemplateWriter
    {
        const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string OfficeRelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

        static readonly HashSet<string> RequiredHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "episode_id", "episode_title", "category", "line_id", "text"
        };

        public static void Write(string path, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("저장할 Excel 경로가 비어 있습니다.", nameof(path));
            if (headers == null || headers.Count == 0) throw new ArgumentException("Excel 양식 헤더가 비어 있습니다.", nameof(headers));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, false))
            {
                WriteXml(archive, "[Content_Types].xml", CreateContentTypes());
                WriteXml(archive, "_rels/.rels", CreateRootRelationships());
                WriteXml(archive, "xl/workbook.xml", CreateWorkbook());
                WriteXml(archive, "xl/_rels/workbook.xml.rels", CreateWorkbookRelationships());
                WriteXml(archive, "xl/styles.xml", CreateStyles());
                WriteXml(archive, "xl/worksheets/sheet1.xml", CreateWorksheet(headers, rows));
            }
        }

        static XDocument CreateContentTypes()
        {
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
            return Document(new XElement(ns + "Types",
                new XElement(ns + "Default", new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ns + "Default", new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/workbook.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/styles.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"))));
        }

        static XDocument CreateRootRelationships()
        {
            XNamespace ns = PackageRelationshipNamespace;
            return Document(new XElement(ns + "Relationships",
                new XElement(ns + "Relationship", new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "xl/workbook.xml"))));
        }

        static XDocument CreateWorkbook()
        {
            XNamespace ns = SpreadsheetNamespace;
            XNamespace rel = OfficeRelationshipNamespace;
            return Document(new XElement(ns + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", rel),
                new XElement(ns + "fileVersion", new XAttribute("appName", "xl")),
                new XElement(ns + "workbookPr", new XAttribute("defaultThemeVersion", "124226")),
                new XElement(ns + "bookViews", new XElement(ns + "workbookView",
                    new XAttribute("xWindow", "0"), new XAttribute("yWindow", "0"),
                    new XAttribute("windowWidth", "24000"), new XAttribute("windowHeight", "12000"))),
                new XElement(ns + "sheets", new XElement(ns + "sheet",
                    new XAttribute("name", "Story"), new XAttribute("sheetId", "1"),
                    new XAttribute(rel + "id", "rId1"))),
                new XElement(ns + "calcPr", new XAttribute("calcId", "191029"))));
        }

        static XDocument CreateWorkbookRelationships()
        {
            XNamespace ns = PackageRelationshipNamespace;
            return Document(new XElement(ns + "Relationships",
                new XElement(ns + "Relationship", new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml")),
                new XElement(ns + "Relationship", new XAttribute("Id", "rId2"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                    new XAttribute("Target", "styles.xml"))));
        }

        static XDocument CreateStyles()
        {
            XNamespace ns = SpreadsheetNamespace;
            return Document(new XElement(ns + "styleSheet",
                new XElement(ns + "fonts", new XAttribute("count", "2"),
                    Font(ns, false), Font(ns, true)),
                new XElement(ns + "fills", new XAttribute("count", "4"),
                    new XElement(ns + "fill", new XElement(ns + "patternFill", new XAttribute("patternType", "none"))),
                    new XElement(ns + "fill", new XElement(ns + "patternFill", new XAttribute("patternType", "gray125"))),
                    SolidFill(ns, "C65911"), SolidFill(ns, "1F4E78")),
                new XElement(ns + "borders", new XAttribute("count", "2"),
                    Border(ns, false), Border(ns, true)),
                new XElement(ns + "cellStyleXfs", new XAttribute("count", "1"),
                    new XElement(ns + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "0"),
                        new XAttribute("fillId", "0"), new XAttribute("borderId", "0"))),
                new XElement(ns + "cellXfs", new XAttribute("count", "4"),
                    new XElement(ns + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "0"),
                        new XAttribute("fillId", "0"), new XAttribute("borderId", "0"), new XAttribute("xfId", "0")),
                    HeaderFormat(ns, "2"), HeaderFormat(ns, "3"),
                    new XElement(ns + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "0"),
                        new XAttribute("fillId", "0"), new XAttribute("borderId", "1"), new XAttribute("xfId", "0"),
                        new XAttribute("applyBorder", "1"), new XAttribute("applyAlignment", "1"),
                        new XElement(ns + "alignment", new XAttribute("vertical", "top"), new XAttribute("wrapText", "1")))),
                new XElement(ns + "cellStyles", new XAttribute("count", "1"),
                    new XElement(ns + "cellStyle", new XAttribute("name", "Normal"),
                        new XAttribute("xfId", "0"), new XAttribute("builtinId", "0"))),
                new XElement(ns + "dxfs", new XAttribute("count", "0")),
                new XElement(ns + "tableStyles", new XAttribute("count", "0"),
                    new XAttribute("defaultTableStyle", "TableStyleMedium2"),
                    new XAttribute("defaultPivotStyle", "PivotStyleLight16"))));
        }

        static XElement Font(XNamespace ns, bool header)
        {
            var font = new XElement(ns + "font");
            if (header) font.Add(new XElement(ns + "b"), new XElement(ns + "color", new XAttribute("rgb", "FFFFFFFF")));
            font.Add(new XElement(ns + "sz", new XAttribute("val", header ? "10" : "9")),
                new XElement(ns + "name", new XAttribute("val", "맑은 고딕")),
                new XElement(ns + "family", new XAttribute("val", "2")),
                new XElement(ns + "charset", new XAttribute("val", "129")));
            return font;
        }

        static XElement SolidFill(XNamespace ns, string color)
        {
            return new XElement(ns + "fill", new XElement(ns + "patternFill",
                new XAttribute("patternType", "solid"),
                new XElement(ns + "fgColor", new XAttribute("rgb", "FF" + color)),
                new XElement(ns + "bgColor", new XAttribute("indexed", "64"))));
        }

        static XElement Border(XNamespace ns, bool visible)
        {
            string style = visible ? "thin" : null;
            return new XElement(ns + "border",
                BorderSide(ns, "left", style), BorderSide(ns, "right", style),
                BorderSide(ns, "top", style), BorderSide(ns, "bottom", style),
                new XElement(ns + "diagonal"));
        }

        static XElement BorderSide(XNamespace ns, string name, string style)
        {
            var side = new XElement(ns + name);
            if (!string.IsNullOrEmpty(style))
            {
                side.Add(new XAttribute("style", style));
                side.Add(new XElement(ns + "color", new XAttribute("rgb", "FFD9E2F3")));
            }
            return side;
        }

        static XElement HeaderFormat(XNamespace ns, string fillId)
        {
            return new XElement(ns + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "1"),
                new XAttribute("fillId", fillId), new XAttribute("borderId", "1"), new XAttribute("xfId", "0"),
                new XAttribute("applyFont", "1"), new XAttribute("applyFill", "1"),
                new XAttribute("applyBorder", "1"), new XAttribute("applyAlignment", "1"),
                new XElement(ns + "alignment", new XAttribute("horizontal", "center"),
                    new XAttribute("vertical", "center"), new XAttribute("wrapText", "1")));
        }

        static XDocument CreateWorksheet(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
        {
            XNamespace ns = SpreadsheetNamespace;
            string lastColumn = ColumnName(headers.Count - 1);
            int lastRow = rows.Count + 1;
            var sheetData = new XElement(ns + "sheetData");

            var headerRow = new XElement(ns + "row", new XAttribute("r", "1"),
                new XAttribute("ht", "34"), new XAttribute("customHeight", "1"));
            for (int column = 0; column < headers.Count; column++)
            {
                uint style = RequiredHeaders.Contains(headers[column]) ? 1u : 2u;
                headerRow.Add(Cell(ns, ColumnName(column) + "1", headers[column], style));
            }
            sheetData.Add(headerRow);

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                int excelRow = rowIndex + 2;
                string[] values = rows[rowIndex] ?? Array.Empty<string>();
                var row = new XElement(ns + "row", new XAttribute("r", excelRow),
                    new XAttribute("ht", "36"), new XAttribute("customHeight", "1"));
                for (int column = 0; column < headers.Count; column++)
                {
                    string value = column < values.Length ? values[column] : string.Empty;
                    row.Add(Cell(ns, ColumnName(column) + excelRow, value, 3));
                }
                sheetData.Add(row);
            }

            var root = new XElement(ns + "worksheet",
                new XElement(ns + "sheetPr", new XElement(ns + "outlinePr", new XAttribute("summaryBelow", "1"))),
                new XElement(ns + "dimension", new XAttribute("ref", $"A1:{lastColumn}{lastRow}")),
                new XElement(ns + "sheetViews", new XElement(ns + "sheetView", new XAttribute("workbookViewId", "0"),
                    new XAttribute("zoomScale", "85"),
                    new XElement(ns + "pane", new XAttribute("ySplit", "1"), new XAttribute("topLeftCell", "A2"),
                        new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")),
                    new XElement(ns + "selection", new XAttribute("pane", "bottomLeft"), new XAttribute("activeCell", "A2"),
                        new XAttribute("sqref", "A2")))),
                new XElement(ns + "sheetFormatPr", new XAttribute("defaultRowHeight", "18")),
                CreateColumns(ns, headers),
                sheetData,
                new XElement(ns + "autoFilter", new XAttribute("ref", $"A1:{lastColumn}{lastRow}")));

            XElement validations = CreateDataValidations(ns, headers);
            if (validations != null) root.Add(validations);
            root.Add(new XElement(ns + "pageMargins", new XAttribute("left", "0.3"), new XAttribute("right", "0.3"),
                new XAttribute("top", "0.5"), new XAttribute("bottom", "0.5"),
                new XAttribute("header", "0.2"), new XAttribute("footer", "0.2")));
            return Document(root);
        }

        static XElement CreateColumns(XNamespace ns, IReadOnlyList<string> headers)
        {
            var columns = new XElement(ns + "cols");
            for (int i = 0; i < headers.Count; i++)
            {
                columns.Add(new XElement(ns + "col", new XAttribute("min", i + 1),
                    new XAttribute("max", i + 1), new XAttribute("width", ColumnWidth(headers[i])),
                    new XAttribute("customWidth", "1")));
            }
            return columns;
        }

        static string ColumnWidth(string header)
        {
            if (string.Equals(header, "text", StringComparison.OrdinalIgnoreCase)) return "48";
            if (string.Equals(header, "summary", StringComparison.OrdinalIgnoreCase)) return "30";
            if (header.EndsWith("_text", StringComparison.OrdinalIgnoreCase)) return "25";
            if (header.IndexOf("sprite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header == "thumbnail" || header == "banner" || header == "background" || header == "cg") return "24";
            if (header.EndsWith("_id", StringComparison.OrdinalIgnoreCase) ||
                header.IndexOf("episode", StringComparison.OrdinalIgnoreCase) >= 0) return "19";
            if (header.IndexOf("visible", StringComparison.OrdinalIgnoreCase) >= 0 ||
                header.IndexOf("flip", StringComparison.OrdinalIgnoreCase) >= 0) return "11";
            return "16";
        }

        static XElement CreateDataValidations(XNamespace ns, IReadOnlyList<string> headers)
        {
            var items = new List<XElement>();
            AddListValidation(items, ns, headers, new[] { "category" }, "Main,Event,Character,Side");
            AddListValidation(items, ns, headers, new[] { "initially_unlocked", "left_visible", "left_flip",
                "center_visible", "center_flip", "right_visible", "right_flip" }, "true,false");
            AddListValidation(items, ns, headers, new[] { "speaker_position" }, "Narrator,Left,Center,Right");
            AddListValidation(items, ns, headers, new[] { "transition" },
                "None,Cut,CrossFade,FadeToBlack,FadeToWhite,SlideLeft,SlideRight");
            return items.Count == 0 ? null : new XElement(ns + "dataValidations",
                new XAttribute("count", items.Count), items);
        }

        static void AddListValidation(ICollection<XElement> result, XNamespace ns, IReadOnlyList<string> headers,
            IReadOnlyList<string> targetHeaders, string values)
        {
            var ranges = new List<string>();
            foreach (string target in targetHeaders)
            {
                for (int i = 0; i < headers.Count; i++)
                {
                    if (!string.Equals(headers[i], target, StringComparison.OrdinalIgnoreCase)) continue;
                    string column = ColumnName(i);
                    ranges.Add(column + "2:" + column + "10000");
                    break;
                }
            }
            if (ranges.Count == 0) return;

            result.Add(new XElement(ns + "dataValidation", new XAttribute("type", "list"),
                new XAttribute("allowBlank", "1"), new XAttribute("showInputMessage", "1"),
                new XAttribute("showErrorMessage", "1"), new XAttribute("errorStyle", "stop"),
                new XAttribute("promptTitle", "입력값 선택"), new XAttribute("prompt", "목록에서 값을 선택하세요."),
                new XAttribute("errorTitle", "지원하지 않는 값"), new XAttribute("error", "목록에 있는 값만 입력할 수 있습니다."),
                new XAttribute("sqref", string.Join(" ", ranges)),
                new XElement(ns + "formula1", "\"" + values + "\"")));
        }

        static XElement Cell(XNamespace ns, string reference, string value, uint style)
        {
            var text = new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value ?? string.Empty);
            return new XElement(ns + "c", new XAttribute("r", reference), new XAttribute("s", style),
                new XAttribute("t", "inlineStr"), new XElement(ns + "is", text));
        }

        static string ColumnName(int zeroBasedIndex)
        {
            int value = zeroBasedIndex + 1;
            var result = new StringBuilder();
            while (value > 0)
            {
                value--;
                result.Insert(0, (char)('A' + value % 26));
                value /= 26;
            }
            return result.ToString();
        }

        static XDocument Document(XElement root)
        {
            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        static void WriteXml(ZipArchive archive, string path, XDocument document)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                document.Save(writer, SaveOptions.DisableFormatting);
        }
    }
}
