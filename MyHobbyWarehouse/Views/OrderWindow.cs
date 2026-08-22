using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MyHobbyWarehouse.Models;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Views;

public class OrderWindow : Window
{
    private readonly List<OrderLine> _lines;
    private readonly string _projectName;

    public OrderWindow(Project project, List<BomLine> bom, int boards)
    {
        Title  = TranslationService.Get("OrderTitle", project.DisplayName, boards);
        Width  = 720; Height = 520;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _projectName = project.DisplayName;
        _lines = ComputeOrder(bom, boards);

        var outer = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.Resources["BgBrush"],
            Padding    = new Thickness(16)
        };
        var stack = new StackPanel();

        stack.Children.Add(new TextBlock
        {
            Text = TranslationService.Get("OrderInfo", project.DisplayName, boards, _lines.Count),
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var grid = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Margin = new Thickness(0, 0, 0, 8),
            MaxHeight = 360,
            AlternatingRowBackground = (System.Windows.Media.Brush)Application.Current.Resources["CardBrush"]
        };
        grid.Columns.Add(new DataGridTextColumn { Header = TranslationService.Get("ColSku"), Binding = new System.Windows.Data.Binding("Sku"), Width = new DataGridLength(70) });
        grid.Columns.Add(new DataGridTextColumn { Header = TranslationService.Get("ColDescription"), Binding = new System.Windows.Data.Binding("Description"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = TranslationService.Get("ColLocation"), Binding = new System.Windows.Data.Binding("Location"), Width = new DataGridLength(70) });
        grid.Columns.Add(new DataGridTextColumn { Header = TranslationService.Get("ExportBomColQty"), Binding = new System.Windows.Data.Binding("Needed"), Width = new DataGridLength(55) });
        grid.Columns.Add(new DataGridTextColumn { Header = TranslationService.Get("ColStock"), Binding = new System.Windows.Data.Binding("InStock"), Width = new DataGridLength(55) });
        grid.Columns.Add(new DataGridTextColumn { Header = TranslationService.Get("ColToOrder"), Binding = new System.Windows.Data.Binding("ToOrder"), Width = new DataGridLength(55) });
        grid.Columns.Add(new DataGridTextColumn { Header = TranslationService.Get("ColUnit"), Binding = new System.Windows.Data.Binding("Unit"), Width = new DataGridLength(50) });
        grid.Columns.Add(new DataGridTextColumn { Header = TranslationService.Get("ColCostPerLine"), Binding = new System.Windows.Data.Binding("DisplayCost"), Width = new DataGridLength(80) });
        grid.ItemsSource = _lines;
        stack.Children.Add(grid);

        double totalCost = _lines.Sum(l => l.Cost);
        stack.Children.Add(new TextBlock
        {
            Text = TranslationService.Get("OrderTotal", totalCost),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnCopy = new Button { Content = TranslationService.Get("CopyList"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 6, 0) };
        btnCopy.Click += (_, _) => CopyToClipboard();
        var btnExport = new Button { Content = TranslationService.Get("ExportCsv"), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 6, 0) };
        btnExport.Click += BtnExport_Click;
        var btnClose = new Button { Content = TranslationService.Get("Close"), Padding = new Thickness(12, 6, 12, 6) };
        btnClose.Click += (_, _) => { DialogResult = true; Close(); };
        btnRow.Children.Add(btnCopy);
        btnRow.Children.Add(btnExport);
        btnRow.Children.Add(btnClose);
        stack.Children.Add(btnRow);

        outer.Child = stack;
        Content = outer;
    }

    private static List<OrderLine> ComputeOrder(List<BomLine> bom, int boards)
    {
        var lines = new List<OrderLine>();
        foreach (var bl in bom)
        {
            // Primary component
            if (!string.IsNullOrEmpty(bl.Sku))
            {
                double needed = bl.Qty * boards;
                double stock = bl.AvailableStock;
                double toOrder = System.Math.Max(0, needed - stock);
                if (toOrder > 0)
                    lines.Add(new OrderLine
                    {
                        Sku = bl.Sku,
                        Description = bl.DisplayDescription,
                        Location = bl.DisplayLocation,
                        Needed = needed,
                        InStock = stock,
                        ToOrder = toOrder,
                        Unit = bl.Unit,
                        Cost = (bl.Component?.LastPrice ?? 0) * toOrder
                    });
            }
            // Secondary component (Sku2) — also needed, order if missing
            if (!string.IsNullOrEmpty(bl.Sku2))
            {
                double needed2 = bl.Qty * boards;
                double stock2 = bl.AvailableStock2;
                double toOrder2 = System.Math.Max(0, needed2 - stock2);
                if (toOrder2 > 0)
                    lines.Add(new OrderLine
                    {
                        Sku = bl.Sku2,
                        Description = bl.Component2?.Description ?? bl.Sku2,
                        Location = bl.Component2?.DisplayLocation ?? "",
                        Needed = needed2,
                        InStock = stock2,
                        ToOrder = toOrder2,
                        Unit = bl.Unit,
                        Cost = (bl.Component2?.LastPrice ?? 0) * toOrder2
                    });
            }
        }
        return lines;
    }

    private void CopyToClipboard()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{TranslationService.Get("ColSku")}\t{TranslationService.Get("ColDescription")}\t{TranslationService.Get("ColToOrder")}\t{TranslationService.Get("ColUnit")}");
        foreach (var l in _lines)
            sb.AppendLine($"{l.Sku}\t{l.Description}\t{l.ToOrder}\t{l.Unit}");
        Clipboard.SetText(sb.ToString());
        MessageBox.Show(TranslationService.Get("ListCopied"), TranslationService.Get("InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnExport_Click(object s, RoutedEventArgs e)
    {
        string safeName = string.Concat(_projectName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        string prefix   = TranslationService.Get("OrderFilePrefix");
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = TranslationService.Get("ImportCsvFilter"),
            FileName = $"{prefix}_{safeName}_{DateTime.Now:yyyy-MM-dd}.csv"
        };
        if (dlg.ShowDialog() != true) return;
        using var w = new System.IO.StreamWriter(dlg.FileName);
        w.WriteLine("Sku;Description;Location;Needed;InStock;ToOrder;Unit;Cost");
        foreach (var l in _lines)
            w.WriteLine($"{l.Sku};{l.Description};{l.Location};{l.Needed};{l.InStock};{l.ToOrder};{l.Unit};{l.Cost:F4}");
        MessageBox.Show(TranslationService.Get("OrderExported", dlg.FileName), TranslationService.Get("InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

public class OrderLine
{
    public string Sku { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public double Needed { get; set; }
    public double InStock { get; set; }
    public double ToOrder { get; set; }
    public string Unit { get; set; } = "pcs";
    public double Cost { get; set; }
    public string DisplayCost => Cost > 0 ? TranslationService.Get("DisplayLineCostFormat", Cost) : TranslationService.Get("ValueNone");
}
