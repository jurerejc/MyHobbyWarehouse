using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyHobbyWarehouse.Data;
using MyHobbyWarehouse.Models;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Views;

/// <summary>
/// Assigns a single image file to multiple components by copying it
/// as {SKU}.ext for each selected component.
/// </summary>
public class MultiImageAssignDialog : Window
{
    private readonly DatabaseService    _db;
    private readonly string             _sourcePath;
    private readonly string             _sourceSku;
    private readonly List<Component>?   _filteredComponents;

    private List<Component>             _allComponents = [];
    private readonly TextBox            _txSearch;
    private readonly StackPanel         _itemsPanel;
    private readonly ScrollViewer       _scroll;
    private readonly TextBlock          _txResult;

    public MultiImageAssignDialog(string sourcePath, string sourceSku, DatabaseService db, List<Component>? filteredComponents = null)
    {
        _db         = db;
        _sourcePath = sourcePath;
        _sourceSku  = sourceSku;
        _filteredComponents = filteredComponents;

        Title  = "Dodeli sliko vec elementom";
        Width  = 620; Height = 660;
        MinWidth = 500; MinHeight = 400;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = new Border
        {
            Background = (Brush)Application.Current.Resources["BgBrush"],
            Padding    = new Thickness(16)
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // header + preview
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // search
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // select all row
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // list
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // result + buttons

        // ── Header + image preview ─────────────────────────────────────────
        var topGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Image preview
        var imgBorder = new Border
        {
            Width = 170, Height = 130,
            Background      = (Brush)Application.Current.Resources["CardBrush"],
            BorderBrush     = (Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(3)
        };
        var img = new Image { Stretch = Stretch.Uniform, MaxWidth = 166, MaxHeight = 126 };
        var bmp = ImageService.LoadBitmap(sourcePath);
        img.Source = bmp;
        imgBorder.Child = img;
        Grid.SetColumn(imgBorder, 0);
        topGrid.Children.Add(imgBorder);

        // Info panel
        var info = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };

        info.Children.Add(new TextBlock
        {
            Text       = "Dodeli sliko",
            FontSize   = 16, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["AccentBrush"],
            Margin     = new Thickness(0, 0, 0, 6)
        });
        info.Children.Add(new TextBlock
        {
            Text       = System.IO.Path.GetFileName(sourcePath),
            FontSize   = 11,
            Foreground = (Brush)Application.Current.Resources["SubTextBrush"],
            TextWrapping = TextWrapping.Wrap,
            Margin     = new Thickness(0, 0, 0, 4)
        });
        info.Children.Add(new TextBlock
        {
            Text       = "Za vsak izbran element se ustvari kopija slike s SKU imenom.",
            FontSize   = 11,
            Foreground = (Brush)Application.Current.Resources["SubTextBrush"],
            TextWrapping = TextWrapping.Wrap,
            Margin     = new Thickness(0, 0, 0, 4)
        });
        info.Children.Add(new TextBlock
        {
            Text       = $"Izvor: {sourceSku}",
            FontSize   = 11, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextBrush"]
        });
        Grid.SetColumn(info, 1);
        topGrid.Children.Add(info);

        Grid.SetRow(topGrid, 0);
        root.Children.Add(topGrid);

        // ── Search ────────────────────────────────────────────────────────
        var searchRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        searchRow.Children.Add(new TextBlock
        {
            Text = "Iskanje:", VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["SubTextBrush"],
            Margin = new Thickness(0, 0, 8, 0), FontSize = 11
        });
        _txSearch = new TextBox { Width = 260, Margin = new Thickness(0, 0, 8, 0) };
        _txSearch.TextChanged += (_, _) => RefreshList();
        searchRow.Children.Add(_txSearch);

        var btnSelectAll = new Button { Content = "Izberi vse", Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 6, 0) };
        btnSelectAll.Click += (_, _) => SetAllChecked(true);
        var btnClearAll = new Button { Content = "Pocisti vse", Padding = new Thickness(10, 4, 10, 4) };
        btnClearAll.Click += (_, _) => SetAllChecked(false);
        searchRow.Children.Add(btnSelectAll);
        searchRow.Children.Add(btnClearAll);

        Grid.SetRow(searchRow, 1);
        root.Children.Add(searchRow);

        // ── Column headers ─────────────────────────────────────────────────
        var colHdr = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        colHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

        void AddHdr(string t, int col)
        {
            var tb = new TextBlock
            {
                Text = t, FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["AccentBrush"],
                Margin = new Thickness(2, 0, 0, 0)
            };
            Grid.SetColumn(tb, col);
            colHdr.Children.Add(tb);
        }
        AddHdr("", 0); AddHdr("SKU", 1); AddHdr("Opis", 2); AddHdr("Kategorija", 3);

        Grid.SetRow(colHdr, 2);
        root.Children.Add(colHdr);

        // ── Items list ────────────────────────────────────────────────────
        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = (Brush)Application.Current.Resources["PanelBrush"],
            Margin     = new Thickness(0, 0, 0, 8)
        };
        _itemsPanel = new StackPanel();
        _scroll.Content = _itemsPanel;

        Grid.SetRow(_scroll, 3);
        root.Children.Add(_scroll);

