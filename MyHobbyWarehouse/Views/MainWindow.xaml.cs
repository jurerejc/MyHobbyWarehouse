using System.Windows.Data;
using ICollectionView = System.ComponentModel.ICollectionView;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using MyHobbyWarehouse.Data;
using MyHobbyWarehouse.Models;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Views;

public partial class MainWindow : Window
{
    private readonly DatabaseService _db;
    private List<Models.Component>          _allComponents = [];
    private ICollectionView?         _componentsView;
    private readonly Dictionary<string, HashSet<string>?> _columnFilters = new();
    private List<BomLine>            _currentBom = [];

    public MainWindow()
    {
        InitializeComponent();
        _db = DatabaseService.Current!;
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Title = $"{TranslationService.Get("AppTitle")}  v{v!.Major}.{v.Minor}";
        Loaded += (_, _) => Initialize();
    }

    private void Initialize()
    {
       
        LoadComponents();
        LoadProjects();
        LoadTransactions();
        RefreshStats();
        SetStatus(TranslationService.Get("StatusReady"));
    }

    // ── Stats ────────────────────────────────────────────────────────────────

    private void RefreshStats()
    {
        var (val, types, _, projects) = _db.GetStats();
        TxtStatComponents.Text = types.ToString();        TxtStatValue.Text      = TranslationService.Get("StatsValueFormat", val);
        TxtStatProjects.Text   = projects.ToString();
    }

    // ── Library tab ──────────────────────────────────────────────────────────

    private void LoadComponents()
    {
        // Force DataGrid to exit any pending editing transaction
        try { GridComponents.CommitEdit(DataGridEditingUnit.Row, true); } catch { }
        try { (_componentsView as System.ComponentModel.IEditableCollectionView)?.CancelEdit(); } catch { }

        _allComponents  = _db.GetAllComponents();
        PrecomputeImageStatus(_allComponents);
        _componentsView = CollectionViewSource.GetDefaultView(_allComponents);
        _componentsView.Filter = FilterPredicate;
        GridComponents.ItemsSource = _componentsView;
        RefreshFilterStatus();
        UpdateFilterButtonStyles();
    }



    // ── Column filter logic ──────────────────────────────────────────────────

    /// <summary>Returns the filter-display value for a component given a column key.</summary>
    private static string GetColValue(Models.Component c, string colKey) => colKey switch
    {        "Category1"     => c.Category1,        "Category3"     => c.Category3,        "Category4"     => c.Category4,        "DisplaySmd"    => c.DisplaySmd,        "StockBucket"   => c.StockSum > 0 ? TranslationService.Get("InStock") : TranslationService.Get("OutOfStock"),        "DisplayPrice"  => c.DisplayPrice,        "DisplayLocation" => !string.IsNullOrEmpty(c.DisplayLocation) ? c.DisplayLocation : TranslationService.Get("NoLocation"),        "LastSupplier"  => !string.IsNullOrEmpty(c.LastSupplier) ? c.LastSupplier : TranslationService.Get("NoSupplier"),        _ => ""
    };
    private List<string> GetUniqueValues(string colKey)
    {        if (colKey == "StockBucket")  return [TranslationService.Get("InStock"), TranslationService.Get("OutOfStock")];        if (colKey == "DisplaySmd")   return ["SMD", "TH"];

        IEnumerable<Models.Component> src = _allComponents;
        // Apply all other active filters so values are relevant to current selection
        foreach (var (key, selected) in _columnFilters)
        {
            if (key == colKey || selected == null) continue;
            src = src.Where(c => selected.Contains(GetColValue(c, key)));
        }
        return src
            .Select(c => GetColValue(c, colKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not Models.Component c) return false;
        foreach (var (col, selected) in _columnFilters)
        {
            if (selected == null) continue;
            if (!selected.Contains(GetColValue(c, col))) return false;
        }
        return true;
    }

    // ── Filter button click ───────────────────────────────────────────────────

    private void FilterBtn_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // prevent column sort
        if (sender is not Button btn) return;
        string colKey = btn.Tag?.ToString() ?? "";
        if (string.IsNullOrEmpty(colKey)) return;

        // Position popup below the button
        var screenPos = btn.PointToScreen(new Point(0, btn.ActualHeight));

        // Clamp to screen
        var screen = System.Windows.SystemParameters.WorkArea;
        double left = Math.Min(screenPos.X, screen.Right  - 245);
        double top  = Math.Min(screenPos.Y, screen.Bottom - 370);

        var current = _columnFilters.TryGetValue(colKey, out var cf) ? cf : null;
        var values  = GetUniqueValues(colKey);

        var popup = new FilterPopup(ColKeyToLabel(colKey), values, current, new Point(left, top));
        popup.Owner = this;

        if (popup.ShowDialog() == true)
        {
            if (popup.SelectedValues == null || popup.SelectedValues.Count == 0)
                _columnFilters.Remove(colKey);
            else
                _columnFilters[colKey] = popup.SelectedValues;

            _componentsView?.Refresh();
            RefreshFilterStatus();
            UpdateFilterButtonStyles();
        }
    }

    private static string ColKeyToLabel(string colKey) => colKey switch
    {        "Category1"     => TranslationService.Get("FilterColType"),        "Category3"     => TranslationService.Get("FilterColValue"),        "Category4"     => TranslationService.Get("FilterColPackage"),        "DisplaySmd"    => TranslationService.Get("ColSmdTh"),        "StockBucket"   => TranslationService.Get("FilterColStock"),        "DisplayPrice"  => TranslationService.Get("FilterColPrice"),        "DisplayLocation" => TranslationService.Get("FilterColLocation"),        "LastSupplier"  => TranslationService.Get("FilterColSupplier"),
        _ => colKey
    };

