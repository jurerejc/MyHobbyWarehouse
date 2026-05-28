using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Models;

/// <summary>
/// Represents a group of transactions that share the same Notes tag
/// (typically one PCB build = one group).
/// </summary>
public class TransactionGroup
{
    public string          Tag              { get; set; } = string.Empty; // = Notes field
    public string          ProjectName      { get; set; } = string.Empty;
    public DateTime        Date             { get; set; }
    public int             TransactionCount { get; set; }
    public double          TotalQtyAbs      { get; set; }  // sum of |qty|
    public TransactionType PrimaryType      { get; set; }

    public string DisplayDate  => Date.ToString("dd.MM.yyyy HH:mm");
    public string DisplayType  => PrimaryType switch
    {
        TransactionType.BuildUse   => TranslationService.Get("TypeBuildUseShort"),
        TransactionType.Purchase   => TranslationService.Get("TypePurchaseShort"),
        TransactionType.ManualIn   => TranslationService.Get("TypeManualInShort"),
        TransactionType.ManualOut  => TranslationService.Get("TypeManualOutShort"),
        TransactionType.Adjustment => TranslationService.Get("TypeAdjustmentShort"),
        _                          => PrimaryType.ToString()
    };
    public string DisplayCount => TranslationService.Get("DisplayCount", TransactionCount);
    public string DisplayQty   => TranslationService.Get("DisplayQty", TotalQtyAbs);
}
