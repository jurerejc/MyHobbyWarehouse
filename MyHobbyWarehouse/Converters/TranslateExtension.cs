using System.Windows.Markup;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Converters;

public class TranslateExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    public TranslateExtension() { }

    public TranslateExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider sp) => TranslationService.Get(Key);
}
