using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Views;

/// <summary>
/// Dialog shown on first run (or from Settings) to choose where the SQLite DB is stored.
/// Supports: use default path, browse for existing DB, or type a custom path.
/// </summary>
public class DbLocationDialog : Window
{
    public string SelectedPath { get; private set; } = SettingsService.DefaultDbPath;
    public bool ComponentsImported { get; private set; } = false;
    public string SelectedLanguage { get; private set; } = "en";

    private readonly TextBox _txPath;
    private readonly ComboBox _cmbLanguage;
    private readonly TextBox _txAppName;
    private readonly TextBox _txAppDesc;
    private readonly TextBox _txLogoPath;

    public DbLocationDialog(string? currentPath, string? currentLanguage = null)
    {
        Title  = string.IsNullOrEmpty(currentPath) ? TranslationService.Get("FirstRunTitle") : TranslationService.Get("SettingsTitle");
        Width  = 560; Height = 660;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var outer = new Border { Padding = new Thickness(24) };
        // Apply background manually since Styles.xaml may not be loaded yet at startup
        outer.SetResourceReference(Border.BackgroundProperty, "BgBrush");

        var scroll = new System.Windows.Controls.ScrollViewer { VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto };
        var stack = new StackPanel();

        // Title
        var title = new TextBlock
        {
            Text       = TranslationService.Get("FirstRunTitle"),
            FontSize   = 16,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 6)
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        stack.Children.Add(title);

        var sub = new TextBlock
        {
            Text       = TranslationService.Get("FirstRunInfo"),
            FontSize   = 11,
            Margin     = new Thickness(0, 0, 0, 16)
        };
        sub.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        stack.Children.Add(sub);

        // Path row
        var pathRow = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _txPath = new TextBox
        {
            Text    = string.IsNullOrEmpty(currentPath) ? SettingsService.DefaultDbPath : currentPath,
            Padding = new Thickness(6, 5, 6, 5),
            Margin  = new Thickness(0, 0, 6, 0)
        };
        Grid.SetColumn(_txPath, 0);
        pathRow.Children.Add(_txPath);

        var btnBrowse = new Button
        {
            Content = "📂 " + TranslationService.Get("Browse"),
            Padding = new Thickness(10, 5, 10, 5),
            Margin  = new Thickness(0, 0, 6, 0)
        };
        btnBrowse.Click += BrowseExisting_Click;
        Grid.SetColumn(btnBrowse, 1);
        pathRow.Children.Add(btnBrowse);

        var btnDefault = new Button
        {
            Content = TranslationService.Get("DefaultDb"),
            Padding = new Thickness(10, 5, 10, 5)
        };
        btnDefault.Click += (_, _) => _txPath.Text = SettingsService.DefaultDbPath;
        Grid.SetColumn(btnDefault, 2);
        pathRow.Children.Add(btnDefault);

        stack.Children.Add(pathRow);

        // Info label showing default path
        var info = new TextBlock
        {
            Text     = $"{TranslationService.Get("DefaultPath", SettingsService.DefaultDbPath)}",
            FontSize = 10,
            Margin   = new Thickness(0, 0, 0, 10)
        };
        info.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        stack.Children.Add(info);

        // ── Language selector ─────────────────────────────────────────────
        var langHdr = new TextBlock
        {
            Text       = TranslationService.Get("Language"),
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 4)
        };
        langHdr.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        stack.Children.Add(langHdr);

