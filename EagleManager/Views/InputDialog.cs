using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EagleManager.Views;

/// <summary>Generic single-line text/number input dialog.</summary>
public class InputDialog : Window
{
    private readonly TextBox _input;
    public string InputValue => _input.Text.Trim();

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        Title  = title;
        Width  = 420; Height = 210;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.Resources["BgBrush"],
            Padding    = new Thickness(20)
        };

        var stack = new StackPanel();

        stack.Children.Add(new TextBlock
        {
            Text       = prompt,
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TextBrush"],
            Margin     = new Thickness(0, 0, 0, 8)
        });

        _input = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 16) };
        _input.Loaded += (_, _) => { _input.Focus(); _input.SelectAll(); };
        _input.KeyDown += (_, e) => { if (e.Key == Key.Enter) Confirm(); };
        stack.Children.Add(_input);

        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var btnCancel = new Button { Content = "Prekliči", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 6, 0) };
        var btnOk     = new Button { Content = "OK",       Padding = new Thickness(20, 6, 20, 6) };
        btnOk.Style   = (Style)Application.Current.Resources["AccentButton"];
        btnCancel.Click += (_, _) => { DialogResult = false; Close(); };
        btnOk.Click     += (_, _) => Confirm();

        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnOk);
        stack.Children.Add(btnRow);

        outer.Child = stack;
        Content     = outer;
    }

    private void Confirm()
    {
        DialogResult = true;
        Close();
    }
}
