using System.Windows.Data;
using ICollectionView = System.ComponentModel.ICollectionView;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using EagleManager.Data;
using EagleManager.Models;
using EagleManager.Services;

namespace EagleManager.Views;

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
        Loaded += (_, _) => Initialize();
    }

    private void Initialize()
    {
       
        LoadComponents();
        LoadProjects();
        LoadTransactions();
        RefreshStats();
        SetStatus("Pripravljeno.");
    }

    // ── Stats ────────────────────────────────────────────────────────────────

    private void RefreshStats()
    {
        var (val, types, _, projects) = _db.GetStats();
        TxtStatComponents.Text = types.ToString();        TxtStatValue.Text      = $"{val:F2} €";
        TxtStatProjects.Text   = projects.ToString();
    }

    // ── Library tab ──────────────────────────────────────────────────────────

    private void LoadComponents()
    {
        _allComponents  = _db.GetAllComponents();
        _componentsView = CollectionViewSource.GetDefaultView(_allComponents);
        _componentsView.Filter = FilterPredicate;
        GridComponents.ItemsSource = _componentsView;
        RefreshFilterStatus();
        UpdateFilterButtonStyles();
    }

    // ── Column filter logic ──────────────────────────────────────────────────

    /// <summary>Returns the filter-display value for a component given a column key.</summary>
    private static string GetColValue(Models.Component c, string colKey) => colKey switch
    {        "Category1"     => c.Category1,        "Category3"     => c.Category3,        "Category4"     => c.Category4,        "DisplaySmd"    => c.DisplaySmd,        "StockBucket"   => c.StockSum > 0 ? "Na zalogi" : "Brez zaloge",        "DisplayPrice"  => c.DisplayPrice,        "DisplayLocation" => !string.IsNullOrEmpty(c.DisplayLocation) ? c.DisplayLocation : "(brez)",        "LastSupplier"  => !string.IsNullOrEmpty(c.LastSupplier) ? c.LastSupplier : "(brez)",        _ => ""
    };

    private List<string> GetUniqueValues(string colKey)
    {        if (colKey == "StockBucket")  return ["Na zalogi", "Brez zaloge"];        if (colKey == "DisplaySmd")   return ["SMD", "TH"];
        return _allComponents
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
    {        "Category1"     => "Tip",        "Category3"     => "Vrednost",        "Category4"     => "Ohišje / Package",        "DisplaySmd"    => "SMD / TH",        "StockBucket"   => "Zaloga",        "DisplayPrice"  => "Cena",        "DisplayLocation" => "Lokacija (rack)",        "LastSupplier"  => "Dobavitelj",
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
        SetStatus(shown == total            ? $"{total} komponent."            : $"Filtrirano: {shown} / {total}  ({active} aktivnih filtrov)");
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

    private void ShowComponentDetail(Models.Component c)
    {
        PanelComponentDetail.Visibility = Visibility.Visible;        TxtDetailSku.Text   = $"SKU: {c.Sku}  |  Stara SKU: {c.OldSku}  |  Alt: {c.Alt}";
        TxtDetailDesc.Text  = c.Description;        TxtDetailStock.Text = $"Zaloga: {c.StockSum:F0} {c.Unit}  (R{c.StockRack}-P{c.StockPackage})";        TxtDetailPrice.Text = $"Cena: {c.LastPrice:F4} €  |  Vrednost: {c.StockValue:F2} €  |  {c.MassMg:F0} mg";
        TxtDetailSupp1.Text = !string.IsNullOrEmpty(c.Supplier1Name)            ? $"S1: {c.Supplier1Name}  {c.Supplier1Sku}  {c.Supplier1Price:F4} €" +              (!string.IsNullOrEmpty(c.Supplier1Url) ? $"  🔗 {c.Supplier1Url}" : "") : "";
        TxtDetailSupp2.Text = !string.IsNullOrEmpty(c.Supplier2Name)            ? $"S2: {c.Supplier2Name}  {c.Supplier2Sku}  {c.Supplier2Price:F4} €" +              (!string.IsNullOrEmpty(c.Supplier2Url) ? $"  🔗 {c.Supplier2Url}" : "") : "";
        TxtDetailSupp3.Text = !string.IsNullOrEmpty(c.Supplier3Name)            ? $"S3: {c.Supplier3Name}  {c.Supplier3Sku}  {c.Supplier3Price:F4} €" +              (!string.IsNullOrEmpty(c.Supplier3Url) ? $"  🔗 {c.Supplier3Url}" : "") : "";
        TxtDetailMfg.Text   = !string.IsNullOrEmpty(c.ManufacturerName)            ? $"MFG: {c.ManufacturerName}  {c.ManufacturerPart}" : "";
        TxtDetailOldSku.Text = !string.IsNullOrEmpty(c.StickerText)            ? $"Nalepka: {c.StickerText}" : "";
    }

    private void BtnAddComponent_Click(object s, RoutedEventArgs e)
    {
        var dlg = new ComponentEditDialog(null, _db);
        if (dlg.ShowDialog() == true) { LoadComponents(); RefreshStats(); }
    }

    private List<Models.Component> GetFilteredComponents()
        => _componentsView?.Cast<Models.Component>().ToList() ?? _allComponents;

    private void BtnEditComponent_Click(object s, RoutedEventArgs e)
    {        if (GridComponents.SelectedItem is not Models.Component comp) { SetStatus("Izberi element za urejanje."); return; }
        var filtered = GetFilteredComponents();
        var dlg = filtered.Count < _allComponents.Count
            ? new ComponentEditDialog(comp, _db, filtered)
            : new ComponentEditDialog(comp, _db);
        if (dlg.ShowDialog() == true) { LoadComponents(); RefreshStats(); }
    }

    private void BtnDeleteComponent_Click(object s, RoutedEventArgs e)
    {
        if (GridComponents.SelectedItem is not Models.Component comp) return;        if (MessageBox.Show($"Izbriši {comp.Sku} — {comp.Description}?", "Potrditev",
 MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
 _db.DeleteComponent(comp.Sku);
        LoadComponents(); RefreshStats();        SetStatus($"Element {comp.Sku} izbrisan.");
    }

    private void BtnImportBase_Click(object s, RoutedEventArgs e)
    {        var dlg = new OpenFileDialog { Filter = "ODS datoteke|*.ods|Vse datoteke|*.*", Title = "Izberi base.ods" };
        if (dlg.ShowDialog() != true) return;
        SetStatus("Uvažanje base.ods …");
        SetBusy(true, "Uvažam base.ods...");
        Mouse.OverrideCursor = Cursors.Wait;

        try
        {
            var (components, errors) = ImportService.ImportBaseOds(dlg.FileName);

            if (errors.Count > 0 && components.Count == 0)
            {                MessageBox.Show(string.Join(" ", errors), "Napake pri uvozu", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int imported = 0;
            foreach (var c in components) { _db.SaveComponent(c); imported++; }

            LoadComponents(); RefreshStats();
            string msg = $"Uvoženo {imported} komponent.";            if (errors.Count > 0) msg += $"Opozorila ({errors.Count}):" + string.Join("", errors.Take(20));            MessageBox.Show(msg, "Uvoz dokončan", MessageBoxButton.OK,
 errors.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            SetStatus(msg);
        }
        finally { Mouse.OverrideCursor = null; SetBusy(false); }
    }

    private void BtnExportXlsx_Click(object s, RoutedEventArgs e)
    {        var dlg = new SaveFileDialog { Filter = "Excel datoteke|*.xlsx", FileName = "komponente.xlsx" };
        if (dlg.ShowDialog() != true) return;
        var forExport = _componentsView?.Cast<Models.Component>().ToList() ?? _allComponents;
        ExportService.ExportComponentsXlsx(forExport, dlg.FileName);        SetStatus($"Izvoženo {forExport.Count} komponent → {dlg.FileName}");
    }

    private void BtnExportCsv_Click(object s, RoutedEventArgs e)
    {        var dlg = new SaveFileDialog { Filter = "CSV datoteke|*.csv", FileName = "komponente.csv" };
        if (dlg.ShowDialog() != true) return;
        var forExport2 = _componentsView?.Cast<Models.Component>().ToList() ?? _allComponents;
        ExportService.ExportComponentsCsv(forExport2, dlg.FileName);        SetStatus($"Izvoženo {forExport2.Count} komponent → {dlg.FileName}");
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
        UpdateBomSummary();        SetStatus($"Projekt: {project.DisplayName}  —  {_currentBom.Count} BOM vrstic.");
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
        TxtBomCount.Text   = $"{count} vrstic";        TxtBomCost.Text    = $"{cost:F2} €";        TxtBomMass.Text    = $"{mass / 1000:F2} g";
        TxtBomWarning.Text = missing > 0 ? $"⚠ {missing}× ni na zalogi  {low}× premalo"                           : low    > 0 ? $"⚠ {low}× premalo na zalogi"                           : "";
    }

    private void BtnNewProject_Click(object s, RoutedEventArgs e)
    {
        var dlg = new ProjectEditDialog(null);
        if (dlg.ShowDialog() != true) return;
        int id = _db.SaveProject(dlg.Result!);
        LoadProjects();
        RefreshStats();
        // Select newly created project
        LstProjects.SelectedItem = (LstProjects.ItemsSource as List<Project>)?.FirstOrDefault(p => p.Id == id);        SetStatus($"Projekt '{dlg.Result!.Name}' ustvarjen.");
    }

    private void BtnEditProject_Click(object s, RoutedEventArgs e)
    {        if (LstProjects.SelectedItem is not Project p) { SetStatus("Izberi projekt."); return; }
        var dlg = new ProjectEditDialog(p);
        if (dlg.ShowDialog() != true) return;
        _db.SaveProject(dlg.Result!);
        LoadProjects();        SetStatus($"Projekt '{dlg.Result!.Name}' posodobljen.");
    }

    private void BtnDeleteProject_Click(object s, RoutedEventArgs e)
    {
        if (LstProjects.SelectedItem is not Project p) return;        if (            MessageBox.Show($"Izbriši projekt '{p.DisplayName}'?" + " " + "Izbrisane bodo tudi vse BOM vrstice.", "Potrditev", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
 _db.DeleteProject(p.Id);
        LoadProjects(); RefreshStats(); ClearBom();        SetStatus($"Projekt '{p.DisplayName}' izbrisan.");
    }

    private void BtnImportCsv_Click(object s, RoutedEventArgs e)
    {        if (LstProjects.SelectedItem is not Project p) { SetStatus("Najprej izberi projekt."); return; }
        var dlg = new OpenFileDialog { Filter = "CSV datoteke|*.csv|Vse datoteke|*.*", Title = "Izberi Eagle BOM CSV" };
        if (dlg.ShowDialog() != true) return;

        SetBusy(true, "Uvažam Eagle CSV...");
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
        var (lines, errors) = ImportService.ImportEagleCsv(dlg.FileName);
        if (lines.Count == 0)
        {            MessageBox.Show("Ni veljavnih BOM vrstic. " + string.Join(" ", errors), "Napaka", MessageBoxButton.OK, MessageBoxImage.Error);
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
            warnMsg += "SKU-ji niso v knjiznici: " + string.Join(", ", missing) + " | ";
        if (missing2.Count > 0)
            warnMsg += "SKU2-ji niso v knjiznici: " + string.Join(", ", missing2) + " | ";
        if (!string.IsNullOrEmpty(warnMsg))
            MessageBox.Show(warnMsg + "Vrstice bodo uvozene, zaloge za manjkajoce SKU-je bodo 0.", "Opozorilo", MessageBoxButton.OK, MessageBoxImage.Warning);

        _db.SaveBomLines(p.Id, lines);
        LoadBom(p);

        string msg = $"Uvozeno {lines.Count} BOM vrstic";
        if (errors.Count > 0) msg += $" ({errors.Count} napak)";
        SetStatus(msg + ".");
        }
        finally { Mouse.OverrideCursor = null; SetBusy(false); }
    }

    private void BtnImportProjectOds_Click(object s, RoutedEventArgs e)
    {
        if (LstProjects.SelectedItem is not Models.Project p)
        { SetStatus("Najprej izberi projekt."); return; }

        var dlg = new OpenFileDialog
        {
            Filter = "ODS datoteke|*.ods|Vse datoteke|*.*",
            Title  = "Izberi projektni ODS (BOM)"
        };
        if (dlg.ShowDialog() != true) return;

        var (importedLines, importErrors) = Services.ImportService.ImportProjectOds(dlg.FileName);

        if (importedLines.Count == 0)
        {
            MessageBox.Show("Ni veljavnih BOM vrstic. Preverite datoteko.",
 "Napaka", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var missing = importedLines
            .Where(l => !string.IsNullOrEmpty(l.Sku) && !_db.ComponentExists(l.Sku))
            .Select(l => l.Sku).Distinct().ToList();
        if (missing.Count > 0)
            MessageBox.Show(
 "Naslednji SKU-ji niso v knjižnici:" + " " + string.Join(", ", missing) + " " +
 "Vrstice bodo uvožene, zaloge bodo 0.",
 "Opozorilo", MessageBoxButton.OK, MessageBoxImage.Warning);

        _db.SaveBomLines(p.Id, importedLines);
        LoadBom(p);

        string msg = $"Uvoženo {importedLines.Count} BOM vrstic iz ODS";
        if (importErrors.Count > 0) msg += $" ({importErrors.Count} napak)";
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
    {        if (LstProjects.SelectedItem is not Project p) { SetStatus("Izberi projekt."); return; }        if (_currentBom.Count == 0) { SetStatus("BOM je prazen — uvozi Eagle CSV najprej."); return; }

        // Ask how many boards to build
        var numDlg = new InputDialog("Gradnja PCB", "Koliko PCB-jev bos izdelal?", "1");

        if (numDlg.ShowDialog() != true) return;
        if (!int.TryParse(numDlg.InputValue, out int boards) || boards <= 0)
        {            MessageBox.Show("Neveljava količina.", "Napaka", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var confirm = MessageBox.Show( $"Odšteti zalogo za {boards}× '{p.DisplayName}'? Ta akcija je nepovratna.", "Potrditev gradnje", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        Mouse.OverrideCursor = Cursors.Wait;
        try
        {            string tag = $"[{DateTime.Now:yyyy-MM-dd HH:mm}] {p.DisplayName}";
            var noteDlg = new InputDialog("Opomba", "Opomba / Znacka:", tag);            string notes = noteDlg.ShowDialog() == true ? noteDlg.InputValue : "";

            var warnings = _db.BuildProject(p, boards, notes);
            LoadBom(p);       // refresh BOM with updated stock
            LoadComponents(); // refresh library tab too
            RefreshStats();
            LoadTransactions();
            string msg = $"Zgrajeno {boards}x {p.DisplayName}.";
            if (warnings.Count > 0)
                msg += Environment.NewLine + Environment.NewLine + "Opozorila:" + Environment.NewLine + string.Join(Environment.NewLine, warnings);
            MessageBox.Show(msg, "Gradnja dokončana", MessageBoxButton.OK,
 warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            SetStatus($"Zgrajeno {boards}x {p.DisplayName}.");
        }
        finally { Mouse.OverrideCursor = null; SetBusy(false); }
    }

    private void BtnExportBom_Click(object s, RoutedEventArgs e)
    {
        if (LstProjects.SelectedItem is not Project p) return;
        if (_currentBom.Count == 0) { SetStatus("BOM je prazen."); return; }

        var dlg = new SaveFileDialog
        {            Filter   = "Excel datoteke|*.xlsx",            FileName = $"BOM_{p.Name}_{p.Revision}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;
        ExportService.ExportBomXlsx(p, _currentBom, dlg.FileName);        SetStatus($"BOM izvožen → {dlg.FileName}");
    }

    private void GridBom_DoubleClick(object s, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GridBom.SelectedItem is not Models.BomLine line) return;
        if (string.IsNullOrEmpty(line.Sku)) return;

        var comp = _db.GetComponent(line.Sku);
        if (comp == null)
        {            MessageBox.Show($"Komponenta {line.Sku} ni v knjižnici.", "Ni najdeno",
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
        LoadTransactions(); LoadComponents(); RefreshStats();        SetStatus("Transakcija dodana.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void BtnSettings_Click(object s, RoutedEventArgs e)
    {
        var settings = EagleManager.Services.SettingsService.Load();
        var dlg = new DbLocationDialog(settings.DatabasePath);
        dlg.Owner = this;
        if (dlg.ShowDialog() != true) return;

        settings.DatabasePath = dlg.SelectedPath;
        EagleManager.Services.SettingsService.Save(settings);

        if (dlg.ComponentsImported)
        {
            LoadComponents();
           
            RefreshStats();            SetStatus("Komponente uvožene iz base.ods.");
        }
        else
        {
            MessageBox.Show( $"Nastavitev shranjena. Nova pot: {settings.DatabasePath} Učinkuje ob naslednjem zagonu aplikacije.", "Nastavitve", MessageBoxButton.OK, MessageBoxImage.Information);
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
        TxtSearchCount.Text = _searchMatches.Count == 0            ? "Ni zadetkov"            : $"0 / {_searchMatches.Count}";
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
            SetStatus("Backup shranjen: " + path);
            var backups = _db.GetBackups();
            string msg = "Backup uspesen:" + Environment.NewLine + path + Environment.NewLine + Environment.NewLine +
                         $"Skupaj backupov: {backups.Count}";
            MessageBox.Show(msg, "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Napaka pri backupu: " + ex.Message, "Napaka",
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
            SetBusy(true, "Osvezujem podatke...");
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                LoadTransactions(TxtTxFilter.Text.Trim());
                LoadComponents();
                RefreshStats();
                if (LstProjects.SelectedItem is Models.Project p) LoadBom(p);
                SetStatus("Zaloga posodobljena po razveljavitvi.");
            }
            finally { Mouse.OverrideCursor = null; SetBusy(false); }
        }
    }


    private void MnuDeleteTxGroup_Click(object s, RoutedEventArgs e)
    {
        if (GridTx.SelectedItem is not Models.TransactionGroup grp) return;
        if (MessageBox.Show(
            $"Izbriši skupino '{grp.Tag}' ({grp.TransactionCount} zapisov)? Zaloga se ne spremeni.",
            "Izbriši skupino", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;
        _db.DeleteTransactionGroup(grp.Tag);
        LoadTransactions(TxtTxFilter.Text.Trim());
        SetStatus($"Skupina '{grp.Tag}' izbrisana.");
    }

    private void MnuReverseTxGroup_Click(object s, RoutedEventArgs e)
    {
        if (GridTx.SelectedItem is not Models.TransactionGroup grp) return;
        if (MessageBox.Show(
            $"Razveljavi VSE {grp.TransactionCount} transakcij v skupini?",
            "Razveljavi skupino", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            != MessageBoxResult.Yes) return;
        SetBusy(true, "Razveljavujem transakcije...");
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            int count = _db.ReverseTransactionGroup(grp.Tag);
            LoadTransactions(TxtTxFilter.Text.Trim());
            LoadComponents();
            RefreshStats();
            if (LstProjects.SelectedItem is Models.Project p) LoadBom(p);
            SetStatus($"Razveljavili {count} transakcij.");
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
        TxtBomSearchCount.Text = _bomMatches.Count == 0 ? "Ni zadetkov" : $"0 / {_bomMatches.Count}";
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
        TxtTxSearchCount.Text = _txMatches.Count == 0 ? "Ni zadetkov" : $"0 / {_txMatches.Count}";
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

    private void SetBusy(bool busy, string msg = "Delam...")
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
