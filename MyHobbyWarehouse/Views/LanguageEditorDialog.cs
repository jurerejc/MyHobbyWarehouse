using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Views;

public class LanguageEditorDialog : Window
{
    private readonly string _code;
    private readonly Dictionary<string, string> _original;
    private readonly Dictionary<string, TextBox> _boxes = new();
    private readonly StackPanel _listPanel = new() { Margin = new Thickness(0, 8, 0, 0) };

    public LanguageEditorDialog(string code, string name)
    {
        _code = code;
        _original = TranslationService.LoadAllKeys(code);
        Title = TranslationService.Get("LangEditTitle", name);
        Width = 700; Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var outer = new Border { Padding = new Thickness(20) };
        outer.SetResourceReference(Border.BackgroundProperty, "BgBrush");

        var root = new DockPanel();

        // ── Search ──
        var searchRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        searchRow.Children.Add(new TextBlock
        {
            Text = TranslationService.Get("Search") + ": ",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });
        var txSearch = new TextBox { Width = 200 };
        txSearch.TextChanged += (_, _) => ApplyFilter(txSearch.Text);
        searchRow.Children.Add(txSearch);
        root.Children.Add(searchRow);
        DockPanel.SetDock(searchRow, Dock.Top);

        // ── Scrollable list ──
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _listPanel };
        root.Children.Add(scroll);

        // ── Buttons ──
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var btnCancel = new Button
        {
            Content = TranslationService.Get("Cancel"),
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(0, 0, 8, 0)
        };
        btnCancel.Click += (_, _) => { DialogResult = false; Close(); };
        btnRow.Children.Add(btnCancel);

        var btnSave = new Button
        {
            Content = TranslationService.Get("Save"),
            Padding = new Thickness(18, 7, 18, 7)
        };
        btnSave.SetResourceReference(Button.StyleProperty, "AccentButton");
        btnSave.Click += BtnSave_Click;
        btnRow.Children.Add(btnSave);
        root.Children.Add(btnRow);
        DockPanel.SetDock(btnRow, Dock.Bottom);

        outer.Child = root;
        Content = outer;

        BuildList();
    }

    private void BuildList()
    {
        _listPanel.Children.Clear();
        _boxes.Clear();

        foreach (var kv in _original.OrderBy(kv => kv.Key))
        {
            if (kv.Key.StartsWith("_")) continue; // meta keys shown separately

            var row = BuildRow(kv.Key, kv.Value);
            _listPanel.Children.Add(row);
        }

        // Meta keys at the bottom
        foreach (var kv in _original.Where(kv => kv.Key.StartsWith("_")))
        {
            var row = BuildRow(kv.Key, kv.Value, isMeta: true);
            _listPanel.Children.Add(row);
        }
    }

    private UIElement BuildRow(string key, string value, bool isMeta = false)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

        var lbl = new TextBlock
        {
            Text = key,
            Width = 180,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = key
        };
        if (isMeta)
            lbl.FontStyle = FontStyles.Italic;
        else
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        panel.Children.Add(lbl);

        var txb = new TextBox
        {
            Text = value,
            Height = 24,
            IsReadOnly = isMeta
        };
        _boxes[key] = txb;
        panel.Children.Add(txb);

        return panel;
    }

    private void ApplyFilter(string filter)
    {
        foreach (var child in _listPanel.Children)
        {
            if (child is DockPanel row)
            {
                var lbl = row.Children[0] as TextBlock;
                bool visible = string.IsNullOrEmpty(filter) ||
                    (lbl?.Text?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? true);
                row.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private void BtnSave_Click(object s, RoutedEventArgs e)
    {
        var updated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in _boxes)
            updated[kv.Key] = kv.Value.Text;

        // Preserve any keys that weren't in the editor (shouldn't happen)
        foreach (var kv in _original)
            if (!updated.ContainsKey(kv.Key))
                updated[kv.Key] = kv.Value;

        TranslationService.SaveAllKeys(_code, updated);
        DialogResult = true;
        Close();
    }
}
