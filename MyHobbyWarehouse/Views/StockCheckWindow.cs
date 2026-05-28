using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyHobbyWarehouse.Data;
using MyHobbyWarehouse.Models;

namespace MyHobbyWarehouse.Views;

/// <summary>
/// Read-only popup showing full stock availability for every BOM line.
/// Color-coded rows: green / amber / red.
/// </summary>
public class StockCheckWindow : Window
{
    private readonly DatabaseService _db;
    private readonly Project         _project;
    private readonly List<BomLine>   _lines;

    public StockCheckWindow(Project project, List<BomLine> lines, DatabaseService db)
    {
        _db      = db;
        _project = project;
        _lines   = lines;

        Title  = $"Preverjanje zaloge — {_project.DisplayName}";
        Width  = 1020; Height = 660;
        MinWidth = 800; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        BuildUi();
    }

    private void BuildUi()
    {
        var outer = new Border
        {
            Background = (Brush)Application.Current.Resources["BgBrush"],
            Padding    = new Thickness(16)
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ── Header ────────────────────────────────────────────────────────
        var hdr = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        hdr.Children.Add(new TextBlock
        {
            Text       = _project.DisplayName,
            FontSize   = 18, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["AccentBrush"]
        });
        if (!string.IsNullOrEmpty(_project.BoardName))
            hdr.Children.Add(Sub(_project.BoardName));
        if (!string.IsNullOrEmpty(_project.Description))
            hdr.Children.Add(Sub(_project.Description));
        Grid.SetRow(hdr, 0);
        root.Children.Add(hdr);

        // ── DataGrid ──────────────────────────────────────────────────────
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows      = false,
            CanUserDeleteRows   = false,
            IsReadOnly          = true,
            SelectionMode       = DataGridSelectionMode.Single,
        };

        grid.Columns.Add(Col("St.",       "DisplayStatus",       50));
        grid.Columns.Add(Col("SKU",       "Sku",                 65));
        grid.Columns.Add(Col("Opis",      "DisplayDescription", 220, true));
        grid.Columns.Add(Col("Qty",       "Qty",                 50));
        grid.Columns.Add(Col("Enota",     "Unit",                55));
        grid.Columns.Add(Col("Na voljo",  "AvailableStock",       80));
        grid.Columns.Add(Col("Razlika",   "StockDiff",           70));
        grid.Columns.Add(Col("€/vrs.",    "DisplayLineCost",      80));
        grid.Columns.Add(Col("Package",   "Package",              80));
        grid.Columns.Add(Col("SKU2",      "Sku2",                 60));
        grid.Columns.Add(Col("St.2",      "DisplayStatus2",       45));
        grid.Columns.Add(Col("Designatorji","PartDesignators",    0, true));

        // Build extended view model with diff
        var rows = _lines.Select(l => new BomCheckRow(l)).ToList();
        grid.ItemsSource = rows;

        // Row color style applied in code — use DataGrid.RowStyle
        grid.LoadingRow += (_, e) =>
        {
            if (e.Row.Item is BomCheckRow row)
            {
                e.Row.Background = row.Line.Status switch
                {
                    StockStatus.Ok  => new SolidColorBrush(Color.FromArgb(28, 78, 201, 148)),
                    StockStatus.Low => new SolidColorBrush(Color.FromArgb(38, 255, 179,  71)),
                    StockStatus.Out => new SolidColorBrush(Color.FromArgb(45, 244, 113, 116)),
                    _               => Brushes.Transparent
                };
            }
        };

        Grid.SetRow(grid, 1);
        root.Children.Add(grid);

        // ── Summary footer ────────────────────────────────────────────────
        double totalCost = _lines.Sum(l => l.LineCost);
        double totalMassG = _lines.Sum(l => (l.Component?.MassMg ?? 0) * l.Qty) / 1000.0;
        int ok   = _lines.Count(l => l.Status == StockStatus.Ok);
        int low  = _lines.Count(l => l.Status == StockStatus.Low);
        int miss = _lines.Count(l => l.Status == StockStatus.Out);
        int unk  = _lines.Count(l => l.Status == StockStatus.Unknown);

        var footer = new Border
        {
            Background    = (Brush)Application.Current.Resources["PanelBrush"],
            BorderBrush   = (Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding       = new Thickness(10, 8, 10, 8),
            Margin        = new Thickness(0, 6, 0, 0)
        };

        var sumGrid = new Grid();
        for (int i = 0; i < 6; i++)
            sumGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        sumGrid.Children.Add(SumBlock(0, "Skupaj vrstic",    $"{_lines.Count}",         "#FFCCCCCC"));
        sumGrid.Children.Add(SumBlock(1, "✅ OK",             $"{ok}",                   "#FF4EC994"));
        sumGrid.Children.Add(SumBlock(2, "⚠️ Premalo",        $"{low}",                  "#FFFFB347"));
        sumGrid.Children.Add(SumBlock(3, "❌ Ni na zalogi",   $"{miss}",                 "#FFF47174"));
        sumGrid.Children.Add(SumBlock(4, "Skupaj strošek",   $"{totalCost:F2} €",        "#FF4EC994"));
        sumGrid.Children.Add(SumBlock(5, "Skupaj masa",      $"{totalMassG:F2} g",       "#FFCCCCCC"));

        footer.Child = sumGrid;
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        outer.Child = root;
        Content     = outer;
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private static DataGridTextColumn Col(string header, string binding, int width, bool stretch = false)
    {
        var col = new DataGridTextColumn
        {
            Header  = header,
            Binding = new System.Windows.Data.Binding(binding),
        };
        col.Width = stretch ? new DataGridLength(1, DataGridLengthUnitType.Star)
                            : width > 0 ? new DataGridLength(width) : DataGridLength.Auto;
        return col;
    }

    private static TextBlock Sub(string text) => new()
    {
        Text       = text,
        Foreground = (Brush)Application.Current.Resources["SubTextBrush"],
        FontSize   = 11,
        Margin     = new Thickness(0, 1, 0, 0)
    };

    private static StackPanel SumBlock(int col, string label, string value, string hexColor)
    {
        var sp = new StackPanel { Margin = new Thickness(6, 0, 6, 0) };
        sp.Children.Add(new TextBlock
        {
            Text       = label,
            FontSize   = 10,
            Foreground = (Brush)Application.Current.Resources["SubTextBrush"]
        });
        sp.Children.Add(new TextBlock
        {
            Text       = value,
            FontSize   = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(hexColor))
        });
        Grid.SetColumn(sp, col);
        return sp;
    }
}

/// <summary>Row wrapper that adds a StockDiff computed property for the grid.</summary>
public class BomCheckRow
{
    public BomLine Line { get; }

    public BomCheckRow(BomLine line) => Line = line;

    // Proxy all bound properties through Line
    public string Sku               => Line.Sku;
    public string Sku2              => Line.Sku2;
    public double Qty               => Line.Qty;
    public string Unit              => Line.Unit;
    public string Package           => Line.Package;
    public string PartDesignators   => Line.PartDesignators;
    public double AvailableStock    => Line.AvailableStock;
    public string DisplayStatus     => Line.DisplayStatus;
    public string DisplayStatus2    => Line.DisplayStatus2;
    public string DisplayDescription => Line.DisplayDescription;
    public string DisplayLineCost   => Line.DisplayLineCost;

    /// <summary>Positive = surplus, negative = shortage.</summary>
    public string StockDiff
    {
        get
        {
            double diff = Line.AvailableStock - Line.Qty;
            return diff >= 0 ? $"+{diff:F0}" : $"{diff:F0}";
        }
    }
}
