using System.Windows;
using System.Windows.Controls;
using EagleManager.Models;

namespace EagleManager.Views;

/// <summary>Dialog for creating or editing a Project.</summary>
public class ProjectEditDialog : Window
{
    public Project? Result { get; private set; }

    private readonly TextBox _txName, _txBoard, _txRev, _txVer, _txDesc, _txNotes;

    public ProjectEditDialog(Project? existing)
    {
        Title  = existing == null ? "Nov projekt" : "Uredi projekt";
        Width  = 480; Height = 500;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.Resources["BgBrush"],
            Padding    = new Thickness(20)
        };

        var stack = new StackPanel();

        // ── Fields ────────────────────────────────────────────────────────
        stack.Children.Add(Label("Ime projekta *"));
        _txName  = Tb(); stack.Children.Add(_txName);

        stack.Children.Add(Label("Ime PCB (Eagle board)"));
        _txBoard = Tb(); stack.Children.Add(_txBoard);

        // Version + Revision on same row
        var verRevGrid = new Grid();
        verRevGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        verRevGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var verSp = new StackPanel { Margin = new Thickness(0, 0, 4, 0) };
        verSp.Children.Add(Label("Verzija (npr. V1, V2)"));
        _txVer = Tb();
        verSp.Children.Add(_txVer);
        var revSp = new StackPanel { Margin = new Thickness(4, 0, 0, 0) };
        revSp.Children.Add(Label("Revizija (npr. A, B)"));
        _txRev = Tb();
        revSp.Children.Add(_txRev);
        Grid.SetColumn(verSp, 0); verRevGrid.Children.Add(verSp);
        Grid.SetColumn(revSp, 1); verRevGrid.Children.Add(revSp);
        stack.Children.Add(verRevGrid);

        stack.Children.Add(Label("Kratek opis"));
        _txDesc  = Tb(); stack.Children.Add(_txDesc);

        stack.Children.Add(Label("Opombe"));
        _txNotes = new TextBox
        {
            AcceptsReturn       = true,
            TextWrapping        = TextWrapping.Wrap,
            Height              = 60,
            Margin              = new Thickness(0, 0, 0, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        stack.Children.Add(_txNotes);

        // ── Prefill ───────────────────────────────────────────────────────
        if (existing != null)
        {
            _txName.Text  = existing.Name;
            _txBoard.Text = existing.BoardName;
            _txVer.Text   = existing.Version;
            _txRev.Text   = existing.Revision;
            _txDesc.Text  = existing.Description;
            _txNotes.Text = existing.Notes;
        }

        // ── Buttons ───────────────────────────────────────────────────────
        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(0, 6, 0, 0)
        };

        var btnCancel = new Button { Content = "Prekliči", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 6, 0) };
        var btnSave   = new Button { Content = "💾 Shrani", Padding = new Thickness(18, 7, 18, 7) };
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

    private void Save(Project? existing)
    {
        if (string.IsNullOrWhiteSpace(_txName.Text))
        {
            MessageBox.Show("Ime projekta je obvezno.", "Napaka",
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

        DialogResult = true;
        Close();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TextBlock Label(string text) => new()
    {
        Text       = text,
        Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SubTextBrush"],
        FontSize   = 11,
        Margin     = new Thickness(0, 8, 0, 2)
    };

    private static TextBox Tb() => new() { Margin = new Thickness(0, 0, 0, 2) };
}
