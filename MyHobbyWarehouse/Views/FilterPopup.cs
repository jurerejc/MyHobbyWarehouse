using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MyHobbyWarehouse.Views;

/// <summary>
/// Excel-style column filter popup.
/// Shows all unique values for a column with checkboxes.
/// Returns SelectedValues = null if "all" is selected (no filter active).
/// </summary>
public class FilterPopup : Window
{
    public HashSet<string>? SelectedValues { get; private set; }

    private readonly ListBox       _lb;
    private readonly TextBox       _txSearch;
    private readonly CheckBox      _chkAll;
    private readonly List<string>  _allValues;
    private readonly HashSet<string> _initialSelected;
    private bool _suppressEvents;
    private bool _closing;

    public FilterPopup(string columnName, List<string> allValues, HashSet<string>? currentFilter,
                       Point position)
    {
        _allValues      = allValues.Order().ToList();
        _initialSelected = currentFilter != null
            ? new HashSet<string>(currentFilter, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(_allValues, StringComparer.OrdinalIgnoreCase);

        // Window setup
        WindowStyle   = WindowStyle.None;
        ResizeMode    = ResizeMode.NoResize;
        AllowsTransparency = true;
        Topmost       = true;
        ShowInTaskbar = false;
        Width         = 240;
        Height        = 360;
        Left          = position.X;
        Top           = position.Y;

        // ── Layout ───────────────────────────────────────────────────────
        var outer = new Border
        {
            Background      = (System.Windows.Media.Brush)Application.Current.Resources["CardBrush"],
            BorderBrush     = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(4),
            Effect          = new System.Windows.Media.Effects.DropShadowEffect
                              { BlurRadius = 12, Opacity = 0.5, ShadowDepth = 2 }
        };

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // title
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // search
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // select all
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // list
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // buttons

        // Title bar
        var title = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.Resources["PanelBrush"],
            Padding    = new Thickness(10, 6, 10, 6)
        };
        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleTb = new TextBlock
        {
            Text       = $"Filter: {columnName}",
            FontWeight = FontWeights.SemiBold,
            FontSize   = 12,
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        var btnClose = new Button
        {
            Content = "✕", Width = 20, Height = 20,
            Padding = new Thickness(0),
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SubTextBrush"]
        };
        btnClose.Click += (_, _) => { _closing = true; Close(); };
        titleRow.Children.Add(titleTb);
        Grid.SetColumn(btnClose, 1); titleRow.Children.Add(btnClose);
        title.Child = titleRow;
        // Make window draggable
        title.MouseDown += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); };
        Grid.SetRow(title, 0); root.Children.Add(title);

        // Search box
        _txSearch = new TextBox
        {
            Margin      = new Thickness(8, 6, 8, 4),
            Padding     = new Thickness(6, 4, 6, 4),
        };
        _txSearch.TextChanged += (_, _) => RefreshList();
        Grid.SetRow(_txSearch, 1); root.Children.Add(_txSearch);

        // Select all checkbox
        var allRow = new Border
        {
            Padding    = new Thickness(10, 4, 10, 2),
            BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(0, 0, 0, 1)
        };
        _chkAll = new CheckBox
        {
            Content    = "Izberi vse",
            FontWeight = FontWeights.SemiBold,
            IsChecked  = true,
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextBrush"]
        };
        _chkAll.Checked   += ChkAll_Changed;
        _chkAll.Unchecked += ChkAll_Changed;
        allRow.Child = _chkAll;
        Grid.SetRow(allRow, 2); root.Children.Add(allRow);

