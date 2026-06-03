using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MyHobbyWarehouse.Data;
using MyHobbyWarehouse.Models;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Views;

public class ComponentEditDialog : Window
{
    private readonly DatabaseService _db;
    private readonly Component?      _original;
    private readonly bool            _isNew;
    private readonly List<Component>? _filteredComponents;

    private TextBox  _txSku=null!, _txOldSku=null!, _txAlt=null!;
    private TextBox  _txDesc=null!, _txUnit=null!;
    private TextBox  _txStockSum=null!, _txLastPrice=null!;
    private ComboBox _cbLocation=null!;
    private List<Location> _locations = [];
    private TextBox  _txMassMg=null!;
    private CheckBox _chkSmd=null!;
    private ComboBox _cbCat1=null!, _cbCat2=null!, _cbCat3=null!,
                     _cbCat4=null!, _cbCat5=null!;
    private TextBox  _txMfgName=null!, _txMfgPart=null!;
    private TextBox  _txS1Name=null!, _txS1Sku=null!, _txS1Price=null!, _txS1Url=null!;
    private TextBox  _txS2Name=null!, _txS2Sku=null!, _txS2Price=null!, _txS2Url=null!;
    private TextBox  _txS3Name=null!, _txS3Sku=null!, _txS3Price=null!, _txS3Url=null!;
    private TextBox  _txSticker=null!;
    private System.Windows.Controls.Image _imgPreview=null!;
    private System.Windows.Controls.Image _imgLocation=null!;
    private TextBlock _txImgPath=null!;

    public ComponentEditDialog(Component? comp, DatabaseService db, List<Component>? filteredComponents = null)
    {
        _db = db; _original = comp; _isNew = comp == null;
        _filteredComponents = filteredComponents;
        BuildUi();
        if (comp != null) Populate(comp);
        else _txSku.Text = _db.GetNextSku();
    }

    private void BuildUi()
    {
        Title  = _isNew ? TranslationService.Get("AddComponent") : TranslationService.Get("EditComponent");
        Width  = 760; Height = 800;
        MinHeight = 600;
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.Resources["BgBrush"],
            Padding    = new Thickness(16)
        };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var stack  = new StackPanel();
        scroll.Content = stack;
        outer.Child    = scroll;
        Content        = outer;

        // ── Identity ──────────────────────────────────────────────────────
        stack.Children.Add(SecHdr(TranslationService.Get("Identity")));
        stack.Children.Add(Row4(
            (TranslationService.Get("SkuRequired"), _txSku      = TB(_isNew ? "" : TranslationService.Get("Immutable"), !_isNew)),
            (TranslationService.Get("OldSku"),      _txOldSku   = TB()),
            (TranslationService.Get("Alt"),         _txAlt      = TB()),
            (TranslationService.Get("Unit"),        _txUnit     = TB("pcs"))
        ));
        stack.Children.Add(Lbl(TranslationService.Get("DescriptionRequired")));
        _txDesc = TB(); stack.Children.Add(_txDesc);

        // ── Zaloga ────────────────────────────────────────────────────────
        stack.Children.Add(SecHdr(TranslationService.Get("Stock")));
        stack.Children.Add(Row2(
            (TranslationService.Get("Total"),          _txStockSum     = TB("0")),
            (TranslationService.Get("LastPrice"),      _txLastPrice    = TB("0"))
        ));

        // Location picker
        var locGrid = new Grid { Margin = new Thickness(4) };
        locGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        locGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        locGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        locGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        locGrid.Children.Add(Lbl(TranslationService.Get("Location")));
        _locations = _db.GetAllLocations();
        _cbLocation = new ComboBox
        {
            ItemsSource = _locations,
            Margin = new Thickness(0, 0, 6, 0)
        };
        Grid.SetRow(_cbLocation, 1); Grid.SetColumn(_cbLocation, 0);
        locGrid.Children.Add(_cbLocation);
        _imgLocation = new System.Windows.Controls.Image
        {
            Width = 100, Height = 75,
            Stretch = System.Windows.Media.Stretch.Uniform,
            StretchDirection = System.Windows.Controls.StretchDirection.Both
        };
        var locImgBorder = new Border
        {
            Child = _imgLocation,
            Width = 104, Height = 79,
            BorderThickness = new Thickness(1),
            Visibility = Visibility.Collapsed
        };
        locImgBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        locImgBorder.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        Grid.SetRow(locImgBorder, 1); Grid.SetColumn(locImgBorder, 1);
        locGrid.Children.Add(locImgBorder);
        _cbLocation.SelectionChanged += (_, _) => LoadLocationThumbnail(locImgBorder);
        stack.Children.Add(locGrid);

