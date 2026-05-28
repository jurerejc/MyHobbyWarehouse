using ClosedXML.Excel;
using MyHobbyWarehouse.Models;
using System.Text;

namespace MyHobbyWarehouse.Services;

/// <summary>
/// Exports components and BOM data to CSV or XLSX.
/// </summary>
public static class ExportService
{
    // ── Component library → XLSX ─────────────────────────────────────────────

    public static void ExportComponentsXlsx(List<Component> components, string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Components");

        // Header row
        string[] headers =
        [
            "SKU","Opis","Enota","Tip","Sub-tip","Vrednost","Package","SMD",
            "Zaloga","Cena (€)","Vrednost zaloge (€)","Masa (mg)",
            "Lokacija","MFG","MFG part",
            "Dobavitelj 1","SKU 1","Cena 1","URL 1",
            "Dobavitelj 2","SKU 2","Cena 2","URL 2",
            "Dobavitelj 3","SKU 3","Cena 3","URL 3",
            "Stara SKU","Alt"
        ];
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0x1A, 0x73, 0x48);
            ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        // Data rows
        int row = 2;
        foreach (var c in components)
        {
            ws.Cell(row, 1).Value  = c.Sku;
            ws.Cell(row, 2).Value  = c.Description;
            ws.Cell(row, 3).Value  = c.Unit;
            ws.Cell(row, 4).Value  = c.Category1;
            ws.Cell(row, 5).Value  = c.Category2;
            ws.Cell(row, 6).Value  = c.Category3;
            ws.Cell(row, 7).Value  = c.Category4;
            ws.Cell(row, 8).Value  = c.Smd ? "SMD" : "TH";
            ws.Cell(row, 9).Value  = c.StockSum;
            ws.Cell(row, 10).Value = c.LastPrice;
            ws.Cell(row, 11).Value = c.StockValue;
            ws.Cell(row, 12).Value = c.MassMg;
            ws.Cell(row, 13).Value = c.DisplayLocation;
            ws.Cell(row, 14).Value = c.ManufacturerName;
            ws.Cell(row, 15).Value = c.ManufacturerPart;
            ws.Cell(row, 16).Value = c.Supplier1Name;
            ws.Cell(row, 17).Value = c.Supplier1Sku;
            ws.Cell(row, 18).Value = c.Supplier1Price;
            ws.Cell(row, 19).Value = c.Supplier1Url;
            ws.Cell(row, 20).Value = c.Supplier2Name;
            ws.Cell(row, 21).Value = c.Supplier2Sku;
            ws.Cell(row, 22).Value = c.Supplier2Price;
            ws.Cell(row, 23).Value = c.Supplier2Url;
            ws.Cell(row, 24).Value = c.Supplier3Name;
            ws.Cell(row, 25).Value = c.Supplier3Sku;
            ws.Cell(row, 26).Value = c.Supplier3Price;
            ws.Cell(row, 27).Value = c.Supplier3Url;
            ws.Cell(row, 28).Value = c.OldSku;
            ws.Cell(row, 29).Value = c.Alt;

            // Highlight zero-stock rows
            if (c.StockSum <= 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromArgb(0x40, 0x20, 0x20);

            row++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(filePath);
    }

    // ── BOM for a project → XLSX ─────────────────────────────────────────────

    public static void ExportBomXlsx(Project project, List<BomLine> lines, string filePath)
    {
        using var wb = new XLWorkbook();

        // ---- BOM sheet ----
        var ws = wb.Worksheets.Add("BOM");
        string[] bomHeaders = ["#","SKU","SKU2","Opis","Qty","Enota","Designatorji","Value","Device","Package"];
        for (int i = 0; i < bomHeaders.Length; i++)
        {
            ws.Cell(1, i + 1).Value = bomHeaders[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0x1A, 0x73, 0x48);
            ws.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }
        int row = 2;
        foreach (var l in lines)
        {
            ws.Cell(row, 1).Value  = row - 1;
            ws.Cell(row, 2).Value  = l.Sku;
            ws.Cell(row, 3).Value  = l.Sku2;
            ws.Cell(row, 4).Value  = l.DisplayDescription;
            ws.Cell(row, 5).Value  = l.Qty;
            ws.Cell(row, 6).Value  = l.Unit;
            ws.Cell(row, 7).Value  = l.PartDesignators;
            ws.Cell(row, 8).Value  = l.Value;
            ws.Cell(row, 9).Value  = l.Device;
            ws.Cell(row, 10).Value = l.Package;
            row++;
        }
        ws.Columns().AdjustToContents();

        // ---- Calculations sheet ----
        var wc = wb.Worksheets.Add("Calculations");
        string[] calcHeaders = ["#","SKU","Opis","Qty","Enota","€/kos","€/vrstica","Masa/kos (mg)","Masa skupaj (mg)","Status","Designatorji"];
        for (int i = 0; i < calcHeaders.Length; i++)
        {
            wc.Cell(1, i + 1).Value = calcHeaders[i];
            wc.Cell(1, i + 1).Style.Font.Bold = true;
            wc.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0x1A, 0x73, 0x48);
            wc.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }
        row = 2;
        double totalCost = 0, totalMass = 0;
        foreach (var l in lines)
        {
            double lineCost = (l.Component?.LastPrice ?? 0) * l.Qty;
            double lineMass = (l.Component?.MassMg    ?? 0) * l.Qty;
            totalCost += lineCost;
            totalMass += lineMass;

            wc.Cell(row, 1).Value  = row - 1;
            wc.Cell(row, 2).Value  = l.Sku;
            wc.Cell(row, 3).Value  = l.DisplayDescription;
            wc.Cell(row, 4).Value  = l.Qty;
            wc.Cell(row, 5).Value  = l.Unit;
            wc.Cell(row, 6).Value  = l.Component?.LastPrice ?? 0;
            wc.Cell(row, 7).Value  = lineCost;
            wc.Cell(row, 8).Value  = l.Component?.MassMg ?? 0;
            wc.Cell(row, 9).Value  = lineMass;
            wc.Cell(row, 10).Value = l.DisplayStatus;
            wc.Cell(row, 11).Value = l.PartDesignators;
            row++;
        }
        // Summary
        wc.Cell(row + 1, 3).Value = "SKUPAJ";
        wc.Cell(row + 1, 3).Style.Font.Bold = true;
        wc.Cell(row + 1, 7).Value = totalCost;
        wc.Cell(row + 1, 7).Style.Font.Bold = true;
        wc.Cell(row + 1, 9).Value = totalMass;
        wc.Cell(row + 1, 9).Style.Font.Bold = true;
        wc.Columns().AdjustToContents();

        wb.SaveAs(filePath);
    }