        // ── Result label + buttons ────────────────────────────────────────
        var bottomPanel = new Grid();
        bottomPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottomPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _txResult = new TextBlock
        {
            FontSize   = 11,
            Foreground = (Brush)Application.Current.Resources["SubTextBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_txResult, 0);
        bottomPanel.Children.Add(_txResult);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var btnCancel = new Button { Content = "Zapri", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 6, 0) };
        btnCancel.Click += (_, _) => Close();

        var btnAssign = new Button { Content = "📋 Kopiraj sliko za izbrane", Padding = new Thickness(14, 7, 14, 7) };
        btnAssign.Style = (Style)Application.Current.Resources["AccentButton"];
        btnAssign.Click += BtnAssign_Click;

        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnAssign);
        Grid.SetColumn(btnRow, 1);
        bottomPanel.Children.Add(btnRow);

        Grid.SetRow(bottomPanel, 4);
        root.Children.Add(bottomPanel);

        outer.Child = root;
        Content     = outer;

        Loaded += (_, _) =>
        {
            _allComponents = _filteredComponents ?? _db.GetAllComponents();
            RefreshList();
        };
    }

    // ── List management ───────────────────────────────────────────────────

    private void RefreshList()
    {
        _itemsPanel.Children.Clear();
        string q = _txSearch.Text.Trim();

        var filtered = string.IsNullOrEmpty(q)
            ? _allComponents
            : _allComponents.Where(c =>
                c.Sku.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                c.Category1.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var comp in filtered)
        {
            var row = BuildRow(comp);
            _itemsPanel.Children.Add(row);
        }

        UpdateResultLabel();
    }

    private UIElement BuildRow(Component comp)
    {
        var border = new Border
        {
            Padding       = new Thickness(4, 3, 4, 3),
            BorderBrush   = (Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

        var chk = new CheckBox
        {
            Tag           = comp.Sku,
            IsChecked     = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        // Pre-check the source SKU
        if (comp.Sku == _sourceSku) chk.IsChecked = true;
        // Indicate if image already exists
        bool hasImg = ImageService.FindImage(comp.Sku) != null;
        chk.Checked   += (_, _) => UpdateResultLabel();
        chk.Unchecked += (_, _) => UpdateResultLabel();

        var tbSku = new TextBlock
        {
            Text      = comp.Sku,
            FontSize  = 12, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["AccentBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin    = new Thickness(2, 0, 0, 0)
        };
        var tbDesc = new TextBlock
        {
            Text      = comp.Description,
            FontSize  = 11,
            Foreground = hasImg
                ? (Brush)Application.Current.Resources["TextBrush"]
                : (Brush)Application.Current.Resources["SubTextBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin   = new Thickness(2, 0, 0, 0),
            ToolTip  = hasImg ? "Ze ima sliko" : null
        };
        var tbCat = new TextBlock
        {
            Text     = comp.Category1,
            FontSize = 10,
            Foreground = (Brush)Application.Current.Resources["SubTextBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin   = new Thickness(2, 0, 0, 0)
        };

        Grid.SetColumn(chk,   0); grid.Children.Add(chk);
        Grid.SetColumn(tbSku, 1); grid.Children.Add(tbSku);
        Grid.SetColumn(tbDesc,2); grid.Children.Add(tbDesc);
        Grid.SetColumn(tbCat, 3); grid.Children.Add(tbCat);

        border.Child = grid;
        return border;
    }

    private void SetAllChecked(bool check)
    {
        foreach (Border b in _itemsPanel.Children)
            if (b.Child is Grid g)
                foreach (UIElement el in g.Children)
                    if (el is CheckBox chk) chk.IsChecked = check;
        UpdateResultLabel();
    }

    private List<string> GetSelectedSkus()
    {
        var result = new List<string>();
        foreach (Border b in _itemsPanel.Children)
            if (b.Child is Grid g)
                foreach (UIElement el in g.Children)
                    if (el is CheckBox chk && chk.IsChecked == true)
                        result.Add(chk.Tag?.ToString() ?? "");
        return result.Where(s => !string.IsNullOrEmpty(s)).ToList();
    }

    private void UpdateResultLabel()
    {
        int count = GetSelectedSkus().Count;
        _txResult.Text = count == 0 ? "Ni izbranih" : $"Izbranih: {count} elementov";
    }

    // ── Assign ────────────────────────────────────────────────────────────

    private void BtnAssign_Click(object s, RoutedEventArgs e)
    {
        var skus = GetSelectedSkus();
        if (skus.Count == 0)
        {
            MessageBox.Show("Ni izbranih elementov.", "Opozorilo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!File.Exists(_sourcePath))
        {
            MessageBox.Show($"Izvorna slika ne obstaja:\n{_sourcePath}", "Napaka",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        int ok = 0, failed = 0;
        string? firstError = null;
        try
        {
            foreach (string sku in skus)
            {
                if (sku == _sourceSku) { ok++; continue; } // already has the image
                try
                {
                    ImageService.SaveImage(sku, _sourcePath);
                    ok++;
                }
                catch (Exception ex)
                {
                    failed++;
                    firstError ??= $"{sku}: {ex.Message}";
                }
            }
        }
        finally
        {
            System.Windows.Input.Mouse.OverrideCursor = null;
        }

        string msg = failed == 0
            ? $"Slika dodeljena {ok} elementom."
            : $"Dodeljena {ok} elementom, {failed} napak.";
        if (firstError != null) msg += $"\n\nPrva napaka: {firstError}";

        _txResult.Text = msg;
        _txResult.Foreground = failed == 0
            ? (Brush)Application.Current.Resources["OkBrush"]
            : (Brush)Application.Current.Resources["WarnBrush"];

        MessageBox.Show(msg, "Dokoncano", MessageBoxButton.OK,
            failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
