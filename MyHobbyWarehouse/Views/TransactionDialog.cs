using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyHobbyWarehouse.Data;
using MyHobbyWarehouse.Models;

namespace MyHobbyWarehouse.Views;

/// <summary>
/// Dialog for manually adding a stock transaction.
/// Supports: Purchase, ManualIn, ManualOut, Adjustment.
/// Performs SKU autocomplete from the component library.
/// </summary>
public class TransactionDialog : Window
{
    private readonly DatabaseService _db;

    private ComboBox  _cmbType    = null!;
    private TextBox   _txSku      = null!;
    private TextBlock _txDesc     = null!;
    private TextBox   _txQty      = null!;
    private TextBox   _txPrice    = null!;
    private TextBox   _txSupplier = null!;
    private TextBox   _txNotes    = null!;
    private ListBox   _lbSuggest  = null!;

    private List<Component> _allComponents = [];

    public TransactionDialog(DatabaseService db)
    {
        _db    = db;
        Title  = "Dodaj transakcijo";
        Width  = 480; Height = 540;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _allComponents = _db.GetAllComponents();
        BuildUi();
    }

    private void BuildUi()
    {
        var outer = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.Resources["BgBrush"],
            Padding    = new Thickness(20)
        };

        var stack = new StackPanel();

        // ── Type ──────────────────────────────────────────────────────────
        stack.Children.Add(Lbl("Tip transakcije *"));
        _cmbType = new ComboBox { Margin = new Thickness(0, 0, 0, 10) };
        _cmbType.Items.Add("Nakup (Purchase)");
        _cmbType.Items.Add("Ročni vnos (ManualIn)");
        _cmbType.Items.Add("Ročni odvzem (ManualOut)");
        _cmbType.Items.Add("Korekcija (Adjustment)");
        _cmbType.SelectedIndex = 0;
        _cmbType.SelectionChanged += (_, _) => UpdateQtySign();
        stack.Children.Add(_cmbType);

        // ── SKU with autocomplete ─────────────────────────────────────────
        stack.Children.Add(Lbl("SKU *"));
        _txSku = new TextBox { Margin = new Thickness(0, 0, 0, 2) };
        _txSku.TextChanged += TxSku_TextChanged;
        _txSku.KeyDown     += TxSku_KeyDown;
        stack.Children.Add(_txSku);

