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
}