        // Values list
        _lb = new ListBox
        {
            Margin          = new Thickness(4, 2, 4, 2),
            Background      = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),

        };
        ScrollViewer.SetVerticalScrollBarVisibility(_lb, ScrollBarVisibility.Auto);
        Grid.SetRow(_lb, 3); root.Children.Add(_lb);

        // Bottom buttons
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 6, 8, 8)
        };
        var btnClear = new Button
        {
            Content = "Počisti filter",
            Padding = new Thickness(10, 5, 10, 5),
            Margin  = new Thickness(0, 0, 6, 0)
        };
        var btnOk = new Button
        {
            Content = "OK",
            Padding = new Thickness(18, 5, 18, 5)
        };
        btnOk.Style = (Style)Application.Current.Resources["AccentButton"];
        btnClear.Click += BtnClear_Click;
        btnOk.Click    += BtnOk_Click;
        btnRow.Children.Add(btnClear);
        btnRow.Children.Add(btnOk);
        Grid.SetRow(btnRow, 4); root.Children.Add(btnRow);

        outer.Child = root;
        Content     = outer;

        // Populate list
        BuildList(_allValues);
        UpdateAllCheckbox();

        // Close when focus is lost
        Deactivated += (_, _) => { if (IsLoaded && !_closing) { _closing = true; Close(); } };
        Loaded      += (_, _) => { _txSearch.Focus(); };
    }

    // ── List management ───────────────────────────────────────────────────────

    private void BuildList(IEnumerable<string> values)
    {
        _suppressEvents = true;
        _lb.Items.Clear();
        foreach (var v in values)
        {
            var chk = new CheckBox
            {
                Content   = string.IsNullOrEmpty(v) ? "(prazno)" : v,
                Tag       = v,
                IsChecked = _initialSelected.Contains(v),
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextBrush"],
                Margin    = new Thickness(2, 1, 2, 1)
            };
            chk.Checked   += Item_Changed;
            chk.Unchecked += Item_Changed;
            _lb.Items.Add(chk);
        }
        _suppressEvents = false;
    }

    private void RefreshList()
    {
        string q = _txSearch.Text.Trim();
        var filtered = string.IsNullOrEmpty(q)
            ? _allValues
            : _allValues.Where(v => v.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        BuildList(filtered);
        UpdateAllCheckbox();
    }

    private void UpdateAllCheckbox()
    {
        _suppressEvents = true;
        var items = _lb.Items.Cast<CheckBox>().ToList();
        bool allChecked = items.All(c => c.IsChecked == true);
        bool noneChecked = items.All(c => c.IsChecked != true);
        _chkAll.IsChecked = allChecked ? true : noneChecked ? false : null;
        _suppressEvents = false;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void ChkAll_Changed(object s, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        bool check = _chkAll.IsChecked == true;
        _suppressEvents = true;
        foreach (CheckBox chk in _lb.Items)
            chk.IsChecked = check;
        // Sync to _initialSelected
        _initialSelected.Clear();
        if (check)
            foreach (CheckBox chk in _lb.Items)
                _initialSelected.Add(chk.Tag?.ToString() ?? "");
        _suppressEvents = false;
    }

    private void Item_Changed(object s, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        if (s is CheckBox chk)
        {
            string val = chk.Tag?.ToString() ?? "";
            if (chk.IsChecked == true) _initialSelected.Add(val);
            else                        _initialSelected.Remove(val);
        }
        UpdateAllCheckbox();
    }

    private void BtnClear_Click(object s, RoutedEventArgs e)
    {
        // Clear = select ALL = no filter active
        _suppressEvents = true;
        _initialSelected.Clear();
        foreach (string v in _allValues) _initialSelected.Add(v);
        foreach (CheckBox chk in _lb.Items) chk.IsChecked = true;
        _chkAll.IsChecked = true;
        _suppressEvents = false;
        SelectedValues = null;
        _closing = true; DialogResult = true; Close();
    }

    private void BtnOk_Click(object s, RoutedEventArgs e)
    {
        var checked_ = _lb.Items.Cast<CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => c.Tag?.ToString() ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // If all values selected → no filter
        var allInList = _lb.Items.Cast<CheckBox>().Select(c => c.Tag?.ToString() ?? "").ToHashSet();
        bool allSelected = _allValues.All(v => checked_.Contains(v));

        SelectedValues = allSelected ? null : checked_;
        _closing = true; DialogResult = true; Close();
    }
}
