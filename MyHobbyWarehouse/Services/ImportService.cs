using System.IO.Compression;
using System.Xml.Linq;
using System.Globalization;
using MyHobbyWarehouse.Models;

namespace MyHobbyWarehouse.Services;

/// <summary>
/// Handles import of base.ods (component library) and Eagle BOM CSV files.
/// ODS is parsed manually via ZIP+XML — no dependency on ExcelDataReader.
/// </summary>
public static class ImportService
{
    // ── ODS namespace shortcuts ───────────────────────────────────────────────
    private static readonly XNamespace NsTable  = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace NsOffice = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    // ── base.ods → List<Component> ───────────────────────────────────────────

    public static (List<Component> Components, List<string> Errors) ImportBaseOds(string filePath)
    {
        var components = new List<Component>();
        var errors     = new List<string>();

        try
        {
            var sheets = ReadOds(filePath);

            // Find sheet named "base" (fallback to first sheet)
            var sheet = sheets.FirstOrDefault(s =>
                s.Name.Contains("base", StringComparison.OrdinalIgnoreCase))
                ?? sheets.FirstOrDefault();

            if (sheet == null) { errors.Add("ODS: ni veljavnega sheeta."); return (components, errors); }
            if (sheet.Rows.Count == 0) { errors.Add("ODS: sheet je prazen."); return (components, errors); }

            // First row = headers
            var headers = sheet.Rows[0]
                .Select(c => c?.ToString()?.Trim().ToLowerInvariant() ?? "")
                .ToList();

            // Build column index map
            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            // Track duplicate "stockrfu" columns
            var rfuCols = new List<int>();
            for (int i = 0; i < headers.Count; i++)
            {
                string h = headers[i];
                if (!colMap.ContainsKey(h)) colMap[h] = i;
                if (h == "stockrfu") rfuCols.Add(i);
            }

            for (int rowIdx = 1; rowIdx < sheet.Rows.Count; rowIdx++)
            {
                var row = sheet.Rows[rowIdx];
                try
                {
                    string sku = NormalizeSku(CellStr(row, colMap, "sku"));
                    if (string.IsNullOrEmpty(sku)) continue;

                    var comp = new Component
                    {
                        Sku              = sku,
                        OldSku           = CellStr(row, colMap, "old sku"),
                        Alt              = CellStr(row, colMap, "alt"),
                        Description      = CellStr(row, colMap, "description"),
                        Unit             = CellStr(row, colMap, "unit", "pcs"),
                        StockSum         = CellDbl(row, colMap, "stocksum"),
                        Stock            = CellDbl(row, colMap, "stock"),
                        StockRfu1        = rfuCols.Count > 0 ? ColDbl(row, rfuCols[0]) : 0,
                        StockRfu2        = rfuCols.Count > 1 ? ColDbl(row, rfuCols[1]) : 0,
                        StockRfu3        = rfuCols.Count > 2 ? ColDbl(row, rfuCols[2]) : 0,
                        StockRfu4        = rfuCols.Count > 3 ? ColDbl(row, rfuCols[3]) : 0,
                        StockRack        = (int)CellDbl(row, colMap, "stockrack"),
                        StockPackage     = (int)CellDbl(row, colMap, "stockpackage"),
                        StockZone        = (int)CellDbl(row, colMap, "stockzome"),
                        LastPrice        = CellDbl(row, colMap, "lastprice"),
                        StockValue       = CellDbl(row, colMap, "stockvalue"),
                        SupCount         = (int)CellDbl(row, colMap, "sup#"),
                        LastSupplier     = CellStr(row, colMap, "lastsupplier"),
                        LastSupplierSku  = CellStr(row, colMap, "last suppliersku"),
                        MassMg           = CellDbl(row, colMap, "mass(mg)"),
                        Smd              = CellDbl(row, colMap, "smd") != 0,
                        Category1        = CellStr(row, colMap, "category 1(type)"),
                        Category2        = CellStr(row, colMap, "category 2(sub-type)"),
                        Category3        = CellStr(row, colMap, "category 3(value)"),
                        Category4        = CellStr(row, colMap, "category 4(case)"),
                        Category5        = CellStr(row, colMap, "category 5(other)"),
                        ManufacturerName = CellStr(row, colMap, "manufacturername"),
                        ManufacturerPart = CellStr(row, colMap, "manufacturerpart #"),
                        Supplier1Name    = CellStr(row, colMap, "supplier 1name"),
                        Supplier1Sku     = ExtractSku(CellStr(row, colMap, "supplier 1sku")),
                        Supplier1Url     = ExtractUrl(CellStr(row, colMap, "supplier 1sku")),
                        Supplier1Price   = CellDbl(row, colMap, "supplier 1price"),
                        Supplier2Name    = CellStr(row, colMap, "supplier 2name"),
                        Supplier2Sku     = ExtractSku(CellStr(row, colMap, "supplier 2sku")),
                        Supplier2Url     = ExtractUrl(CellStr(row, colMap, "supplier 2sku")),
                        Supplier2Price   = CellDbl(row, colMap, "supplier 2price"),
                        Supplier3Name    = CellStr(row, colMap, "supplier 3name"),
                        Supplier3Sku     = ExtractSku(CellStr(row, colMap, "supplier 3sku")),
                        Supplier3Url     = ExtractUrl(CellStr(row, colMap, "supplier 3sku")),
                        Supplier3Price   = CellDbl(row, colMap, "supplier 3price"),
                        StickerText      = CellStr(row, colMap, "stickertext"),
                    };
                    components.Add(comp);
                }
                catch (Exception ex)
                {
                    errors.Add($"Vrstica {rowIdx + 1}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Napaka pri branju datoteke: {ex.Message}");
        }

        return (components, errors);
    }

    // ── ODS reader (ZIP + XML, no external dependencies) ─────────────────────

    private record OdsSheet(string Name, List<List<object?>> Rows);

    private static List<OdsSheet> ReadOds(string filePath)
    {
        var sheets = new List<OdsSheet>();

        using var zip    = ZipFile.OpenRead(filePath);
        var contentEntry = zip.GetEntry("content.xml")
            ?? throw new InvalidDataException("content.xml ni v ODS datoteki.");

        XDocument doc;
        using (var stream = contentEntry.Open())
            doc = XDocument.Load(stream);

        foreach (var tableEl in doc.Descendants(NsTable + "table"))
        {
            string sheetName = tableEl.Attribute(NsTable + "name")?.Value ?? "";
            var rows = new List<List<object?>>();

            foreach (var rowEl in tableEl.Elements(NsTable + "table-row"))
            {
                var cells = new List<object?>();

                foreach (var cellEl in rowEl.Elements(NsTable + "table-cell"))
                {
                    // table:number-columns-repeated — how many times this cell repeats
                    int repeat = 1;
                    var repAttr = cellEl.Attribute(NsTable + "number-columns-repeated");
                    if (repAttr != null) int.TryParse(repAttr.Value, out repeat);

                    object? value = ReadCellValue(cellEl);

                    // Limit large trailing empty repeats (e.g. 1024 empty cells at end)
                    if (value == null && repeat > 64) repeat = 1;

                    for (int i = 0; i < repeat; i++) cells.Add(value);
                }

                // Skip rows that are completely empty
                if (cells.Any(c => c != null))
                    rows.Add(cells);
            }

            if (rows.Count > 0)
                sheets.Add(new OdsSheet(sheetName, rows));
        }

        return sheets;
    }

    private static object? ReadCellValue(XElement cell)
    {
        string? valType = cell.Attribute(NsOffice + "value-type")?.Value;

        return valType switch
        {
            // Numeric types → use office:value attribute (exact value, locale-independent)
            "float" or "percentage" or "currency" =>
                double.TryParse(
                    cell.Attribute(NsOffice + "value")?.Value,
                    NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : (object?)null,

            "boolean" =>
                cell.Attribute(NsOffice + "boolean-value")?.Value == "true" ? 1.0 : 0.0,

            "date" =>
                cell.Attribute(NsOffice + "date-value")?.Value,

            // String / unknown → XElement.Value concatenates all descendant text nodes
            _ => cell.Value.Trim() is string s && s.Length > 0 ? s : null
        };
    }

    // ── Eagle BOM CSV → List<BomLine> ────────────────────────────────────────

    public static (List<BomLine> Lines, List<string> Errors) ImportEagleCsv(string filePath)
    {
        var rawLines = new List<BomLine>();
        var errors   = new List<string>();

        try
        {
            var fileLines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (fileLines.Length == 0) return (rawLines, errors);

            var headers = SplitCsv(fileLines[0]);
            var colMap  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                var h = headers[i].Trim();
                if (!string.IsNullOrEmpty(h) && !colMap.ContainsKey(h))
                    colMap[h] = i;
            }

            for (int li = 1; li < fileLines.Length; li++)
            {
                var raw = fileLines[li].Trim();
                if (string.IsNullOrEmpty(raw)) continue;

                try
                {
                    var cols   = SplitCsv(raw);
                    string xcode  = CsvStr(cols, colMap, "XCODE").Trim();
                    string xcode1 = CsvStr(cols, colMap, "XCODE1").Trim();

                    if (string.IsNullOrEmpty(xcode) || xcode == "0") continue;

                    double qty = 0;
                    double.TryParse(CsvStr(cols, colMap, "qty"),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out qty);
                    if (qty <= 0) continue;

                    rawLines.Add(new BomLine
                    {
                        Sku             = xcode,
                        Sku2            = xcode1,
                        Qty             = qty,
                        Unit            = CsvStr(cols, colMap, "unit", "pcs"),
                        PartDesignators = CsvStr(cols, colMap, "part"),
                        Value           = CsvStr(cols, colMap, "value"),
                        Device          = CsvStr(cols, colMap, "device"),
                        Package         = CsvStr(cols, colMap, "package"),
                    });
                }
                catch (Exception ex)
                {
                    errors.Add($"CSV vrstica {li + 1}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Napaka pri branju CSV: {ex.Message}");
        }

        // Group by primary SKU
        var grouped = rawLines
            .GroupBy(l => l.Sku)
            .Select(g =>
            {
                var parts = g
                    .SelectMany(l => l.PartDesignators
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim()))
                    .Distinct().OrderBy(p => p).ToList();
                return new BomLine
                {
                    Sku             = g.Key,
                    Sku2            = g.FirstOrDefault(l => !string.IsNullOrEmpty(l.Sku2))?.Sku2 ?? "",
                    Qty             = g.Sum(l => l.Qty),
                    Unit            = g.First().Unit,
                    PartDesignators = string.Join(", ", parts),
                    Value           = g.First().Value,
                    Device          = g.First().Device,
                    Package         = g.First().Package,
                };
            })
            .ToList();

        return (grouped, errors);
    }


    // ── Projektni ODS (bom sheet) → List<BomLine> ────────────────────────────

    public static (List<BomLine> Lines, List<string> Errors) ImportProjectOds(string filePath)
    {
        var lines  = new List<BomLine>();
        var errors = new List<string>();

        try
        {
            var sheets = ReadOds(filePath);

            // Try "bom" sheet first, fall back to first sheet
            var sheet = sheets.FirstOrDefault(s =>
                            s.Name.Equals("bom", StringComparison.OrdinalIgnoreCase))
                        ?? sheets.FirstOrDefault();

            if (sheet == null || sheet.Rows.Count == 0)
            { errors.Add("ODS: ni veljavnega BOM sheeta."); return (lines, errors); }

            // First row = headers
            var headers = sheet.Rows[0]
                .Select(c => c?.ToString()?.Trim().ToLowerInvariant() ?? "")
                .ToList();

            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Count; i++)
                if (!colMap.ContainsKey(headers[i])) colMap[headers[i]] = i;

            for (int rowIdx = 1; rowIdx < sheet.Rows.Count; rowIdx++)
            {
                var row = sheet.Rows[rowIdx];
                try
                {
                    string sku = NormalizeSku(CellStr(row, colMap, "sku"));
                    if (string.IsNullOrEmpty(sku)) continue;

                    double qty = 0;
                    // "qty" or "quantity"
                    qty = CellDbl(row, colMap, "qty");
                    if (qty == 0) qty = CellDbl(row, colMap, "quantity");
                    if (qty <= 0) continue;

                    lines.Add(new BomLine
                    {
                        Sku             = sku,
                        Qty             = qty,
                        Unit            = CellStr(row, colMap, "unit", "pcs"),
                        PartDesignators = CellStr(row, colMap, "part designation"),
                        // description comes from library lookup — no need to store
                    });
                }
                catch (Exception ex)
                {
                    errors.Add($"Vrstica {rowIdx + 1}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Napaka pri branju ODS: {ex.Message}");
        }

        return (lines, errors);
    }

    // ── CSV parser (semicolon-delimited, quoted) ──────────────────────────────

    private static string[] SplitCsv(string line)
    {
        var result  = new List<string>();
        var current = new StringBuilder();
        bool inQ    = false;
        foreach (char ch in line)
        {
            if (ch == '"'       ) { inQ = !inQ; }
            else if (ch == ';' && !inQ) { result.Add(current.ToString()); current.Clear(); }
            else                  { current.Append(ch); }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }

    // ── Cell helpers ──────────────────────────────────────────────────────────

    private static string CellStr(List<object?> row, Dictionary<string, int> map,
        string col, string def = "")
    {
        if (!map.TryGetValue(col, out int idx) || idx >= row.Count) return def;
        return row[idx]?.ToString()?.Trim() ?? def;
    }

    private static double CellDbl(List<object?> row, Dictionary<string, int> map, string col)
    {
        if (!map.TryGetValue(col, out int idx) || idx >= row.Count) return 0;
        var val = row[idx];
        if (val is double d) return d;
        return double.TryParse(val?.ToString(),
            NumberStyles.Any, CultureInfo.InvariantCulture, out double r) ? r : 0;
    }

    private static double ColDbl(List<object?> row, int idx)
    {
        if (idx >= row.Count) return 0;
        var val = row[idx];
        if (val is double d) return d;
        return double.TryParse(val?.ToString(),
            NumberStyles.Any, CultureInfo.InvariantCulture, out double r) ? r : 0;
    }

    private static string CsvStr(string[] cols, Dictionary<string, int> map,
        string col, string def = "")
    {
        if (!map.TryGetValue(col, out int idx) || idx >= cols.Length) return def;
        return cols[idx].Trim();
    }

    /// <summary>Returns the value as-is if it is NOT a URL, otherwise returns empty string.</summary>
    private static string ExtractSku(string raw)
        => IsUrl(raw) ? string.Empty : raw;

    /// <summary>Returns the value if it looks like a URL, otherwise empty string.</summary>
    private static string ExtractUrl(string raw)
        => IsUrl(raw) ? raw : string.Empty;

    private static bool IsUrl(string s)
        => s.StartsWith("http://",  StringComparison.OrdinalIgnoreCase)
        || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || s.StartsWith("www.",     StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSku(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        if (double.TryParse(raw, NumberStyles.Any,
            CultureInfo.InvariantCulture, out double num))
            return ((int)num).ToString("D3");
        return raw;
    }
}