        // Suggestion list (hidden by default)
        _lbSuggest = new ListBox
        {
            MaxHeight  = 120,
            Visibility = Visibility.Collapsed,
            Margin     = new Thickness(0, 0, 0, 4),
            Background = (System.Windows.Media.Brush)Application.Current.Resources["CardBrush"],
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextBrush"],
            BorderThickness = new Thickness(1),
            BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"],
        };
        _lbSuggest.MouseDoubleClick += (_, _) => ApplySuggestion();
        _lbSuggest.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) ApplySuggestion();
            if (e.Key == Key.Escape) _lbSuggest.Visibility = Visibility.Collapsed;
        };
        stack.Children.Add(_lbSuggest);

        // Component description (read-only feedback)
        _txDesc = new TextBlock
        {
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"],
            FontSize   = 11,
            Margin     = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        };
        stack.Children.Add(_txDesc);

        // ── Qty ───────────────────────────────────────────────────────────
        stack.Children.Add(Lbl("Količina *  (negativno = odvzem)"));
        _txQty = new TextBox { Text = "1", Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(_txQty);

        // ── Price ─────────────────────────────────────────────────────────
        stack.Children.Add(Lbl("Cena / kos (€)"));
        _txPrice = new TextBox { Text = "0", Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(_txPrice);

        // ── Supplier ─────────────────────────────────────────────────────
        stack.Children.Add(Lbl("Dobavitelj"));
        _txSupplier = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(_txSupplier);

        // ── Notes ─────────────────────────────────────────────────────────
        stack.Children.Add(Lbl("Opomba"));
        _txNotes = new TextBox
        {
            Height              = 50,
            AcceptsReturn       = true,
            TextWrapping        = TextWrapping.Wrap,
            Margin              = new Thickness(0, 0, 0, 14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        stack.Children.Add(_txNotes);

        // ── Buttons ───────────────────────────────────────────────────────
        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnCancel = new Button { Content = "Prekliči", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 6, 0) };
        var btnSave   = new Button { Content = "💾 Dodaj", Padding  = new Thickness(18, 7, 18, 7) };
        btnSave.Style = (Style)Application.Current.Resources["AccentButton"];
        btnCancel.Click += (_, _) => { DialogResult = false; Close(); };
        btnSave.Click   += Save_Click;
        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnSave);
        stack.Children.Add(btnRow);

        outer.Child = stack;
        Content     = outer;

        Loaded += (_, _) => _txSku.Focus();
    }

    // ── SKU autocomplete ──────────────────────────────────────────────────────

    private void TxSku_TextChanged(object s, TextChangedEventArgs e)
    {
        string q = _txSku.Text.Trim();

        // Show suggestions
        if (q.Length >= 1)
        {
            var matches = _allComponents
                .Where(c => c.Sku.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                         || c.Description.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(12)
                .Select(c => $"{c.Sku}  —  {c.Description}  [{c.StockSum:F0} {c.Unit}]")
                .ToList();

            _lbSuggest.ItemsSource = matches;
            _lbSuggest.Visibility  = matches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            _lbSuggest.Visibility = Visibility.Collapsed;
        }

        // Resolve exact SKU immediately
        var comp = _db.GetComponent(q);
        _txDesc.Text = comp != null
            ? $"{comp.Description}  |  Zaloga: {comp.StockSum:F0} {comp.Unit}  |  Cena: {comp.LastPrice:F4} €"
            : (q.Length > 0 ? "SKU ni v knjižnici" : "");

        if (comp != null && string.IsNullOrEmpty(_txPrice.Text.Replace("0", "").Trim()))
            _txPrice.Text = comp.LastPrice.ToString("F4");
    }

    private void TxSku_KeyDown(object s, KeyEventArgs e)
    {
        if (_lbSuggest.Visibility == Visibility.Visible)
        {
            if (e.Key == Key.Down)
            {
                _lbSuggest.Focus();
                _lbSuggest.SelectedIndex = 0;
                e.Handled = true;
            }
            if (e.Key == Key.Escape)
            {
                _lbSuggest.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }
    }

    private void ApplySuggestion()
    {
        if (_lbSuggest.SelectedItem is not string item) return;
        // Extract SKU (before the first "  —  ")
        string sku = item.Split(new[] { "  —  " }, StringSplitOptions.None)[0].Trim();
        _txSku.Text = sku;
        _txSku.CaretIndex = sku.Length;
        _lbSuggest.Visibility = Visibility.Collapsed;
        _txQty.Focus();
    }

    // ── Type → qty sign hint ──────────────────────────────────────────────────

    private void UpdateQtySign()
    {
        // ManualOut index = 2
        if (_cmbType.SelectedIndex == 2 && double.TryParse(_txQty.Text, out double q) && q > 0)
            _txQty.Text = (-q).ToString("F0");
        else if (_cmbType.SelectedIndex != 2 && double.TryParse(_txQty.Text, out double q2) && q2 < 0)
            _txQty.Text = (-q2).ToString("F0");
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    private void Save_Click(object s, RoutedEventArgs e)
    {
        string sku = _txSku.Text.Trim();
        if (string.IsNullOrEmpty(sku))
        { MessageBox.Show("SKU je obvezen.", "Napaka", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        if (!double.TryParse(_txQty.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double qty) || qty == 0)
        { MessageBox.Show("Vnesi veljavno količino (≠ 0).", "Napaka", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        double price = 0;
        double.TryParse(_txPrice.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out price);

        // Map ComboBox index → TransactionType
        var type = _cmbType.SelectedIndex switch
        {
            0 => TransactionType.Purchase,
            1 => TransactionType.ManualIn,
            2 => TransactionType.ManualOut,
            3 => TransactionType.Adjustment,
            _ => TransactionType.ManualIn
        };

        // For ManualOut / Adjustment, qty should be signed accordingly
        // Convention: ManualOut index=2 → negative, Adjustment keeps sign as entered
        if (type == TransactionType.ManualOut && qty > 0) qty = -qty;

        var comp = _db.GetComponent(sku);
        var tx = new StockTransaction
        {
            ComponentSku         = sku,
            ComponentDescription = comp?.Description ?? sku,
            Type                 = type,
            Date                 = DateTime.Now,
            Qty                  = qty,
            UnitPrice            = price,
            Supplier             = _txSupplier.Text.Trim(),
            Notes                = _txNotes.Text.Trim(),
        };

        _db.AddTransaction(tx);   // also updates stock

        DialogResult = true;
        Close();
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static TextBlock Lbl(string text) => new()
    {
        Text       = text,
        Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SubTextBrush"],
        FontSize   = 11,
        Margin     = new Thickness(0, 6, 0, 2)
    };
}