    /// <summary>Update filter button visual: accent = active, subdued = inactive.</summary>
    private void UpdateFilterButtonStyles()
    {
        var buttons = new Dictionary<string, Button?>
        {            ["Category1"]     = FBtnTip,            ["Category3"]     = FBtnVred,            ["Category4"]     = FBtnPkg,            ["DisplaySmd"]    = FBtnSmd,            ["StockBucket"]   = FBtnStock,            ["DisplayPrice"]  = FBtnCena,            ["DisplayLocation"] = FBtnLoc,            ["LastSupplier"]  = FBtnSupp,
        };
        var accent = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"];        var sub    = (System.Windows.Media.Brush)Application.Current.Resources["SubTextBrush"];

        foreach (var (key, btn) in buttons)
        {
            if (btn == null) continue;
            bool active = _columnFilters.ContainsKey(key) && _columnFilters[key] != null;
            btn.Foreground = active ? accent : sub;            btn.Content    = active ? "▼" : "▾";
            btn.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
        }
    }

    private void BtnClearFilters_Click(object s, RoutedEventArgs e)
    {
        _columnFilters.Clear();
        _componentsView?.Refresh();
        RefreshFilterStatus();
        UpdateFilterButtonStyles();
    }

    private void RefreshFilterStatus()
    {
        int shown = _componentsView?.Cast<Models.Component>().Count() ?? 0;
        int total = _allComponents.Count;
        int active = _columnFilters.Count(kv => kv.Value != null);
        SetStatus(shown == total            ? TranslationService.Get("StatusComponents", total)            : TranslationService.Get("StatusFiltered", shown, total, active));
    }

