namespace EagleManager.Models;

public class BomLine
{
    public int    Id             { get; set; }
    public int    ProjectId      { get; set; }

    // Primary component SKU (from Eagle XCODE)
    public string Sku            { get; set; } = string.Empty;
    // Secondary SKU (from Eagle XCODE1 — e.g. IC socket paired with an IC)
    public string Sku2           { get; set; } = string.Empty;

    public double Qty            { get; set; }
    public string Unit           { get; set; } = "pcs";

    // Eagle fields (for reference, not used in calculations)
    public string PartDesignators { get; set; } = string.Empty; // "IC1, IC2, R3"
    public string Value          { get; set; } = string.Empty;  // Eagle VALUE attribute
    public string Device         { get; set; } = string.Empty;  // Eagle DEVICE
    public string Package        { get; set; } = string.Empty;  // Eagle PACKAGE

    public string Notes          { get; set; } = string.Empty;

    // Populated at runtime from DB — NOT persisted
    public Component? Component  { get; set; }
    public Component? Component2 { get; set; }

    // ── Computed stock status ────────────────────────────────────────────────

    public double AvailableStock  => Component?.StockSum  ?? 0;
    public double AvailableStock2 => Component2?.StockSum ?? 0;

    public StockStatus Status => string.IsNullOrEmpty(Sku) ? StockStatus.Unknown
        : AvailableStock >= Qty              ? StockStatus.Ok
        : AvailableStock > 0                 ? StockStatus.Low
                                             : StockStatus.Out;

    public StockStatus Status2 => string.IsNullOrEmpty(Sku2) ? StockStatus.NotApplicable
        : AvailableStock2 >= Qty             ? StockStatus.Ok
        : AvailableStock2 > 0                ? StockStatus.Low
                                             : StockStatus.Out;

    public string DisplayStatus => Status switch
    {
        StockStatus.Ok      => "✅",
        StockStatus.Low     => "⚠️",
        StockStatus.Out     => "❌",
        StockStatus.Unknown => "?",
        _                   => ""
    };

    public string DisplayStatus2 => Status2 switch
    {
        StockStatus.Ok             => "✅",
        StockStatus.Low            => "⚠️",
        StockStatus.Out            => "❌",
        StockStatus.NotApplicable  => "",
        _                          => ""
    };

    public double LineCost           => (Component?.LastPrice ?? 0) * Qty;
    public string DisplayLineCost    => LineCost > 0 ? $"{LineCost:F4} €" : "—";
    public string DisplayAvailability => $"{AvailableStock:F0} / {Qty:F0}";
    public string DisplayDescription => Component?.Description ?? $"[{Sku}]";
}

public enum StockStatus { Ok, Low, Out, Unknown, NotApplicable }
