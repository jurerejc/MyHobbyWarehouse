using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyHobbyWarehouse.Data;
using MyHobbyWarehouse.Models;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Views;

/// <summary>
/// Detail window for one transaction group (one PCB build).
/// Shows all individual component transactions with options to:
///   - Redo selected individual transaction
///   - Redo entire group at once
/// </summary>
public class TransactionDetailWindow : Window
{
    private readonly DatabaseService   _db;
    private readonly TransactionGroup  _group;
    private readonly DataGrid          _grid;
    private List<StockTransaction>     _items = [];

    public bool StockChanged { get; private set; }

    public TransactionDetailWindow(TransactionGroup group, DatabaseService db)
    {
        _db    = db;
        _group = group;

        Title  = TranslationService.Get("TxGroupDetail", group.Tag.Length > 60 ? group.Tag[..60] + "…" : group.Tag);
        Width  = 860; Height = 560;
        MinWidth = 700; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = new Border
        {
            Background = (Brush)Application.Current.Resources["BgBrush"],
            Padding    = new Thickness(16)
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Header ────────────────────────────────────────────────────────────
        var hdrPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        var tagTb = new TextBlock
        {
            Text       = group.Tag,
            FontSize   = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["AccentBrush"],
            TextWrapping = TextWrapping.Wrap
        };
        hdrPanel.Children.Add(tagTb);

        var subRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        void AddBadge(string txt)
        {
            var b = new Border
            {
                Background    = (Brush)Application.Current.Resources["CardBrush"],
                CornerRadius  = new CornerRadius(3),
                Padding       = new Thickness(8, 3, 8, 3),
                Margin        = new Thickness(0, 0, 8, 0)
            };
            b.Child = new TextBlock
            {
                Text       = txt,
                FontSize   = 11,
                Foreground = (Brush)Application.Current.Resources["SubTextBrush"]
            };
            subRow.Children.Add(b);
        }
        AddBadge(group.DisplayDate);
        if (!string.IsNullOrEmpty(group.ProjectName)) AddBadge(group.ProjectName);
        AddBadge(group.DisplayType);
        AddBadge(group.DisplayCount);
        hdrPanel.Children.Add(subRow);

        Grid.SetRow(hdrPanel, 0); root.Children.Add(hdrPanel);

        // ── DataGrid ──────────────────────────────────────────────────────────
        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows      = false,
            IsReadOnly          = true,
            SelectionMode       = DataGridSelectionMode.Single,
        };

        DataGridTextColumn Col(string h, string b, int w, bool stretch = false)
        {
            var col = new DataGridTextColumn
            {
                Header  = h,
                Binding = new System.Windows.Data.Binding(b),
            };
            col.Width = stretch
                ? new DataGridLength(1, DataGridLengthUnitType.Star)
                : new DataGridLength(w);
            return col;
        }

        _grid.Columns.Add(Col(TranslationService.Get("TransactionDetailColDate"), "DisplayDate",         130));
        _grid.Columns.Add(Col("SKU",       "ComponentSku",         70));
        _grid.Columns.Add(Col(TranslationService.Get("ColDescription"),          "ComponentDescription", 0, true));
        _grid.Columns.Add(Col(TranslationService.Get("ColType"),                "DisplayType",          90));
        _grid.Columns.Add(Col(TranslationService.Get("TransactionDetailColQuantity"), "DisplayQty",       60));
        _grid.Columns.Add(Col(TranslationService.Get("ColPrice"),               "DisplayPrice",         80));
        _grid.Columns.Add(Col(TranslationService.Get("ColSupplier"),            "Supplier",             90));

        Grid.SetRow(_grid, 1); root.Children.Add(_grid);

        // ── Buttons ───────────────────────────────────────────────────────────
        var btnPanel = new Border
        {
            Background    = (Brush)Application.Current.Resources["PanelBrush"],
            BorderBrush   = (Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding       = new Thickness(0, 10, 0, 0),
            Margin        = new Thickness(0, 8, 0, 0)
        };
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal };

        var btnRedoOne = new Button
        {
            Content = "↩ " + TranslationService.Get("TxReverseSelected"),
            Padding = new Thickness(14, 7, 14, 7),
            Margin  = new Thickness(0, 0, 8, 0)
        };
        btnRedoOne.Click += BtnRedoOne_Click;

        var btnRedoAll = new Button
        {
            Content = "↩↩ " + TranslationService.Get("TxReverseAll"),
            Padding = new Thickness(14, 7, 14, 7),
            Margin  = new Thickness(0, 0, 8, 0)
        };
        btnRedoAll.Style = (Style)Application.Current.Resources["AccentButton"];
        btnRedoAll.Click += BtnRedoAll_Click;

        var btnClose = new Button
        {
            Content = TranslationService.Get("Close"),
            Padding = new Thickness(14, 7, 14, 7)
        };
        btnClose.Click += (_, _) => Close();

        btnRow.Children.Add(btnRedoOne);
        btnRow.Children.Add(btnRedoAll);
        btnRow.Children.Add(btnClose);
        btnPanel.Child = btnRow;
        Grid.SetRow(btnPanel, 2); root.Children.Add(btnPanel);

        outer.Child = root;
        Content     = outer;

        Loaded += (_, _) => LoadData();
    }

    private void LoadData()
    {
        _items = _db.GetTransactionsByTag(_group.Tag);
        _grid.ItemsSource = _items;
    }

    // ── Redo single ───────────────────────────────────────────────────────────

    private void BtnRedoOne_Click(object s, RoutedEventArgs e)
    {
        if (_grid.SelectedItem is not StockTransaction tx) return;

        string direction = tx.Qty < 0 ? TranslationService.Get("TransactionDetailAddsBack") : TranslationService.Get("TransactionDetailRemoves");
        string qty = Math.Abs(tx.Qty).ToString("F0");

        if (MessageBox.Show(
            TranslationService.Get("TxReverseConfirm", tx.ComponentSku, tx.ComponentDescription, direction, qty),
            TranslationService.Get("TransactionDetailUndoTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question)
            != MessageBoxResult.Yes) return;

        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            _db.ReverseTransaction(tx);
            StockChanged = true;
            SetStatus(TranslationService.Get("TxReverseSingleDone", tx.ComponentSku));
        }
        finally { System.Windows.Input.Mouse.OverrideCursor = null; }
    }

    // ── Redo all ─────────────────────────────────────────────────────────────

    private void BtnRedoAll_Click(object s, RoutedEventArgs e)
    {
        if (_items.Count == 0) return;

        if (MessageBox.Show(
            TranslationService.Get("TxReverseGroupConfirm", _items.Count, _group.Tag),
            TranslationService.Get("TransactionDetailUndoGroupTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;

        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            int count = _db.ReverseTransactionGroup(_group.Tag);
            StockChanged = true;
            System.Windows.Input.Mouse.OverrideCursor = null;
            MessageBox.Show(TranslationService.Get("TxReverseDone", count), TranslationService.Get("TransactionDetailDoneTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally { System.Windows.Input.Mouse.OverrideCursor = null; }
        Close();
    }

    private void SetStatus(string msg)
    {
        Title = $"{msg} — {_group.Tag[..Math.Min(40, _group.Tag.Length)]}";
    }
}
