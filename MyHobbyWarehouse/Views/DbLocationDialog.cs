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

    private readonly TextBox _txPath;

    public DbLocationDialog(string? currentPath)
    {
        Title  = "Lokacija baze podatkov";
        Width  = 560; Height = 440;
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
            Text       = "Izberi lokacijo baze podatkov",
            FontSize   = 16,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 6)
        };
        title.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        stack.Children.Add(title);

        var sub = new TextBlock
        {
            Text       = "Baza (SQLite .db datoteka) se ustvari avtomatično, če še ne obstaja.",
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
            Content = "📂 Brskaj…",
            Padding = new Thickness(10, 5, 10, 5),
            Margin  = new Thickness(0, 0, 6, 0)
        };
        btnBrowse.Click += BrowseExisting_Click;
        Grid.SetColumn(btnBrowse, 1);
        pathRow.Children.Add(btnBrowse);

        var btnDefault = new Button
        {
            Content = "Privzeto",
            Padding = new Thickness(10, 5, 10, 5)
        };
        btnDefault.Click += (_, _) => _txPath.Text = SettingsService.DefaultDbPath;
        Grid.SetColumn(btnDefault, 2);
        pathRow.Children.Add(btnDefault);

        stack.Children.Add(pathRow);

        // Info label showing default path
        var info = new TextBlock
        {
            Text     = $"Privzeto: {SettingsService.DefaultDbPath}",
            FontSize = 10,
            Margin   = new Thickness(0, 0, 0, 16)
        };
        info.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        stack.Children.Add(info);

        // Buttons
        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var btnCancel = new Button
        {
            Content = "Prekliči",
            Padding = new Thickness(12, 7, 12, 7),
            Margin  = new Thickness(0, 0, 8, 0)
        };
        btnCancel.Click += (_, _) => { DialogResult = false; Close(); };

        var btnOk = new Button
        {
            Content = "✔ Potrdi",
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
            Text       = "Uvoz knjižnice komponent",
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 4)
        };
        importHdr.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        stack.Children.Add(importHdr);

        var importSub = new TextBlock
        {
            Text     = "Enkratni uvoz base.ods v SQLite bazo (nadomesti obstoječe komponente).",
            FontSize = 11,
            Margin   = new Thickness(0, 0, 0, 8)
        };
        importSub.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
        stack.Children.Add(importSub);

        var btnImport = new Button
        {
            Content = "📥 Uvozi base.ods …",
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
            Filter = "ODS datoteke|*.ods|Vse datoteke|*.*",
            Title  = "Izberi base.ods"
        };
        if (dlg.ShowDialog() != true) return;

        System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
        try
        {
            var (components, errors) = Services.ImportService.ImportBaseOds(dlg.FileName);

            if (errors.Count > 0 && components.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "(" + errors.Count + " napak)", "Napake pri uvozu",
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

            string msg = $"Uvoženo {imported} komponent.";
            if (errors.Count > 0)
                msg += $"Opozorila ({errors.Count}):" + string.Join("", errors.Take(20));
            ComponentsImported = true;
            System.Windows.MessageBox.Show(msg, "Uvoz dokončan",
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
            Title            = "Izberi ali ustvari datoteko baze",
            Filter           = "SQLite baza|*.db|Vse datoteke|*.*",
            FileName         = Path.GetFileName(_txPath.Text),
            InitialDirectory = Path.GetDirectoryName(_txPath.Text)
                               ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            OverwritePrompt  = false   // don't warn — opening existing DB is intentional
        };
        if (dlg.ShowDialog() == true)
            _txPath.Text = dlg.FileName;
    }

    private void Confirm_Click(object s, RoutedEventArgs e)
    {
        string path = _txPath.Text.Trim();

        if (string.IsNullOrEmpty(path))
        {
            MessageBox.Show("Pot ne sme biti prazna.", "Napaka",
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
            MessageBox.Show("Neveljavna pot.", "Napaka",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try { Directory.CreateDirectory(dir); }
        catch (Exception ex)
        {
            MessageBox.Show($"Mapa ne more biti ustvarjena: {ex.Message}", "Napaka",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        SelectedPath = path;
        DialogResult = true;
        Close();
    }
}
