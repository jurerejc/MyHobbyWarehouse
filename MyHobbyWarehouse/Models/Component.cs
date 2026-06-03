using CommunityToolkit.Mvvm.ComponentModel;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Models;

public partial class Component : ObservableObject
{
    // Identity
    public string Sku        { get; set; } = string.Empty;
    public string OldSku     { get; set; } = string.Empty;
    public string Alt        { get; set; } = string.Empty;

    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _unit        = "pcs";

    // Stock levels
    [ObservableProperty] private double _stockSum;      // total (authoritative)
    [ObservableProperty] private double _stock;         // zone 0 (available)
    [ObservableProperty] private double _stockRfu1;
    [ObservableProperty] private double _stockRfu2;
    [ObservableProperty] private double _stockRfu3;
    [ObservableProperty] private double _stockRfu4;
    [ObservableProperty] private int    _stockRack;
    [ObservableProperty] private int    _stockPackage;
    [ObservableProperty] private int    _stockZone;
    public int? LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;

    // Pricing
    [ObservableProperty] private double _lastPrice;
    [ObservableProperty] private double _stockValue;

    // Supplier summary
    [ObservableProperty] private int    _supCount;
    [ObservableProperty] private string _lastSupplier    = string.Empty;
    [ObservableProperty] private string _lastSupplierSku = string.Empty;

    // Physical
    [ObservableProperty] private double _massMg;
    [ObservableProperty] private bool   _smd;

    // Categories (5-level hierarchy from base.ods)
    [ObservableProperty] private string _category1 = string.Empty;  // type
    [ObservableProperty] private string _category2 = string.Empty;  // sub-type
    [ObservableProperty] private string _category3 = string.Empty;  // value
    [ObservableProperty] private string _category4 = string.Empty;  // case/package
    [ObservableProperty] private string _category5 = string.Empty;  // other

    // Manufacturer
    [ObservableProperty] private string _manufacturerName = string.Empty;
    [ObservableProperty] private string _manufacturerPart = string.Empty;

    // Supplier 1
    [ObservableProperty] private string _supplier1Name  = string.Empty;
    [ObservableProperty] private string _supplier1Sku   = string.Empty;
    [ObservableProperty] private double _supplier1Price;
    [ObservableProperty] private string _supplier1Url   = string.Empty;

    // Supplier 2
    [ObservableProperty] private string _supplier2Name  = string.Empty;
    [ObservableProperty] private string _supplier2Sku   = string.Empty;
    [ObservableProperty] private double _supplier2Price;
    [ObservableProperty] private string _supplier2Url   = string.Empty;

    // Supplier 3
    [ObservableProperty] private string _supplier3Name  = string.Empty;
    [ObservableProperty] private string _supplier3Sku   = string.Empty;
    [ObservableProperty] private double _supplier3Price;
    [ObservableProperty] private string _supplier3Url   = string.Empty;

    [ObservableProperty] private string _stickerText = string.Empty;

    // Display helpers
    public string DisplayStock     => $"{StockSum:F0} {Unit}";
    public string DisplayPrice     => LastPrice  > 0 ? TranslationService.Get("DisplayPriceFormat", LastPrice)  : TranslationService.Get("ValueNone");
    public string DisplayValue     => StockValue > 0 ? TranslationService.Get("DisplayValueFormat", StockValue) : TranslationService.Get("ValueNone");
    public string DisplayLocation  => !string.IsNullOrEmpty(LocationCode)
        ? LocationCode
        : StockRack > 0
            ? $"R{StockRack}" + (StockPackage > 0 ? $"-P{StockPackage}" : "")
            : "";
    public string DisplaySmd       => Smd ? TranslationService.Get("SmdLabel") : TranslationService.Get("ThLabel");
    public string DisplayCategory  => string.Join(TranslationService.Get("CategorySeparator"),
        new[] { Category1, Category2, Category3, Category4 }
        .Where(s => !string.IsNullOrEmpty(s)));

    public bool IsLowStock         => StockSum <= 0;
    public bool HasImage           { get; set; }
}
