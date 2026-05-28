using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Models;

public enum TransactionType
{
    Purchase,    // Nakup materiala
    ManualIn,    // Ročni vnos
    ManualOut,   // Ročni odvzem
    BuildUse,    // Poraba pri gradnji PCB
    Adjustment,  // Korekcija zaloge
}

public class StockTransaction
{
    public int            Id                   { get; set; }
    public string         ComponentSku         { get; set; } = string.Empty;
    public string         ComponentDescription { get; set; } = string.Empty;
    public TransactionType Type                { get; set; }
    public DateTime       Date                 { get; set; } = DateTime.Now;
    public double         Qty                  { get; set; }   // positive = in, negative = out
    public double         UnitPrice            { get; set; }
    public string         Supplier             { get; set; } = string.Empty;
    public string         Notes                { get; set; } = string.Empty;
    public int?           ProjectId            { get; set; }
    public string         ProjectName          { get; set; } = string.Empty;

    public string DisplayType => Type switch
    {
        TransactionType.Purchase   => TranslationService.Get("TypePurchaseShort"),
        TransactionType.ManualIn   => TranslationService.Get("TypeManualInShort"),
        TransactionType.ManualOut  => TranslationService.Get("TypeManualOutShort"),
        TransactionType.BuildUse   => TranslationService.Get("TypeBuildUseShort"),
        TransactionType.Adjustment => TranslationService.Get("TypeAdjustmentShort"),
        _                          => Type.ToString()
    };

    public string DisplayQty   => Qty > 0 ? $"+{Qty:F0}" : $"{Qty:F0}";
    public string DisplayDate  => Date.ToString("dd.MM.yyyy HH:mm");
    public string DisplayPrice => UnitPrice > 0 ? TranslationService.Get("DisplayPriceFormat", UnitPrice) : TranslationService.Get("ValueNone");
}
