using System.Windows;
using System.Windows.Controls;
using MyHobbyWarehouse.Models;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Views;

public class ProjectEditDialog : Window
{
    public Project? Result { get; private set; }
    public string? PendingImagePath => _pendingImagePath;

    private readonly TextBox _txName, _txBoard, _txRev, _txVer, _txDesc, _txNotes;
    private readonly System.Windows.Controls.Image _imgPreview = null!;
    private readonly TextBlock _txImgPath = null!;
    private readonly Button _btnSelectImg, _btnDeleteImg;
    private int _projectId;
    private string? _pendingImagePath;

    public ProjectEditDialog(Project? existing)
    {
        Title  = existing == null ? TranslationService.Get("NewProject") : TranslationService.Get("EditProject");
        Width  = 520; Height = 580;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.Resources["BgBrush"],
            Padding    = new Thickness(20)
        };

        var stack = new StackPanel();

        // Fields
        stack.Children.Add(Label(TranslationService.Get("ProjectName") + " *"));
        _txName  = Tb(); stack.Children.Add(_txName);

        stack.Children.Add(Label(TranslationService.Get("BoardName")));
        _txBoard = Tb(); stack.Children.Add(_txBoard);

        var verRevGrid = new Grid();
        verRevGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        verRevGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var verSp = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
        verSp.Children.Add(Label(TranslationService.Get("Version")));
        _txVer = Tb();
        verSp.Children.Add(_txVer);
        var revSp = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
        revSp.Children.Add(Label(TranslationService.Get("Revision")));
        _txRev = Tb();
        revSp.Children.Add(_txRev);
        Grid.SetColumn(verSp, 0); verRevGrid.Children.Add(verSp);
        Grid.SetColumn(revSp, 1); verRevGrid.Children.Add(revSp);
        stack.Children.Add(verRevGrid);

        stack.Children.Add(Label(TranslationService.Get("Description")));
        _txDesc  = Tb(); stack.Children.Add(_txDesc);

        stack.Children.Add(Label(TranslationService.Get("Notes")));
        _txNotes = new TextBox
        {
            AcceptsReturn       = true,
            TextWrapping        = TextWrapping.Wrap,
            Height              = 60,
            Margin              = new Thickness(0, 0, 0, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        stack.Children.Add(_txNotes);

        // ── Image ─────────────────────────────────────────────────────────
        stack.Children.Add(Label(TranslationService.Get("ProjectImage")));
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
        stack.Children.Add(imgBorder);

        _txImgPath = new TextBlock
        {
            Text = TranslationService.Get("NoImage"),
            FontSize = 11, Margin = new Thickness(0, 0, 0, 4)
        };
        _txImgPath.SetResourceReference(ForegroundProperty, "SubTextBrush");
        stack.Children.Add(_txImgPath);

        var imgBtnRow = new StackPanel { Orientation = Orientation.Horizontal };
        _btnSelectImg = new Button { Content = TranslationService.Get("SelectImage"), Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 0, 4, 0) };
        _btnSelectImg.Click += BtnSelectImage_Click;
        imgBtnRow.Children.Add(_btnSelectImg);
        _btnDeleteImg = new Button
        {
            Content = TranslationService.Get("DeleteImage"),
            Padding = new Thickness(8, 4, 8, 4),
            IsEnabled = false
        };
        _btnDeleteImg.Style = (Style)Application.Current.Resources["DangerButton"];
        _btnDeleteImg.Click += BtnDeleteImage_Click;
        imgBtnRow.Children.Add(_btnDeleteImg);
        stack.Children.Add(imgBtnRow);

        // Prefill
        _projectId = existing?.Id ?? 0;
        if (existing != null)
        {
            _txName.Text  = existing.Name;
            _txBoard.Text = existing.BoardName;
            _txVer.Text   = existing.Version;
            _txRev.Text   = existing.Revision;
            _txDesc.Text  = existing.Description;
            _txNotes.Text = existing.Notes;
            LoadProjectImage();
        }

        // Buttons
        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(0, 6, 0, 0)
        };

        var btnCancel = new Button { Content = TranslationService.Get("Cancel"), Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 6, 0) };
        var btnSave   = new Button { Content = TranslationService.Get("Save"), Padding = new Thickness(18, 7, 18, 7) };
        btnSave.Style = (Style)Application.Current.Resources["AccentButton"];

        btnCancel.Click += (_, _) => { DialogResult = false; Close(); };
        btnSave.Click   += (_, _) => Save(existing);

        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnSave);
        stack.Children.Add(btnRow);

        outer.Child = stack;
        Content     = outer;

        Loaded += (_, _) => _txName.Focus();
    }

    private void LoadProjectImage()
    {
        if (_projectId <= 0) return;
        string? path = ImageService.FindProjectImage(_projectId);
        if (path != null)
        {
            _imgPreview.Source = null;
            var bmp = ImageService.LoadBitmapFresh(path);
            _imgPreview.Source = bmp;
            _txImgPath.Text = Path.GetFileName(path);
            _btnDeleteImg.IsEnabled = true;
        }
        else
        {
            _imgPreview.Source = null;
            _txImgPath.Text = TranslationService.Get("NoImage");
            _btnDeleteImg.IsEnabled = false;
        }
    }

    private void BtnSelectImage_Click(object s, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = TranslationService.Get("ImageFileFilter"),
            Title = TranslationService.Get("SelectProjectImage")
        };
        if (dlg.ShowDialog() != true) return;
        string sourcePath = dlg.FileName;
        if (_projectId > 0)
        {
            ImageService.SaveProjectImage(_projectId, sourcePath);
            LoadProjectImage();
        }
        else
        {
            _pendingImagePath = sourcePath;
            var bmp = ImageService.LoadBitmapFresh(sourcePath);
            _imgPreview.Source = bmp;
            _txImgPath.Text = Path.GetFileName(sourcePath);
            _btnDeleteImg.IsEnabled = true;
        }
    }

    private void BtnDeleteImage_Click(object s, RoutedEventArgs e)
    {
        if (_projectId > 0)
            ImageService.DeleteProjectImages(_projectId);
        _pendingImagePath = null;
        _imgPreview.Source = null;
        _txImgPath.Text = TranslationService.Get("NoImage");
        _btnDeleteImg.IsEnabled = false;
    }

    private void Save(Project? existing)
    {
        if (string.IsNullOrWhiteSpace(_txName.Text))
        {
            MessageBox.Show(TranslationService.Get("ProjectNameRequired"), TranslationService.Get("ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = existing ?? new Project { CreatedDate = DateTime.Now };
        Result.Name        = _txName.Text.Trim();
        Result.BoardName   = _txBoard.Text.Trim();
        Result.Version     = _txVer.Text.Trim();
        Result.Revision    = _txRev.Text.Trim();
        Result.Description = _txDesc.Text.Trim();
        Result.Notes       = _txNotes.Text.Trim();
        Result.ModifiedDate = DateTime.Now;
        Result.HasImage    = _pendingImagePath != null || ImageService.FindProjectImage(_projectId) != null;

        DialogResult = true;
        Close();
    }

    private static TextBlock Label(string text) => new()
    {
        Text       = text,
        Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SubTextBrush"],
        FontSize   = 11,
        Margin     = new Thickness(0, 8, 0, 2)
    };

    private static TextBox Tb() => new() { Margin = new Thickness(0, 0, 0, 2) };
}