    private void GridComponents_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (GridComponents.SelectedItem is Models.Component comp)
            ShowComponentDetail(comp);
        else
            PanelComponentDetail.Visibility = Visibility.Collapsed;
    }

    private void GridComponents_DoubleClick(object s, MouseButtonEventArgs e)
        => BtnEditComponent_Click(s, e);

    private void GridComponents_ToolTipOpening(object s, ToolTipEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not DataGridRow) dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
        var row = dep as DataGridRow;
        if (row?.DataContext is not Models.Component comp) return;
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        string? compImg = ImageService.FindImage(comp.Sku);
        if (compImg != null)
        {
            panel.Children.Add(new System.Windows.Controls.Image
            {
                Source = ImageService.LoadBitmap(compImg),
                Width = 200,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Margin = new Thickness(0, 0, 4, 0)
            });
        }
        if (!string.IsNullOrEmpty(comp.LocationCode))
        {
            string? locImg = ImageService.FindLocationImage(comp.LocationCode);
            if (locImg != null)
            {
                panel.Children.Add(new System.Windows.Controls.Image
                {
                    Source = ImageService.LoadBitmap(locImg),
                    Width = 200,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                });
            }
        }
        if (panel.Children.Count > 0)
            row.ToolTip = new ToolTip { Content = panel };
        else
        {
            row.ToolTip = null;
            e.Handled = true;
        }
    }

    private void ShowComponentDetail(Models.Component c)
    {
        PanelComponentDetail.Visibility = Visibility.Visible;        TxtDetailSku.Text   = $"SKU: {c.Sku}  |  Stara SKU: {c.OldSku}  |  Alt: {c.Alt}";
        TxtDetailDesc.Text  = c.Description;        string locPart = !string.IsNullOrEmpty(c.DisplayLocation) ? $"  ({c.DisplayLocation})" : "";
        TxtDetailStock.Text = TranslationService.Get("DetailStock", c.StockSum, c.Unit) + locPart;        TxtDetailPrice.Text = TranslationService.Get("DetailPrice", c.LastPrice, c.StockValue, c.MassMg);
        TxtDetailSupp1.Text = !string.IsNullOrEmpty(c.Supplier1Name)            ? TranslationService.Get("DetailSupplier", 1, c.Supplier1Name, c.Supplier1Sku, c.Supplier1Price) +              (!string.IsNullOrEmpty(c.Supplier1Url) ? $"  🔗 {c.Supplier1Url}" : "") : "";
        TxtDetailSupp2.Text = !string.IsNullOrEmpty(c.Supplier2Name)            ? TranslationService.Get("DetailSupplier", 2, c.Supplier2Name, c.Supplier2Sku, c.Supplier2Price) +              (!string.IsNullOrEmpty(c.Supplier2Url) ? $"  🔗 {c.Supplier2Url}" : "") : "";
        TxtDetailSupp3.Text = !string.IsNullOrEmpty(c.Supplier3Name)            ? TranslationService.Get("DetailSupplier", 3, c.Supplier3Name, c.Supplier3Sku, c.Supplier3Price) +              (!string.IsNullOrEmpty(c.Supplier3Url) ? $"  🔗 {c.Supplier3Url}" : "") : "";
        TxtDetailMfg.Text   = !string.IsNullOrEmpty(c.ManufacturerName)            ? TranslationService.Get("DetailMfg", c.ManufacturerName, c.ManufacturerPart) : "";
        TxtDetailOldSku.Text = !string.IsNullOrEmpty(c.StickerText)            ? TranslationService.Get("DetailLabel", c.StickerText) : "";
    }

    private void BtnAddComponent_Click(object s, RoutedEventArgs e)
    {
        var dlg = new ComponentEditDialog(null, _db);
        if (dlg.ShowDialog() == true) { LoadComponents(); RefreshStats(); }
    }

    private void AddComponentToProject()
    {
        if (GridComponents.SelectedItem is not Models.Component comp)
        { SetStatus(TranslationService.Get("StatusSelectComponent")); return; }

        var dlg = new AddToProjectDialog(_db, comp);
        dlg.Owner = this;
        if (dlg.ShowDialog() != true) return;

        var line = new BomLine
        {
            ProjectId = dlg.SelectedProject!.Id,
            Sku = comp.Sku,
            Qty = dlg.Quantity,
            Unit = comp.Unit,
            PartDesignators = dlg.Designators,
        };
        _db.AddBomLine(line);

        // If the target project is currently selected, refresh BOM view
        if (LstProjects.SelectedItem is Project cur && cur.Id == dlg.SelectedProject.Id)
            LoadBom(cur);

        LoadProjects();
        RefreshStats();
        SetStatus(TranslationService.Get("StatusComponentAddedToProject", comp.Sku, dlg.Quantity, dlg.SelectedProject.DisplayName));
    }

    private void BtnAddComponentToProject_Click(object s, RoutedEventArgs e) => AddComponentToProject();
    private void MnuAddComponentToProject_Click(object s, RoutedEventArgs e) => AddComponentToProject();

    private static void PrecomputeImageStatus(List<Models.Component> components)
    {
        if (components.Count == 0) return;
        var imagesDir = Services.ImageService.ImagesFolder;
        if (!Directory.Exists(imagesDir)) return;
        var withImages = Directory.EnumerateFiles(imagesDir, "*.*")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in components)
            c.HasImage = withImages.Contains(c.Sku);
    }

    private List<Models.Component> GetFilteredComponents()
        => _componentsView?.Cast<Models.Component>().ToList() ?? _allComponents;

    private void BtnEditComponent_Click(object s, RoutedEventArgs e)
    {        if (GridComponents.SelectedItem is not Models.Component comp) { SetStatus(TranslationService.Get("StatusSelectComponent")); return; }
        var filtered = GetFilteredComponents();
        var dlg = filtered.Count < _allComponents.Count
            ? new ComponentEditDialog(comp, _db, filtered)
            : new ComponentEditDialog(comp, _db);
        if (dlg.ShowDialog() == true) { LoadComponents(); RefreshStats(); }
    }

    private void BtnDeleteComponent_Click(object s, RoutedEventArgs e)
    {
        if (GridComponents.SelectedItem is not Models.Component comp) return;
        if (MessageBox.Show(TranslationService.Get("ConfirmDeleteComponent", comp.Sku, comp.Description), TranslationService.Get("Confirmation"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _db.DeleteComponent(comp.Sku);
        LoadComponents(); RefreshStats();
        SetStatus(TranslationService.Get("StatusComponentDeleted", comp.Sku));
    }

    private void BtnImportBase_Click(object s, RoutedEventArgs e)
    {        var dlg = new OpenFileDialog { Filter = TranslationService.Get("ImportFileFilter"), Title = TranslationService.Get("SelectBaseOds") };
        if (dlg.ShowDialog() != true) return;
        SetStatus(TranslationService.Get("StatusImportingBase"));
        SetBusy(true, TranslationService.Get("StatusImportingBase"));
        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            var (components, errors) = ImportService.ImportBaseOds(dlg.FileName);

            if (errors.Count > 0 && components.Count == 0)
            {                MessageBox.Show(string.Join(" ", errors), TranslationService.Get("ImportErrorsTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int imported = 0;
            foreach (var c in components) { _db.SaveComponent(c); imported++; }

            LoadComponents(); RefreshStats();
            string msg = TranslationService.Get("StatusImportDone", imported);            if (errors.Count > 0) msg += TranslationService.Get("ImportWarnings", errors.Count) + string.Join("", errors.Take(20));            MessageBox.Show(msg, TranslationService.Get("ImportDoneTitle"), MessageBoxButton.OK,
 errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            SetStatus(msg);
        }
        finally { Mouse.OverrideCursor = null; SetBusy(false); }
    }

    private void BtnExportXlsx_Click(object s, RoutedEventArgs e)
    {        var dlg = new SaveFileDialog { Filter = TranslationService.Get("ExportXlsxFilter"), FileName = TranslationService.Get("ExportXlsxFilename") };
        if (dlg.ShowDialog() != true) return;
        var forExport = _componentsView?.Cast<Models.Component>().ToList() ?? _allComponents;
        ExportService.ExportComponentsXlsx(forExport, dlg.FileName);        SetStatus(TranslationService.Get("StatusExported", forExport.Count, dlg.FileName));
    }

    private void BtnExportCsv_Click(object s, RoutedEventArgs e)
    {        var dlg = new SaveFileDialog { Filter = TranslationService.Get("ExportCsvFilter"), FileName = TranslationService.Get("ExportCsvFilename") };
        if (dlg.ShowDialog() != true) return;
        var forExport2 = _componentsView?.Cast<Models.Component>().ToList() ?? _allComponents;
        ExportService.ExportComponentsCsv(forExport2, dlg.FileName);        SetStatus(TranslationService.Get("StatusExported", forExport2.Count, dlg.FileName));
    }

    // ── Projects tab ─────────────────────────────────────────────────────────

    private void LoadProjects()
    {
        LstProjects.ItemsSource = _db.GetAllProjects();
    }

    private void LstProjects_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (LstProjects.SelectedItem is Project p)
        {
            PanelProjectHeader.Visibility = Visibility.Visible;
            TxtNoProject.Visibility       = Visibility.Collapsed;
            LoadBom(p);
        }
        else
        {
            PanelProjectHeader.Visibility = Visibility.Collapsed;
            TxtNoProject.Visibility       = Visibility.Visible;
            ClearBom();
        }
    }

    private void LoadBom(Project project)
    {
        TxtProjectName.Text  = project.DisplayName;
        TxtProjectBoard.Text = project.BoardName;
        TxtProjectDesc.Text  = project.Description;

        _currentBom = _db.GetBomLinesWithComponents(project.Id);
        GridBom.ItemsSource = _currentBom;
        UpdateBomSummary();        SetStatus(TranslationService.Get("StatusProjectBom", project.DisplayName, _currentBom.Count));
    }

    private void ClearBom()
    {
        _currentBom = [];
        GridBom.ItemsSource = null;        TxtBomCount.Text = ""; TxtBomCost.Text = ""; TxtBomMass.Text = "";        TxtBomWarning.Text = "";
    }

    private void UpdateBomSummary()
    {
        int    count    = _currentBom.Count;
        double cost     = _currentBom.Sum(l => l.LineCost);
        double mass     = _currentBom.Sum(l => (l.Component?.MassMg ?? 0) * l.Qty);
        int    missing  = _currentBom.Count(l => l.Status == StockStatus.Out);
        int    low      = _currentBom.Count(l => l.Status == StockStatus.Low);
        TxtBomCount.Text   = TranslationService.Get("BomLines", count);        TxtBomCost.Text    = TranslationService.Get("BomCostFormat", cost);        TxtBomMass.Text    = TranslationService.Get("BomMassFormat", mass / 1000);
        TxtBomWarning.Text = missing > 0 ? TranslationService.Get("BomSummaryWarn", missing, low)                           : low    > 0 ? TranslationService.Get("BomSummaryWarnLow", low)                           : "";
    }

    private void BtnNewProject_Click(object s, RoutedEventArgs e)
    {
        var dlg = new ProjectEditDialog(null);
        if (dlg.ShowDialog() != true) return;
        int id = _db.SaveProject(dlg.Result!);
        LoadProjects();
        RefreshStats();
        // Select newly created project
        LstProjects.SelectedItem = (LstProjects.ItemsSource as List<Project>)?.FirstOrDefault(p => p.Id == id);        SetStatus(TranslationService.Get("StatusProjectCreated", dlg.Result!.Name));
    }

    private void BtnEditProject_Click(object s, RoutedEventArgs e)
    {        if (LstProjects.SelectedItem is not Project p) { SetStatus(TranslationService.Get("StatusSelectProject")); return; }
        var dlg = new ProjectEditDialog(p);
        if (dlg.ShowDialog() != true) return;
        _db.SaveProject(dlg.Result!);
        LoadProjects();        SetStatus(TranslationService.Get("StatusProjectUpdated", dlg.Result!.Name));
    }

    private void BtnDeleteProject_Click(object s, RoutedEventArgs e)
    {
        if (LstProjects.SelectedItem is not Project p) return;        if (            MessageBox.Show(TranslationService.Get("ConfirmDeleteProject", p.DisplayName), TranslationService.Get("Confirmation"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
 _db.DeleteProject(p.Id);
        LoadProjects(); RefreshStats(); ClearBom();        SetStatus(TranslationService.Get("StatusProjectDeleted", p.DisplayName));
    }

    private void BtnDeleteBomLine_Click(object s, RoutedEventArgs e)
    {
        if (LstProjects.SelectedItem is not Project p) { SetStatus(TranslationService.Get("StatusSelectProject")); return; }
        if (GridBom.SelectedItem is not BomLine line) { SetStatus(TranslationService.Get("StatusSelectBomLine")); return; }
        if (MessageBox.Show(TranslationService.Get("ConfirmDeleteBomLine", line.Sku, line.Qty),
            TranslationService.Get("Confirmation"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        _db.DeleteBomLine(line.Id);
        LoadBom(p);
        SetStatus(TranslationService.Get("StatusBomLineDeleted", line.Sku));
    }

    private void BtnImportCsv_Click(object s, RoutedEventArgs e)
    {        if (LstProjects.SelectedItem is not Project p) { SetStatus(TranslationService.Get("StatusSelectProject")); return; }
        var dlg = new OpenFileDialog { Filter = TranslationService.Get("ImportCsvFilter"), Title = TranslationService.Get("SelectEagleBomCsv") };
        if (dlg.ShowDialog() != true) return;

        SetBusy(true, TranslationService.Get("ImportCsvRunning"));
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
        var (lines, errors) = ImportService.ImportEagleCsv(dlg.FileName);
        if (lines.Count == 0)
        {            MessageBox.Show(TranslationService.Get("ErrorNoValidBomLines") + string.Join(" ", errors), TranslationService.Get("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Check which SKUs are missing from library
        var missing = lines
            .Where(l => !string.IsNullOrEmpty(l.Sku) && !_db.ComponentExists(l.Sku))
            .Select(l => l.Sku).Distinct().ToList();
        var missing2 = lines
            .Where(l => !string.IsNullOrEmpty(l.Sku2) && !_db.ComponentExists(l.Sku2))
            .Select(l => l.Sku2).Distinct().ToList();
        string warnMsg = "";
        if (missing.Count > 0)
            warnMsg += TranslationService.Get("WarningSkuNotInLibrary") + string.Join(", ", missing) + " | ";
        if (missing2.Count > 0)
            warnMsg += TranslationService.Get("WarningSku2NotInLibrary") + string.Join(", ", missing2) + " | ";
        if (!string.IsNullOrEmpty(warnMsg))
            MessageBox.Show(warnMsg + TranslationService.Get("WarningLinesWillBeImported"), TranslationService.Get("WarningTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);

        _db.SaveBomLines(p.Id, lines);
        LoadBom(p);

        string msg = TranslationService.Get("StatusBomImported", lines.Count);
        if (errors.Count > 0) msg += TranslationService.Get("StatusBomImportErrors", errors.Count);
        SetStatus(msg + ".");
        }
        finally { Mouse.OverrideCursor = null; SetBusy(false); }
    }

    private void BtnImportProjectOds_Click(object s, RoutedEventArgs e)
    {
        if (LstProjects.SelectedItem is not Models.Project p)
        { SetStatus(TranslationService.Get("StatusSelectProject")); return; }

        var dlg = new OpenFileDialog
        {
            Filter = TranslationService.Get("BomOdsFilter"),
            Title  = TranslationService.Get("SelectProjectOds")
        };
        if (dlg.ShowDialog() != true) return;

        var (importedLines, importErrors) = Services.ImportService.ImportProjectOds(dlg.FileName);

        if (importedLines.Count == 0)
        {
            MessageBox.Show(TranslationService.Get("ErrorNoValidBomLinesCheck"),
 TranslationService.Get("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var missing = importedLines
            .Where(l => !string.IsNullOrEmpty(l.Sku) && !_db.ComponentExists(l.Sku))
            .Select(l => l.Sku).Distinct().ToList();
        if (missing.Count > 0)
            MessageBox.Show(
 TranslationService.Get("WarningMissingSkusInLibrary") + string.Join(", ", missing) + " " +
 TranslationService.Get("WarningLinesWillBeImportedStockZero"),
 TranslationService.Get("WarningTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);

        _db.SaveBomLines(p.Id, importedLines);
        LoadBom(p);

        string msg = TranslationService.Get("StatusBomImportedFromOds", importedLines.Count);
        if (importErrors.Count > 0) msg += TranslationService.Get("StatusBomImportErrors", importErrors.Count);
        SetStatus(msg + ".");
    }

    private void BtnStockCheck_Click(object s, RoutedEventArgs e)
    {
        if (LstProjects.SelectedItem is not Project p) return;
        var win = new StockCheckWindow(p, _currentBom, _db);
        win.Owner = this;
        win.ShowDialog();
    }

    private void BtnBuild_Click(object s, RoutedEventArgs e)
    {        if (LstProjects.SelectedItem is not Project p) { SetStatus(TranslationService.Get("StatusSelectProject")); return; }        if (_currentBom.Count == 0) { SetStatus(TranslationService.Get("StatusBomEmpty")); return; }

        // Ask how many boards to build
        var numDlg = new InputDialog(TranslationService.Get("BuildTitle"), TranslationService.Get("BuildPrompt"), "1");

        if (numDlg.ShowDialog() != true) return;
        if (!int.TryParse(numDlg.InputValue, out int boards) || boards <= 0)
        {            MessageBox.Show(TranslationService.Get("BuildInvalid"), TranslationService.Get("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show( TranslationService.Get("BuildConfirm", boards, p.DisplayName), TranslationService.Get("BuildConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {            string tag = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {p.DisplayName}";
            var noteDlg = new InputDialog(TranslationService.Get("BuildNoteTitle"), TranslationService.Get("BuildNote"), tag);            string notes = noteDlg.ShowDialog() == true ? noteDlg.InputValue : "";

            var warnings = _db.BuildProject(p, boards, notes);
            LoadBom(p);       // refresh BOM with updated stock
            LoadComponents(); // refresh library tab too
            RefreshStats();
            LoadTransactions();
            string msg = TranslationService.Get("BuildDone", boards, p.DisplayName);
            if (warnings.Count > 0)
                msg += Environment.NewLine + Environment.NewLine + TranslationService.Get("BuildWarnings") + Environment.NewLine + string.Join(Environment.NewLine, warnings);
            MessageBox.Show(msg, TranslationService.Get("BuildDoneTitle"), MessageBoxButton.OK,
 warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            SetStatus(TranslationService.Get("BuildDone", boards, p.DisplayName));
        }
        finally { Mouse.OverrideCursor = null; SetBusy(false); }
    }

    private void BtnExportBom_Click(object s, RoutedEventArgs e)
    {
        if (LstProjects.SelectedItem is not Project p) return;
        if (_currentBom.Count == 0) { SetStatus(TranslationService.Get("StatusBomEmpty")); return; }

        var dlg = new SaveFileDialog
        {            Filter   = TranslationService.Get("ExportBomFilter"),            FileName = $"BOM_{p.Name}_{p.Revision}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;
        ExportService.ExportBomXlsx(p, _currentBom, dlg.FileName);        SetStatus(TranslationService.Get("StatusBomExported", dlg.FileName));
    }

    private void GridBom_DoubleClick(object s, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GridBom.SelectedItem is not Models.BomLine line) return;
        if (string.IsNullOrEmpty(line.Sku)) return;

        var comp = _db.GetComponent(line.Sku);
        if (comp == null)
        {            MessageBox.Show(TranslationService.Get("ComponentNotFound", line.Sku), TranslationService.Get("ComponentNotFoundTitle"),
 MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new ComponentEditDialog(comp, _db);
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            LoadBom(LstProjects.SelectedItem as Models.Project ?? new Models.Project());
            LoadComponents();
            RefreshStats();
        }
    }

    // ── Transactions tab ─────────────────────────────────────────────────────

    private void LoadTransactions(string? filter = null)
    {
        GridTx.ItemsSource = _db.GetTransactionGroups(
            string.IsNullOrWhiteSpace(filter) ? null : filter);
    }

    private void TxtTxFilter_TextChanged(object s, TextChangedEventArgs e)
        => LoadTransactions(TxtTxFilter.Text.Trim());

    private void BtnRefreshTx_Click(object s, RoutedEventArgs e)
        => LoadTransactions(TxtTxFilter.Text.Trim());

    private void BtnAddTx_Click(object s, RoutedEventArgs e)
    {
        var dlg = new TransactionDialog(_db);
        if (dlg.ShowDialog() != true) return;
        LoadTransactions(); LoadComponents(); RefreshStats();        SetStatus(TranslationService.Get("StatusTransactionAdded"));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void BtnLocations_Click(object s, RoutedEventArgs e)
    {
        var dlg = new LocationEditDialog(_db);
        dlg.Owner = this;
        dlg.ShowDialog();
        // Refresh components so location codes are up-to-date
        LoadComponents();
    }

    private void BtnSettings_Click(object s, RoutedEventArgs e)
    {
        var settings = MyHobbyWarehouse.Services.SettingsService.Load();
        string prevLang = settings.Language;
        var dlg = new DbLocationDialog(settings.DatabasePath, settings.Language);
        dlg.Owner = this;
        if (dlg.ShowDialog() != true) return;

        settings.DatabasePath = dlg.SelectedPath;
        settings.Language = dlg.SelectedLanguage;
        MyHobbyWarehouse.Services.SettingsService.Save(settings);

        if (dlg.ComponentsImported)
        {
            LoadComponents();
           
            RefreshStats();            SetStatus(TranslationService.Get("StatusComponentsImportedFromBase"));
        }
        else if (dlg.SelectedLanguage != prevLang)
        {
            string langName = TranslationService.GetLanguageName(dlg.SelectedLanguage);
            MessageBox.Show(TranslationService.Get("SettingsSavedLangChanged", langName), TranslationService.Get("SettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(TranslationService.Get("SettingsSaved", settings.DatabasePath), TranslationService.Get("SettingsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ── Ctrl+F search ────────────────────────────────────────────────────────

    private List<Models.Component> _searchMatches = [];
    private int _searchIndex = -1;

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == System.Windows.Input.Key.F
            && e.KeyboardDevice.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            int tab = MainTab.SelectedIndex;
            if (tab == 0) { SearchBar.Visibility = Visibility.Visible; TxtSearch.Focus(); TxtSearch.SelectAll(); }
            else if (tab == 1) { BomSearchBar.Visibility = Visibility.Visible; TxtBomSearch.Focus(); TxtBomSearch.SelectAll(); }
            else if (tab == 2) { TxSearchBar.Visibility = Visibility.Visible; TxtTxSearch.Focus(); TxtTxSearch.SelectAll(); }
            e.Handled = true;
        }
        if (e.Key == System.Windows.Input.Key.F3)
        {
            int tab = MainTab.SelectedIndex;
            if (tab == 0)
            {
                if (SearchBar.Visibility != Visibility.Visible)
                { SearchBar.Visibility = Visibility.Visible; TxtSearch.Focus(); }
                else SearchNext();
            }
            else if (tab == 1)
            {
                if (BomSearchBar.Visibility != Visibility.Visible)
                { BomSearchBar.Visibility = Visibility.Visible; TxtBomSearch.Focus(); }
                else BomSearchNext();
            }
            else if (tab == 2)
            {
                if (TxSearchBar.Visibility != Visibility.Visible)
                { TxSearchBar.Visibility = Visibility.Visible; TxtTxSearch.Focus(); }
                else TxSearchNext();
            }
            e.Handled = true;
        }
    }

    private void TxtSearch_KeyDown(object s, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.F3)
        { SearchNext(); e.Handled = true; }
        if (e.Key == System.Windows.Input.Key.Escape)
        { CloseSearch(); e.Handled = true; }
    }

    private void TxtSearch_TextChanged(object s, TextChangedEventArgs e)
    {
        string q = TxtSearch.Text.Trim();        if (string.IsNullOrEmpty(q)) { _searchMatches.Clear(); _searchIndex = -1; TxtSearchCount.Text = ""; return; }

        _searchMatches = _allComponents.Where(c =>
            c.Sku.Contains(q, StringComparison.OrdinalIgnoreCase)         ||
            c.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            c.Category1.Contains(q, StringComparison.OrdinalIgnoreCase)   ||
            c.Category3.Contains(q, StringComparison.OrdinalIgnoreCase)   ||
            c.Category4.Contains(q, StringComparison.OrdinalIgnoreCase)   ||
            c.ManufacturerPart.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            c.LastSupplier.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            c.OldSku.Contains(q, StringComparison.OrdinalIgnoreCase)
        ).ToList();

        _searchIndex = -1;
        TxtSearchCount.Text = _searchMatches.Count == 0            ? TranslationService.Get("NoMatches")            : TranslationService.Get("SearchResults", 0, _searchMatches.Count);
        if (_searchMatches.Count > 0) SearchNext();
    }

    private void SearchNext()
    {
        if (_searchMatches.Count == 0) return;
        _searchIndex = (_searchIndex + 1) % _searchMatches.Count;
        ScrollToMatch();
    }

    private void BtnSearchNext_Click(object s, RoutedEventArgs e) => SearchNext();
    private void BtnSearchPrev_Click(object s, RoutedEventArgs e)
    {
        if (_searchMatches.Count == 0) return;
        _searchIndex = (_searchIndex - 1 + _searchMatches.Count) % _searchMatches.Count;
        ScrollToMatch();
    }

    private void BtnSearchClose_Click(object s, RoutedEventArgs e) => CloseSearch();

    private void CloseSearch()
    {
        SearchBar.Visibility = Visibility.Collapsed;        TxtSearch.Text = "";
        _searchMatches.Clear();
        _searchIndex = -1;        TxtSearchCount.Text = "";
        GridComponents.Focus();
    }

    private void ScrollToMatch()
    {
        if (_searchIndex < 0 || _searchIndex >= _searchMatches.Count) return;
        var comp = _searchMatches[_searchIndex];        TxtSearchCount.Text = $"{_searchIndex + 1} / {_searchMatches.Count}";
        // Select and scroll to matching row
        GridComponents.SelectedItem = comp;
        GridComponents.ScrollIntoView(comp);
    }


    // ── Backup ───────────────────────────────────────────────────────────────

    private void BtnBackup_Click(object s, RoutedEventArgs e)
    {
        try
        {
            string path = _db.Backup();
            SetStatus(TranslationService.Get("BackupDone") + path);
            var backups = _db.GetBackups();
            string msg = TranslationService.Get("BackupSuccess") + Environment.NewLine + path + Environment.NewLine + Environment.NewLine +
                         TranslationService.Get("BackupTotal", backups.Count);
            MessageBox.Show(msg, TranslationService.Get("BackupTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(TranslationService.Get("BackupError") + ex.Message, TranslationService.Get("ErrorTitle"),
 MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Redo / Reverse transaction ────────────────────────────────────────────

    // ── Transaction group handlers ────────────────────────────────────────────

    private void GridTx_DoubleClick(object s, System.Windows.Input.MouseButtonEventArgs e)
        => OpenTxDetail();

    private void MnuOpenTxDetail_Click(object s, RoutedEventArgs e) => OpenTxDetail();

    private void OpenTxDetail()
    {
        if (GridTx.SelectedItem is not Models.TransactionGroup grp) return;
        var win = new TransactionDetailWindow(grp, _db);
        win.Owner = this;
        win.ShowDialog();
        if (win.StockChanged)
        {
            SetBusy(true, TranslationService.Get("StatusRefreshing"));
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                LoadTransactions(TxtTxFilter.Text.Trim());
                LoadComponents();
                RefreshStats();
                if (LstProjects.SelectedItem is Models.Project p) LoadBom(p);
                SetStatus(TranslationService.Get("StatusStockReverted"));
            }
            finally { Mouse.OverrideCursor = null; SetBusy(false); }
        }
    }


    private void MnuDeleteTxGroup_Click(object s, RoutedEventArgs e)
    {
        if (GridTx.SelectedItem is not Models.TransactionGroup grp) return;
        if (MessageBox.Show(
            TranslationService.Get("ConfirmDeleteTxGroup", grp.Tag, grp.TransactionCount),
            TranslationService.Get("DeleteTxGroupTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;
        _db.DeleteTransactionGroup(grp.Tag);
        LoadTransactions(TxtTxFilter.Text.Trim());
        SetStatus(TranslationService.Get("StatusTxGroupDeleted", grp.Tag));
    }

    private void MnuReverseTxGroup_Click(object s, RoutedEventArgs e)
    {
        if (GridTx.SelectedItem is not Models.TransactionGroup grp) return;
        if (MessageBox.Show(
            TranslationService.Get("ConfirmReverseTxGroup", grp.TransactionCount),
            TranslationService.Get("ReverseTxGroupTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;
        SetBusy(true, TranslationService.Get("StatusReversing"));
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            int count = _db.ReverseTransactionGroup(grp.Tag);
            LoadTransactions(TxtTxFilter.Text.Trim());
            LoadComponents();
            RefreshStats();
            if (LstProjects.SelectedItem is Models.Project p) LoadBom(p);
            SetStatus(TranslationService.Get("StatusTxGroupReverted", count));
        }
        finally { Mouse.OverrideCursor = null; SetBusy(false); }
    }



    // ── Image hover popup ────────────────────────────────────────────────────

    private string? _lastHoverSku;

    private void Grid_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        // Find row under mouse
        var pos = e.GetPosition(grid);
        var hit = System.Windows.Media.VisualTreeHelper.HitTest(grid, pos);
        if (hit == null) { HideImagePopup(); return; }

        // Walk visual tree up to DataGridRow
        var dep = hit.VisualHit as System.Windows.DependencyObject;
        while (dep != null && dep is not DataGridRow)
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);

        if (dep is not DataGridRow row) { HideImagePopup(); return; }

        // Get SKU from row data context
        string? sku = row.DataContext switch
        {
            Models.Component c  => c.Sku,
            Models.BomLine    b => b.Sku,
            _                   => null
        };

        if (string.IsNullOrEmpty(sku)) { HideImagePopup(); return; }
        if (sku == _lastHoverSku)      return; // already showing

        string? imgPath = Services.ImageService.FindImage(sku);
        if (imgPath == null) { HideImagePopup(); return; }

        _lastHoverSku      = sku;
        var bmp            = Services.ImageService.LoadBitmap(imgPath);
        PopupImage.Source  = bmp;
        PopupSku.Text      = sku;
        ImagePopup.IsOpen  = true;
    }

    private void Grid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        => HideImagePopup();

    private void HideImagePopup()
    {
        if (ImagePopup.IsOpen)
        {
            ImagePopup.IsOpen = false;
            _lastHoverSku     = null;
            PopupImage.Source = null;
        }
    }


    // ── BOM Ctrl+F search ────────────────────────────────────────────────────

    private List<Models.BomLine> _bomMatches = [];
    private int _bomSearchIdx = -1;

    private void TxtBomSearch_KeyDown(object s, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.F3)
        { BomSearchNext(); e.Handled = true; }
        if (e.Key == System.Windows.Input.Key.Escape)
        { CloseBomSearch(); e.Handled = true; }
    }

    private void TxtBomSearch_TextChanged(object s, TextChangedEventArgs e)
    {
        string q = TxtBomSearch.Text.Trim();
        if (string.IsNullOrEmpty(q)) { _bomMatches.Clear(); _bomSearchIdx = -1; TxtBomSearchCount.Text = ""; return; }
        _bomMatches = _currentBom.Where(l =>
            l.Sku.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            l.DisplayDescription.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            l.PartDesignators.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            l.Package.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        _bomSearchIdx = -1;
        TxtBomSearchCount.Text = _bomMatches.Count == 0 ? TranslationService.Get("NoMatches") : TranslationService.Get("SearchResults", 0, _bomMatches.Count);
        if (_bomMatches.Count > 0) BomSearchNext();
    }

    private void BomSearchNext()
    {
        if (_bomMatches.Count == 0) return;
        _bomSearchIdx = (_bomSearchIdx + 1) % _bomMatches.Count;
        TxtBomSearchCount.Text = $"{_bomSearchIdx + 1} / {_bomMatches.Count}";
        GridBom.SelectedItem = _bomMatches[_bomSearchIdx];
        GridBom.ScrollIntoView(_bomMatches[_bomSearchIdx]);
    }

    private void BtnBomSearchNext_Click(object s, RoutedEventArgs e) => BomSearchNext();

    private void BtnBomSearchPrev_Click(object s, RoutedEventArgs e)
    {
        if (_bomMatches.Count == 0) return;
        _bomSearchIdx = (_bomSearchIdx - 1 + _bomMatches.Count) % _bomMatches.Count;
        TxtBomSearchCount.Text = $"{_bomSearchIdx + 1} / {_bomMatches.Count}";
        GridBom.SelectedItem = _bomMatches[_bomSearchIdx];
        GridBom.ScrollIntoView(_bomMatches[_bomSearchIdx]);
    }

    private void BtnBomSearchClose_Click(object s, RoutedEventArgs e) => CloseBomSearch();

    private void CloseBomSearch()
    {
        BomSearchBar.Visibility = Visibility.Collapsed;
        TxtBomSearch.Text = "";
        _bomMatches.Clear();
        _bomSearchIdx = -1;
        GridBom.Focus();
    }

    // ── Transactions Ctrl+F search ───────────────────────────────────────────

    private List<Models.TransactionGroup> _txMatches = [];
    private int _txSearchIdx = -1;

    private void TxtTxSearch_KeyDown(object s, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.F3)
        { TxSearchNext(); e.Handled = true; }
        if (e.Key == System.Windows.Input.Key.Escape)
        { CloseTxSearch(); e.Handled = true; }
    }

    private void TxtTxSearch_TextChanged(object s, TextChangedEventArgs e)
    {
        string q = TxtTxSearch.Text.Trim();
        var all = (GridTx.ItemsSource as List<Models.TransactionGroup>) ?? [];
        if (string.IsNullOrEmpty(q)) { _txMatches.Clear(); _txSearchIdx = -1; TxtTxSearchCount.Text = ""; return; }
        _txMatches = all.Where(g =>
            g.Tag.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            g.ProjectName.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        _txSearchIdx = -1;
        TxtTxSearchCount.Text = _txMatches.Count == 0 ? TranslationService.Get("NoMatches") : TranslationService.Get("SearchResults", 0, _txMatches.Count);
        if (_txMatches.Count > 0) TxSearchNext();
    }

    private void TxSearchNext()
    {
        if (_txMatches.Count == 0) return;
        _txSearchIdx = (_txSearchIdx + 1) % _txMatches.Count;
        TxtTxSearchCount.Text = $"{_txSearchIdx + 1} / {_txMatches.Count}";
        GridTx.SelectedItem = _txMatches[_txSearchIdx];
        GridTx.ScrollIntoView(_txMatches[_txSearchIdx]);
    }

    private void BtnTxSearchNext_Click(object s, RoutedEventArgs e) => TxSearchNext();

    private void BtnTxSearchPrev_Click(object s, RoutedEventArgs e)
    {
        if (_txMatches.Count == 0) return;
        _txSearchIdx = (_txSearchIdx - 1 + _txMatches.Count) % _txMatches.Count;
        TxtTxSearchCount.Text = $"{_txSearchIdx + 1} / {_txMatches.Count}";
        GridTx.SelectedItem = _txMatches[_txSearchIdx];
        GridTx.ScrollIntoView(_txMatches[_txSearchIdx]);
    }

    private void BtnTxSearchClose_Click(object s, RoutedEventArgs e) => CloseTxSearch();

    private void CloseTxSearch()
    {
        TxSearchBar.Visibility = Visibility.Collapsed;
        TxtTxSearch.Text = "";
        _txMatches.Clear();
        _txSearchIdx = -1;
        GridTx.Focus();
    }

    // ── Busy indicator ────────────────────────────────────────────────────────

    private void SetBusy(bool busy, string msg = "")
    {
        if (BusyOverlay == null) return;
        BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (BusyText != null) BusyText.Text = msg;
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            System.Windows.Threading.DispatcherPriority.Render, () => { });
    }

    private void SetStatus(string msg)
    {
        if (TxtStatus == null) return;        TxtStatus.Text = $"[{DateTime.Now:HH:mm:ss}]  {msg}";
    }
}