        // ── Fizično ───────────────────────────────────────────────────────
        stack.Children.Add(SecHdr(TranslationService.Get("Physical")));
        var physRow = new Grid();
        physRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        physRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var sp1 = new StackPanel { Margin = new Thickness(4) };
        sp1.Children.Add(Lbl(TranslationService.Get("MassMg"))); _txMassMg = TB("0"); sp1.Children.Add(_txMassMg);
        var sp2 = new StackPanel { Margin = new Thickness(4, 12, 4, 4), VerticalAlignment = VerticalAlignment.Bottom };
        _chkSmd = new CheckBox { Content = TranslationService.Get("SmdCheck") };
        _chkSmd.SetResourceReference(ForegroundProperty, "TextBrush");
        sp2.Children.Add(_chkSmd);
        Grid.SetColumn(sp1, 0); physRow.Children.Add(sp1);
        Grid.SetColumn(sp2, 1); physRow.Children.Add(sp2);
        stack.Children.Add(physRow);

        // ── Kategorije ────────────────────────────────────────────────────
        stack.Children.Add(SecHdr(TranslationService.Get("Categories")));
        var allComps = _db.GetAllComponents();
        var cat1 = allComps.Select(c => c.Category1).Where(s => s != "").Distinct().OrderBy(s => s).ToList();
        var cat2 = allComps.Select(c => c.Category2).Where(s => s != "").Distinct().OrderBy(s => s).ToList();
        var cat3 = allComps.Select(c => c.Category3).Where(s => s != "").Distinct().OrderBy(s => s).ToList();
        var cat4 = allComps.Select(c => c.Category4).Where(s => s != "").Distinct().OrderBy(s => s).ToList();
        var cat5 = allComps.Select(c => c.Category5).Where(s => s != "").Distinct().OrderBy(s => s).ToList();
        stack.Children.Add(Row4CB(
            (TranslationService.Get("TypeCat1"),     _cbCat1 = EditCb(cat1)),
            (TranslationService.Get("SubTypeCat2"),  _cbCat2 = EditCb(cat2)),
            (TranslationService.Get("ValueCat3"),    _cbCat3 = EditCb(cat3)),
            (TranslationService.Get("PackageCat4"),  _cbCat4 = EditCb(cat4))
        ));
        var c5Sp = new StackPanel { Margin = new Thickness(4) };
        c5Sp.Children.Add(Lbl(TranslationService.Get("OtherCat5"))); _cbCat5 = EditCb(cat5); c5Sp.Children.Add(_cbCat5);
        stack.Children.Add(c5Sp);

        // ── Proizvajalec ──────────────────────────────────────────────────
        stack.Children.Add(SecHdr(TranslationService.Get("Manufacturer")));
        stack.Children.Add(Row2((TranslationService.Get("Name"), _txMfgName = TB()), (TranslationService.Get("MfgPart"), _txMfgPart = TB())));

        // ── Dobavitelji ───────────────────────────────────────────────────
        stack.Children.Add(SecHdr(TranslationService.Get("Suppliers")));
        stack.Children.Add(SupRow(TranslationService.Get("Supplier1"), ref _txS1Name, ref _txS1Sku, ref _txS1Price, ref _txS1Url));
        stack.Children.Add(SupRow(TranslationService.Get("Supplier2"), ref _txS2Name, ref _txS2Sku, ref _txS2Price, ref _txS2Url));
        stack.Children.Add(SupRow(TranslationService.Get("Supplier3"), ref _txS3Name, ref _txS3Sku, ref _txS3Price, ref _txS3Url));