    // ── Components → CSV (simple, for backup/import elsewhere) ───────────────

    public static void ExportComponentsCsv(List<Component> components, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("sku;description;unit;stocksum;lastprice;stockvalue;mass(mg);SMD;" +
                      "category 1(type);category 2(sub-type);category 3(value);" +
                      "category 4(case);category 5(other);stockrack;stockpackage;" +
                      "manufacturername;manufacturerpart #;" +
                      "supplier 1name;supplier 1sku;supplier 1price;" +
                      "supplier 2name;supplier 2sku;supplier 2price;" +
                      "supplier 3name;supplier 3sku;supplier 3price;" +
                      "stickertext;old sku;alt");

        foreach (var c in components)
        {
            sb.AppendLine(string.Join(";", new[]
            {
                Q(c.Sku),          Q(c.Description),  Q(c.Unit),
                c.StockSum.ToString("F0", System.Globalization.CultureInfo.InvariantCulture),
                c.LastPrice.ToString("F4",  System.Globalization.CultureInfo.InvariantCulture),
                c.StockValue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                c.MassMg.ToString("F0", System.Globalization.CultureInfo.InvariantCulture),
                c.Smd ? "1" : "0",
                Q(c.Category1), Q(c.Category2), Q(c.Category3), Q(c.Category4), Q(c.Category5),
                c.StockRack.ToString(), c.StockPackage.ToString(),
                Q(c.ManufacturerName), Q(c.ManufacturerPart),
                Q(c.Supplier1Name), Q(c.Supplier1Sku),
                c.Supplier1Price.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                Q(c.Supplier2Name), Q(c.Supplier2Sku),
                c.Supplier2Price.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                Q(c.Supplier3Name), Q(c.Supplier3Sku),
                c.Supplier3Price.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                Q(c.StickerText), Q(c.OldSku), Q(c.Alt)
            }));
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    // Quote a CSV field if it contains semicolons or quotes
    private static string Q(string s)
        => s.Contains(';') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