        _cmbLanguage = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 14)
        };
        var langs = TranslationService.GetAvailableLanguages();
        foreach (var (code, name) in langs)
            _cmbLanguage.Items.Add(new { Label = name, Value = code });
        _cmbLanguage.DisplayMemberPath = "Label";
        _cmbLanguage.SelectedValuePath = "Value";
        _cmbLanguage.SelectedValue = currentLanguage ?? "en";
        stack.Children.Add(_cmbLanguage);

        // Language management buttons
        var langBtnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 14)
        };

        var btnNewLang = new Button { Content = "➕ " + TranslationService.Get("LangNew"), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 0) };
        btnNewLang.Click += BtnNewLang_Click;
        langBtnRow.Children.Add(btnNewLang);

        var btnEditLang = new Button { Content = "✏ " + TranslationService.Get("LangEdit"), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 0) };
        btnEditLang.Click += BtnEditLang_Click;
        langBtnRow.Children.Add(btnEditLang);

        var btnDeleteLang = new Button { Content = "🗑 " + TranslationService.Get("LangDelete"), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 6, 0) };
        btnDeleteLang.Click += BtnDeleteLang_Click;
        langBtnRow.Children.Add(btnDeleteLang);

        var btnImportLang = new Button { Content = "📂 " + TranslationService.Get("ImportLang"), Padding = new Thickness(10, 5, 10, 5) };
        btnImportLang.Click += BtnImportLang_Click;
        langBtnRow.Children.Add(btnImportLang);

        stack.Children.Add(langBtnRow);

        // ── App appearance ────────────────────────────────────────────────
        var appHdr = new TextBlock
        {
            Text       = TranslationService.Get("Appearance"),
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 4)
        };
        appHdr.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        stack.Children.Add(appHdr);

        var appNameLabel = new TextBlock { Text = TranslationService.Get("AppName"), Margin = new Thickness(0, 0, 0, 2) };
        appNameLabel.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        stack.Children.Add(appNameLabel);

        _txAppName = new TextBox { Text = "MyHobbyWarehouse", Padding = new Thickness(6, 5, 6, 5), Margin = new Thickness(0, 0, 0, 8) };
        stack.Children.Add(_txAppName);

        var appDescLabel = new TextBlock { Text = TranslationService.Get("AppDescription"), Margin = new Thickness(0, 0, 0, 2) };
        appDescLabel.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        stack.Children.Add(appDescLabel);

        _txAppDesc = new TextBox { Text = "", Padding = new Thickness(6, 5, 6, 5), Margin = new Thickness(0, 0, 0, 8) };
        stack.Children.Add(_txAppDesc);

        // Logo path
        var logoLabel = new TextBlock { Text = TranslationService.Get("AppLogo"), Margin = new Thickness(0, 0, 0, 2) };
        logoLabel.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        stack.Children.Add(logoLabel);

        var logoRow = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        logoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        logoRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _txLogoPath = new TextBox { Padding = new Thickness(6, 5, 6, 5), Margin = new Thickness(0, 0, 6, 0) };
        Grid.SetColumn(_txLogoPath, 0);
        logoRow.Children.Add(_txLogoPath);

        var btnLogo = new Button { Content = "📂 " + TranslationService.Get("Browse"), Padding = new Thickness(10, 5, 10, 5) };
        btnLogo.Click += BtnLogo_Click;
        Grid.SetColumn(btnLogo, 1);
        logoRow.Children.Add(btnLogo);

        stack.Children.Add(logoRow);

        // Load existing values from DB if available
        try
        {
            var db = Data.DatabaseService.Current;
            if (db != null)
            {
                var appInfo = db.GetAppInfo();
                _txAppName.Text = appInfo.Name;
                _txAppDesc.Text = appInfo.Description;
                _txLogoPath.Text = ImageService.FindLogoImage() != null ? "✓ " + TranslationService.Get("LogoSet") : "";
            }
        }
        catch { /* DB not yet initialized */ }

        // Buttons
        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var btnCancel = new Button
        {
            Content = TranslationService.Get("Cancel"),
            Padding = new Thickness(12, 7, 12, 7),
            Margin  = new Thickness(0, 0, 8, 0)
        };
        btnCancel.Click += (_, _) => { DialogResult = false; Close(); };

        var btnOk = new Button
        {
            Content = "✔ " + TranslationService.Get("ConfirmPath"),
            Padding = new Thickness(18, 7, 18, 7)
        };
        btnOk.SetResourceReference(Button.StyleProperty, "AccentButton");
        btnOk.Click += Confirm_Click;

        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnOk);
        stack.Children.Add(btnRow);

        // ── Import base.ods section ───────────────────────────────────────
        var sep = new System.Windows.Controls.Separator { Margin = new Thickness(0, 16, 0, 12) };
        stack.Children.Add(sep);

        var importHdr = new TextBlock
        {
            Text       = TranslationService.Get("ImportOdsTitle"),
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 4)
        };
        importHdr.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        stack.Children.Add(importHdr);

        var importSub = new TextBlock
        {
            Text     = TranslationService.Get("ImportOdsInfo"),
            FontSize = 11,
            Margin   = new Thickness(0, 0, 0, 8)
        };
        importSub.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        stack.Children.Add(importSub);

        var btnImport = new Button
        {
            Content = "📥 " + TranslationService.Get("ImportOds"),
            Padding = new Thickness(14, 7, 14, 7),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        btnImport.Click += ImportBase_Click;
        stack.Children.Add(btnImport);

        scroll.Content = stack;
        outer.Child    = scroll;
        Content        = outer;
    }

    private void ImportBase_Click(object s, System.Windows.RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = TranslationService.Get("ImportFileFilter"),
            Title  = TranslationService.Get("SelectBaseOds")
        };
        if (dlg.ShowDialog() != true) return;

        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            var (components, errors) = Services.ImportService.ImportBaseOds(dlg.FileName);

            if (errors.Count > 0 && components.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    TranslationService.Get("ImportOdsErrors", errors.Count), TranslationService.Get("ErrorTitle"),
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            // Save path first so DB is initialised before import
            var settings = Services.SettingsService.Load();
            if (!string.IsNullOrEmpty(_txPath.Text.Trim()))
                settings.DatabasePath = _txPath.Text.Trim();
            Services.SettingsService.Save(settings);
            var db = Data.DatabaseService.Current
                     ?? new Data.DatabaseService(settings.DatabasePath);

            int imported = 0;
            foreach (var c in components) { db.SaveComponent(c); imported++; }

            string msg = TranslationService.Get("ImportOdsDone", imported);
            if (errors.Count > 0)
                msg += TranslationService.Get("ImportOdsErrors", errors.Count) + ":" + string.Join("", errors.Take(20));
            ComponentsImported = true;
            System.Windows.MessageBox.Show(msg, TranslationService.Get("Completed"),
                System.Windows.MessageBoxButton.OK,
                errors.Count > 0
                    ? System.Windows.MessageBoxImage.Warning
                    : System.Windows.MessageBoxImage.Information);
        }
        finally { System.Windows.Input.Mouse.OverrideCursor = null; }
    }

    private void BrowseExisting_Click(object s, RoutedEventArgs e)
    {
        // SaveFileDialog lets user pick new OR existing .db file
        var dlg = new SaveFileDialog
        {
            Title            = TranslationService.Get("SelectDb"),
            Filter           = TranslationService.Get("DbBrowseFilter"),
            FileName         = Path.GetFileName(_txPath.Text),
            InitialDirectory = Path.GetDirectoryName(_txPath.Text)
                               ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            OverwritePrompt  = false   // don't warn — opening existing DB is intentional
        };
        if (dlg.ShowDialog() == true)
            _txPath.Text = dlg.FileName;
    }

    private void BtnLogo_Click(object s, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = TranslationService.Get("SelectAppLogo"),
            Filter = TranslationService.Get("LogoBrowseFilter"),
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
            _txLogoPath.Text = dlg.FileName;
    }

    private void Confirm_Click(object s, RoutedEventArgs e)
    {
        string path = _txPath.Text.Trim();

        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show(TranslationService.Get("PathRequired"), TranslationService.Get("ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Must end in .db
        if (!path.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
            path += ".db";

        // Validate directory is writable
        string? dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir))
        {
            MessageBox.Show(TranslationService.Get("InvalidPath"), TranslationService.Get("ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try { Directory.CreateDirectory(dir); }
        catch (Exception ex)
        {
            MessageBox.Show(TranslationService.Get("PathCreateError", ex.Message), TranslationService.Get("ErrorTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        SelectedPath = path;
        SelectedLanguage = _cmbLanguage.SelectedValue?.ToString() ?? "en";

        // Save app info
        try
        {
            var db = Data.DatabaseService.Current;
            if (db != null)
            {
                var info = db.GetAppInfo();
                info.Name = _txAppName.Text.Trim();
                info.Description = _txAppDesc.Text.Trim();
                string logoPath = _txLogoPath.Text.Trim();
                if (!string.IsNullOrEmpty(logoPath))
                    ImageService.SaveLogoImage(logoPath);
                info.LogoPath = ImageService.FindLogoImage() != null ? "1" : "";
                db.SaveAppInfo(info);
            }
        }
        catch { /* DB not yet initialized */ }

        DialogResult = true;
        Close();
    }

    private void RefreshLanguages()
    {
        _cmbLanguage.Items.Clear();
        foreach (var (code, name) in TranslationService.GetAvailableLanguages())
            _cmbLanguage.Items.Add(new { Label = name, Value = code });
    }

    private void BtnNewLang_Click(object s, System.Windows.RoutedEventArgs e)
    {
        var nameDlg = new InputDialog(TranslationService.Get("LangNewTitle"), TranslationService.Get("LangNewName"), "");
        if (nameDlg.ShowDialog() != true) return;
        string langName = nameDlg.InputValue.Trim();
        if (string.IsNullOrEmpty(langName)) return;

        var codeDlg = new InputDialog(TranslationService.Get("LangNewTitle"), TranslationService.Get("LangNewCode"), "");
        if (codeDlg.ShowDialog() != true) return;
        string langCode = codeDlg.InputValue.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(langCode)) return;

        // Check for duplicates
        var existing = TranslationService.GetAvailableLanguages();
        if (existing.Any(l => l.Code.Equals(langCode, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(TranslationService.Get("LangCodeExists", langCode),
                TranslationService.Get("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            TranslationService.CreateLanguage(langName, langCode);
            RefreshLanguages();
            _cmbLanguage.SelectedValue = langCode;
            MessageBox.Show(TranslationService.Get("LangCreated", langName),
                TranslationService.Get("Completed"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(TranslationService.Get("LangCreateError", ex.Message),
                TranslationService.Get("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnEditLang_Click(object s, System.Windows.RoutedEventArgs e)
    {
        string code = _cmbLanguage.SelectedValue?.ToString() ?? "";
        if (string.IsNullOrEmpty(code)) return;

        string name = TranslationService.GetLanguageName(code);
        var editor = new LanguageEditorDialog(code, name)
        {
            Owner = this
        };
        if (editor.ShowDialog() == true)
        {
            // Reload translations if editing current language
            if (code == TranslationService.CurrentLanguage)
                TranslationService.Load(code);
            RefreshLanguages();
            _cmbLanguage.SelectedValue = code;
        }
    }

    private void BtnDeleteLang_Click(object s, System.Windows.RoutedEventArgs e)
    {
        string code = _cmbLanguage.SelectedValue?.ToString() ?? "";
        if (string.IsNullOrEmpty(code)) return;

        if (!TranslationService.IsUserLanguage(code))
        {
            MessageBox.Show(TranslationService.Get("LangDeleteBuiltIn"),
                TranslationService.Get("WarningTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string name = TranslationService.GetLanguageName(code);
        if (MessageBox.Show(TranslationService.Get("LangDeleteConfirm", name),
                TranslationService.Get("Confirmation"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        TranslationService.DeleteLanguage(code);
        RefreshLanguages();
        _cmbLanguage.SelectedValue = "en";
    }

    private void BtnImportLang_Click(object s, System.Windows.RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = TranslationService.Get("LangFileFilter"),
            Title  = TranslationService.Get("ImportLangTitle")
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            TranslationService.ImportLanguage(dlg.FileName);
            // Refresh ComboBox
            _cmbLanguage.Items.Clear();
            foreach (var (code, name) in TranslationService.GetAvailableLanguages())
                _cmbLanguage.Items.Add(new { Label = name, Value = code });
            _cmbLanguage.SelectedValue = TranslationService.CurrentLanguage;
            MessageBox.Show(TranslationService.Get("ImportLangDone", Path.GetFileNameWithoutExtension(dlg.FileName).Replace("strings.", "")),
                TranslationService.Get("Completed"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(TranslationService.Get("ImportLangError", ex.Message),
                TranslationService.Get("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