        // ── Sticker ───────────────────────────────────────────────────────
        stack.Children.Add(SecHdr(TranslationService.Get("Sticker")));
        _txSticker = TB(); stack.Children.Add(_txSticker);

        // ── Slika ─────────────────────────────────────────────────────────
        stack.Children.Add(SecHdr(TranslationService.Get("ComponentImage")));
        var imgGrid = new Grid();
        imgGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
        imgGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Image preview box
        var imgBorder = new Border
        {
            Width = 320, Height = 200,
            Background    = (System.Windows.Media.Brush)Application.Current.Resources["CardBrush"],
            BorderBrush   = (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"],
            BorderThickness = new Thickness(1),
            Margin        = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _imgPreview = new System.Windows.Controls.Image
        {
            Stretch = System.Windows.Media.Stretch.Uniform,
            MaxWidth = 316, MaxHeight = 196
        };
        imgBorder.Child = _imgPreview;
        Grid.SetColumn(imgBorder, 0); imgGrid.Children.Add(imgBorder);

        // Image controls
        var imgCtrl = new StackPanel { Margin = new Thickness(12, 4, 4, 4), VerticalAlignment = VerticalAlignment.Top };
        _txImgPath = new TextBlock
        {
            Text         = TranslationService.Get("NoImage"),
            FontSize     = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 0, 0, 8)
        };
        _txImgPath.SetResourceReference(ForegroundProperty, "SubTextBrush");

        var btnSelImg = new Button { Content = TranslationService.Get("SelectImage"), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 6) };
        btnSelImg.Click += BtnSelectImage_Click;
        var btnMulti = new Button { Content = TranslationService.Get("AssignToMultiple"), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 6) };
        btnMulti.Click += BtnMultiAssign_Click;
        var btnDelImg = new Button { Content = TranslationService.Get("DeleteImage"), Padding = new Thickness(10, 6, 10, 6) };
        btnDelImg.Style = (Style)Application.Current.Resources["DangerButton"];
        btnDelImg.Click += BtnDeleteImage_Click;

        var btnDelMulti = new Button
        {
            Content = TranslationService.Get("DeleteImageFiltered"),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 6, 0, 0),
            IsEnabled = _filteredComponents != null && _filteredComponents.Count > 0
        };
        btnDelMulti.Style = (Style)Application.Current.Resources["DangerButton"];
        btnDelMulti.Click += BtnDeleteMulti_Click;

        var fmtNote = new TextBlock
        {
            Text = TranslationService.Get("ImageFormatNote"),
            FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0)
        };
        fmtNote.SetResourceReference(ForegroundProperty, "SubTextBrush");

        imgCtrl.Children.Add(_txImgPath);
        imgCtrl.Children.Add(btnSelImg);
        imgCtrl.Children.Add(btnMulti);
        imgCtrl.Children.Add(btnDelImg);
        imgCtrl.Children.Add(btnDelMulti);
        imgCtrl.Children.Add(fmtNote);
        Grid.SetColumn(imgCtrl, 1); imgGrid.Children.Add(imgCtrl);
        stack.Children.Add(imgGrid);

        // ── Buttons ───────────────────────────────────────────────────────
        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var btnCancel = new Button { Content = TranslationService.Get("Cancel"), Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 6, 0) };
        var btnSave   = new Button { Content = TranslationService.Get("Save"), Padding = new Thickness(18, 7, 18, 7) };
        btnSave.Style = (Style)Application.Current.Resources["AccentButton"];
        btnCancel.Click += (_, _) => { DialogResult = false; Close(); };
        btnSave.Click   += Save_Click;
        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnSave);
        stack.Children.Add(btnRow);
    }

    private void Populate(Component c)
    {
        _txSku.Text          = c.Sku;
        _txOldSku.Text       = c.OldSku;
        _txAlt.Text          = c.Alt;
        _txDesc.Text         = c.Description;
        _txUnit.Text         = c.Unit;
        _txStockSum.Text     = c.StockSum.ToString("F0");
        _txLastPrice.Text    = c.LastPrice.ToString("F4");
        if (c.LocationId.HasValue)
            _cbLocation.SelectedItem = _locations.FirstOrDefault(l => l.Id == c.LocationId.Value);
        _txMassMg.Text       = c.MassMg.ToString("F0");
        _chkSmd.IsChecked    = c.Smd;
        _cbCat1.Text = c.Category1; _cbCat2.Text = c.Category2;
        _cbCat3.Text = c.Category3; _cbCat4.Text = c.Category4; _cbCat5.Text = c.Category5;
        _txMfgName.Text = c.ManufacturerName; _txMfgPart.Text = c.ManufacturerPart;
        _txS1Name.Text = c.Supplier1Name; _txS1Sku.Text = c.Supplier1Sku;
        _txS1Price.Text = c.Supplier1Price.ToString("F4"); _txS1Url.Text = c.Supplier1Url;
        _txS2Name.Text = c.Supplier2Name; _txS2Sku.Text = c.Supplier2Sku;
        _txS2Price.Text = c.Supplier2Price.ToString("F4"); _txS2Url.Text = c.Supplier2Url;
        _txS3Name.Text = c.Supplier3Name; _txS3Sku.Text = c.Supplier3Sku;
        _txS3Price.Text = c.Supplier3Price.ToString("F4"); _txS3Url.Text = c.Supplier3Url;
        _txSticker.Text = c.StickerText;

        // Load image
        LoadImagePreview(c.Sku);
    }

    private void LoadLocationThumbnail(Border border)
    {
        var loc = _cbLocation.SelectedItem as Location;
        if (loc == null || string.IsNullOrEmpty(loc.Code))
        { _imgLocation.Source = null; border.Visibility = Visibility.Collapsed; return; }
        string? path = ImageService.FindLocationImage(loc.Code);
        if (path != null)
        {
            var bmp = ImageService.LoadBitmapFresh(path);
            _imgLocation.Source = bmp;
            border.Visibility = Visibility.Visible;
        }
        else
        {
            _imgLocation.Source = null;
            border.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadImagePreview(string sku)
    {
        string? imgPath = ImageService.FindImage(sku);
        if (imgPath != null)
        {
            var bmp = ImageService.LoadBitmap(imgPath);
            _imgPreview.Source = bmp;
            _txImgPath.Text = Path.GetFileName(imgPath);
        }
        else
        {
            _imgPreview.Source = null;
            _txImgPath.Text = TranslationService.Get("NoImage");
        }
    }

    private void BtnSelectImage_Click(object s, RoutedEventArgs e)
    {
        string sku = _isNew ? _txSku.Text.Trim() : _original!.Sku;
        if (string.IsNullOrEmpty(sku)) { MessageBox.Show(TranslationService.Get("EnterSkuFirst"), TranslationService.Get("Warning"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        var dlg = new OpenFileDialog
        {
            Filter = TranslationService.Get("ImageFileFilter"),
            Title  = TranslationService.Get("SelectComponentImage")
        };
        if (dlg.ShowDialog() != true) return;
        ImageService.SaveImage(sku, dlg.FileName);
        LoadImagePreview(sku);
    }

    private void BtnMultiAssign_Click(object s, RoutedEventArgs e)
    {
        string sku = _isNew ? _txSku.Text.Trim() : _original!.Sku;
        if (string.IsNullOrEmpty(sku))
        { Err(TranslationService.Get("EnterSkuFirst")); return; }

        string? imgPath = ImageService.FindImage(sku);
        if (imgPath == null)
        { Err(TranslationService.Get("AddImageFirst")); return; }

        var dlg = new MultiImageAssignDialog(imgPath, sku, _db, _filteredComponents);
        dlg.Owner = this;
        dlg.ShowDialog();
    }

    private void BtnDeleteImage_Click(object s, RoutedEventArgs e)
    {
        string sku = _isNew ? _txSku.Text.Trim() : _original!.Sku;
        if (string.IsNullOrEmpty(sku)) return;
        ImageService.DeleteImages(sku);
        _imgPreview.Source = null;
        _txImgPath.Text = TranslationService.Get("NoImage");
    }

    private void BtnDeleteMulti_Click(object s, RoutedEventArgs e)
    {
        if (_filteredComponents == null || _filteredComponents.Count == 0) return;

        int hasImage = _filteredComponents.Count(c => ImageService.FindImage(c.Sku) != null);
        if (hasImage == 0) { MessageBox.Show(TranslationService.Get("NoFilteredHaveImages"), TranslationService.Get("Warning"), MessageBoxButton.OK, MessageBoxImage.Information); return; }

        if (MessageBox.Show(TranslationService.Get("DeleteImageConfirm", hasImage, _filteredComponents.Count), TranslationService.Get("Confirmation"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        int ok = 0;
        foreach (var c in _filteredComponents)
        {
            if (ImageService.FindImage(c.Sku) != null)
            {
                ImageService.DeleteImages(c.Sku);
                ok++;
            }
        }

        // Clear preview if current component's image was deleted
        string curSku = _isNew ? _txSku.Text.Trim() : _original!.Sku;
        if (ImageService.FindImage(curSku) == null)
        {
            _imgPreview.Source = null;
            _txImgPath.Text = TranslationService.Get("NoImage");
        }

        MessageBox.Show(TranslationService.Get("ImageDeletedMultiple", ok), TranslationService.Get("Completed"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Save_Click(object s, RoutedEventArgs e)
    {
        string sku = _txSku.Text.Trim();
        if (string.IsNullOrEmpty(sku))         { Err(TranslationService.Get("SkuRequiredMsg")); return; }
        if (string.IsNullOrEmpty(_txDesc.Text)) { Err(TranslationService.Get("DescriptionRequiredMsg")); return; }

        var comp = _original ?? new Component { Sku = sku };
        if (_isNew) comp.Sku = sku;
        comp.OldSku = _txOldSku.Text.Trim(); comp.Alt = _txAlt.Text.Trim();
        comp.Description = _txDesc.Text.Trim();
        comp.Unit = string.IsNullOrEmpty(_txUnit.Text) ? "pcs" : _txUnit.Text.Trim();
        comp.StockSum = D(_txStockSum.Text); comp.LastPrice = D(_txLastPrice.Text);
        comp.StockValue = comp.StockSum * comp.LastPrice;
        comp.LocationId = (_cbLocation.SelectedItem as Location)?.Id;
        comp.MassMg = D(_txMassMg.Text); comp.Smd = _chkSmd.IsChecked == true;
        comp.Category1 = CbT(_cbCat1); comp.Category2 = CbT(_cbCat2);
        comp.Category3 = CbT(_cbCat3); comp.Category4 = CbT(_cbCat4); comp.Category5 = CbT(_cbCat5);
        comp.ManufacturerName = _txMfgName.Text.Trim(); comp.ManufacturerPart = _txMfgPart.Text.Trim();
        comp.Supplier1Name = _txS1Name.Text.Trim(); comp.Supplier1Sku = _txS1Sku.Text.Trim();
        comp.Supplier1Price = D(_txS1Price.Text); comp.Supplier1Url = _txS1Url.Text.Trim();
        comp.Supplier2Name = _txS2Name.Text.Trim(); comp.Supplier2Sku = _txS2Sku.Text.Trim();
        comp.Supplier2Price = D(_txS2Price.Text); comp.Supplier2Url = _txS2Url.Text.Trim();
        comp.Supplier3Name = _txS3Name.Text.Trim(); comp.Supplier3Sku = _txS3Sku.Text.Trim();
        comp.Supplier3Price = D(_txS3Price.Text); comp.Supplier3Url = _txS3Url.Text.Trim();
        comp.StickerText = _txSticker.Text.Trim();
        _db.SaveComponent(comp);
        DialogResult = true; Close();
    }

    // ── UI helpers ────────────────────────────────────────────────────────────
    private static TextBlock SecHdr(string t) => new()
    {
        Text = t, FontWeight = FontWeights.SemiBold, FontSize = 12,
        Margin = new Thickness(0, 12, 0, 4),
        Foreground = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"]
    };
    private static TextBlock Lbl(string t) => new()
    {
        Text = t, FontSize = 11, Margin = new Thickness(0, 0, 0, 2),
        Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SubTextBrush"]
    };
    private static TextBox TB(string text = "", bool ro = false)
        => new() { Text = text, Margin = new Thickness(0, 0, 0, 4), IsReadOnly = ro, Opacity = ro ? 0.5 : 1 };
    private static ComboBox EditCb(List<string> items)
    {
        var cb = new ComboBox { IsEditable = true, IsTextSearchEnabled = true, Margin = new Thickness(0, 0, 0, 4) };
        foreach (var i in items) cb.Items.Add(i);
        return cb;
    }
    private static string CbT(ComboBox cb) => (cb.Text ?? cb.SelectedItem?.ToString() ?? "").Trim();
    private static Grid FourCol()
    {
        var g = new Grid();
        for (int i = 0; i < 4; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        return g;
    }
    private static Grid Row4((string L, TextBox T) c1, (string L, TextBox T) c2, (string L, TextBox T) c3, (string L, TextBox T) c4)
    {
        var g = FourCol(); int i = 0;
        foreach (var (l, t) in new[] { c1, c2, c3, c4 })
        { var sp = new StackPanel { Margin = new Thickness(4) }; sp.Children.Add(Lbl(l)); sp.Children.Add(t); Grid.SetColumn(sp, i++); g.Children.Add(sp); }
        return g;
    }
    private static Grid Row4CB((string L, ComboBox C) c1, (string L, ComboBox C) c2, (string L, ComboBox C) c3, (string L, ComboBox C) c4)
    {
        var g = FourCol(); int i = 0;
        foreach (var (l, c) in new[] { c1, c2, c3, c4 })
        { var sp = new StackPanel { Margin = new Thickness(4) }; sp.Children.Add(Lbl(l)); sp.Children.Add(c); Grid.SetColumn(sp, i++); g.Children.Add(sp); }
        return g;
    }
    private static Grid Row2((string L, TextBox T) c1, (string L, TextBox T) c2)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int i = 0;
        foreach (var (l, t) in new[] { c1, c2 })
        { var sp = new StackPanel { Margin = new Thickness(4) }; sp.Children.Add(Lbl(l)); sp.Children.Add(t); Grid.SetColumn(sp, i++); g.Children.Add(sp); }
        return g;
    }
    private static Grid SupRow(string hdr, ref TextBox tName, ref TextBox tSku, ref TextBox tPrice, ref TextBox tUrl)
    {
        var g = new Grid();
        foreach (var w in new[] { 1.4, 1.2, 0.8, 2.0 })
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
        tName = TB(); tSku = TB(); tPrice = TB("0"); tUrl = TB();
        tUrl.PreviewMouseDoubleClick += OpenUrl;
        tUrl.ToolTip           = TranslationService.Get("DoubleClickOpenUrl");
        tUrl.Cursor            = System.Windows.Input.Cursors.Hand;
        tUrl.Foreground        = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"];
        int i = 0;
        foreach (var (l, t) in new (string, TextBox)[] { (TranslationService.Get("SupplierNameLabel", hdr), tName), (TranslationService.Get("SupplierSku"), tSku), (TranslationService.Get("Price"), tPrice), (TranslationService.Get("SupplierUrl"), tUrl) })
        { var sp = new StackPanel { Margin = new Thickness(4) }; sp.Children.Add(Lbl(l)); sp.Children.Add(t); Grid.SetColumn(sp, i++); g.Children.Add(sp); }
        return g;
    }

    /// <summary>Opens a URL in the default browser. Called on double-click in URL fields.</summary>
    private static void OpenUrl(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not TextBox tb) return;
        string url = tb.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
        e.Handled = true;
    }

    private static void Err(string m) => MessageBox.Show(m, TranslationService.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Warning);
    private static double D(string s) => double.TryParse(s.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : 0;
}
