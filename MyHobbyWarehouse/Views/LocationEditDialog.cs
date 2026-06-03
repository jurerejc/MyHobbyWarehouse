using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MyHobbyWarehouse.Data;
using MyHobbyWarehouse.Models;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Views;

public class LocationEditDialog : Window
{
    private readonly DatabaseService _db;
    private DataGrid _gridLocations = null!;
    private TextBox _txCode = null!, _txDesc = null!;
    private System.Windows.Controls.Image _imgPreview = null!;
    private TextBlock _txImgPath = null!;
    private Button _btnDelete = null!, _btnDeleteImg = null!;
    private int? _editingId;

    public LocationEditDialog(DatabaseService db)
    {
        _db = db;
        Title = TranslationService.Get("LocationManager");
        Width = 600; Height = 520;
        MinWidth = 500; MinHeight = 400;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        BuildUi();
        RefreshList();
    }

    private void BuildUi()
    {
        var outer = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.Resources["BgBrush"],
            Padding = new Thickness(14)
        };
        var mainGrid = new Grid();
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star) });

        // ── Left: location DataGrid ────────────────────────────────────────
        var leftPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        leftPanel.Children.Add(new TextBlock
        {
            Text = TranslationService.Get("Locations"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"]
        });

        _gridLocations = new DataGrid
        {
            IsReadOnly = true,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Margin = new Thickness(0, 0, 0, 4),
            RowHeight = 28,
            AlternatingRowBackground =
                (System.Windows.Media.Brush)Application.Current.Resources["CardBrush"] ?? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1A, 0x27, 0x33)),
            BorderThickness = new Thickness(1),
            SelectionMode = DataGridSelectionMode.Single,
        };
        _gridLocations.SetResourceReference(Control.BackgroundProperty, "BgBrush");
        _gridLocations.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        _gridLocations.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
        _gridLocations.Columns.Add(new DataGridTextColumn
        {
            Header = TranslationService.Get("LocationCode"),
            Binding = new System.Windows.Data.Binding("Code"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });
        _gridLocations.Columns.Add(new DataGridTextColumn
        {
            Header = TranslationService.Get("Description"),
            Binding = new System.Windows.Data.Binding("Description"),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });
        _gridLocations.SelectionChanged += GridLocations_SelectionChanged;
        leftPanel.Children.Add(_gridLocations);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
        var btnNew = new Button { Content = TranslationService.Get("New"), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 4, 0) };
        btnNew.Click += (_, _) => NewLocation();
        _btnDelete = new Button
        {
            Content = TranslationService.Get("Delete"),
            Padding = new Thickness(8, 4, 8, 4),
            IsEnabled = false
        };
        _btnDelete.Style = (Style)Application.Current.Resources["DangerButton"];
        _btnDelete.Click += BtnDelete_Click;
        btnRow.Children.Add(btnNew);
        btnRow.Children.Add(_btnDelete);
        leftPanel.Children.Add(btnRow);
        Grid.SetColumn(leftPanel, 0);
        mainGrid.Children.Add(leftPanel);

        // ── Right: edit panel ──────────────────────────────────────────────
        var rightPanel = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        rightPanel.Children.Add(new TextBlock
        {
            Text = TranslationService.Get("LocationDetails"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"]
        });

        rightPanel.Children.Add(Label(TranslationService.Get("LocationCode")));
        _txCode = new TextBox { Margin = new Thickness(0, 0, 0, 4) };
        rightPanel.Children.Add(_txCode);

        rightPanel.Children.Add(Label(TranslationService.Get("Description")));
        _txDesc = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        rightPanel.Children.Add(_txDesc);

        // Image preview
        var imgBorder = new Border
        {
            Width = 200, Height = 140,
            Background = (System.Windows.Media.Brush)Application.Current.Resources["CardBrush"],
            BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _imgPreview = new System.Windows.Controls.Image
        {
            Stretch = System.Windows.Media.Stretch.Uniform,
            MaxWidth = 196, MaxHeight = 136
        };
        imgBorder.Child = _imgPreview;
        rightPanel.Children.Add(imgBorder);

        _txImgPath = new TextBlock
        {
            Text = TranslationService.Get("NoImage"),
            FontSize = 11, Margin = new Thickness(0, 0, 0, 4)
        };
        _txImgPath.SetResourceReference(ForegroundProperty, "SubTextBrush");
        rightPanel.Children.Add(_txImgPath);

        var imgBtnRow = new StackPanel { Orientation = Orientation.Horizontal };
        var btnImg = new Button { Content = TranslationService.Get("SelectImage"), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 4, 0) };
        btnImg.Click += BtnSelectImage_Click;
        imgBtnRow.Children.Add(btnImg);
        _btnDeleteImg = new Button
        {
            Content = TranslationService.Get("DeleteImage"),
            Padding = new Thickness(8, 4, 8, 4),
            IsEnabled = false
        };
        _btnDeleteImg.Style = (Style)Application.Current.Resources["DangerButton"];
        _btnDeleteImg.Click += BtnDeleteImage_Click;
        imgBtnRow.Children.Add(_btnDeleteImg);
        rightPanel.Children.Add(imgBtnRow);

        // Save
        var btnSave = new Button
        {
            Content = TranslationService.Get("Save"),
            Padding = new Thickness(18, 7, 18, 7),
            Margin = new Thickness(0, 10, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        btnSave.Style = (Style)Application.Current.Resources["AccentButton"];
        btnSave.Click += BtnSave_Click;
        rightPanel.Children.Add(btnSave);

        Grid.SetColumn(rightPanel, 1);
        mainGrid.Children.Add(rightPanel);

        outer.Child = mainGrid;
        Content = outer;
    }

    private void RefreshList()
    {
        var locs = _db.GetAllLocations();
        foreach (var loc in locs)
            loc.HasImage = ImageService.FindLocationImage(loc.Code) != null;
        _gridLocations.ItemsSource = locs;
    }

    private void GridLocations_SelectionChanged(object s, SelectionChangedEventArgs e)
    {
        if (_gridLocations.SelectedItem is Location loc)
        {
            _editingId = loc.Id;
            _txCode.Text = loc.Code;
            _txDesc.Text = loc.Description;
            _btnDelete.IsEnabled = true;
            LoadLocationImage(loc.Code);
        }
        else
        {
            _editingId = null;
            _txCode.Text = "";
            _txDesc.Text = "";
            _btnDelete.IsEnabled = false;
            ClearImagePreview();
        }
    }

    private void NewLocation()
    {
        _editingId = null;
        _txCode.Text = "";
        _txDesc.Text = "";
        _btnDelete.IsEnabled = false;
        _btnDeleteImg.IsEnabled = false;
        ClearImagePreview();
        _gridLocations.SelectedItem = null;
        _txCode.Focus();
    }

    private void BtnSave_Click(object s, RoutedEventArgs e)
    {
        string code = _txCode.Text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            MessageBox.Show(TranslationService.Get("LocationCodeRequired"), TranslationService.Get("ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check duplicate code on new location
        var allLocs = _db.GetAllLocations();
        var existing = allLocs.FirstOrDefault(l =>
            l.Code.Equals(code, StringComparison.OrdinalIgnoreCase) &&
            (!_editingId.HasValue || l.Id != _editingId.Value));
        if (existing != null)
        {
            MessageBox.Show(TranslationService.Get("LocationCodeExists", existing.Code, existing.Description),
                TranslationService.Get("WarningTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var loc = new Location { Code = code, Description = _txDesc.Text.Trim() };
        if (_editingId.HasValue) loc.Id = _editingId.Value;

        int savedId = _db.SaveLocation(loc);
        _editingId = savedId;
        RefreshList();

        foreach (var item in _gridLocations.Items)
        {
            if (item is Location l && l.Id == savedId)
            {
                _gridLocations.SelectedItem = item;
                break;
            }
        }
    }

    private void BtnDelete_Click(object s, RoutedEventArgs e)
    {
        if (_gridLocations.SelectedItem is not Location loc) return;
        if (MessageBox.Show(
            TranslationService.Get("LocationDeleteConfirm", loc.Code),
            TranslationService.Get("Confirmation"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        _db.DeleteLocation(loc.Id);
        ImageService.DeleteLocationImages(loc.Code);
        RefreshList();
        NewLocation();
    }

    private void BtnSelectImage_Click(object s, RoutedEventArgs e)
    {
        string code = _txCode.Text.Trim();
        if (string.IsNullOrEmpty(code))
        {
            MessageBox.Show(TranslationService.Get("LocationCodeRequired"), TranslationService.Get("WarningTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new OpenFileDialog
        {
            Filter = TranslationService.Get("ImageFileFilter"),
            Title = TranslationService.Get("SelectLocationImage")
        };
        if (dlg.ShowDialog() != true) return;
        ImageService.SaveLocationImage(code, dlg.FileName);
        _btnDeleteImg.IsEnabled = true;
        LoadLocationImage(code);
    }

    private void BtnDeleteImage_Click(object s, RoutedEventArgs e)
    {
        string code = _txCode.Text.Trim();
        if (string.IsNullOrEmpty(code)) return;
        ImageService.DeleteLocationImages(code);
        ClearImagePreview();
        _btnDeleteImg.IsEnabled = false;
    }

    private void LoadLocationImage(string code)
    {
        string? imgPath = ImageService.FindLocationImage(code);
        if (imgPath != null)
        {
            _imgPreview.Source = null;
            var bmp = ImageService.LoadBitmapFresh(imgPath);
            _imgPreview.Source = bmp;
            _txImgPath.Text = Path.GetFileName(imgPath);
            _btnDeleteImg.IsEnabled = true;
        }
        else
        {
            ClearImagePreview();
        }
    }

    private void ClearImagePreview()
    {
        _imgPreview.Source = null;
        _txImgPath.Text = TranslationService.Get("NoImage");
        _btnDeleteImg.IsEnabled = false;
    }

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 11,
        Margin = new Thickness(0, 0, 0, 2),
        Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SubTextBrush"]
    };
}
