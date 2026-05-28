using System.Windows;
using System.Windows.Controls;
using MyHobbyWarehouse.Data;
using MyHobbyWarehouse.Models;
using MyHobbyWarehouse.Services;

namespace MyHobbyWarehouse.Views;

public class AddToProjectDialog : Window
{
    public Project? SelectedProject { get; private set; }
    public double Quantity { get; private set; }
    public string Designators { get; private set; } = "";

    private readonly ComboBox _cmbProject;
    private readonly TextBox _txQty;
    private readonly TextBox _txParts;

    public AddToProjectDialog(DatabaseService db, Component component)
    {
        Title = TranslationService.Get("AddToProjectTitle", component.Sku);
        Width = 440; Height = 360;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var outer = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.Resources["BgBrush"],
            Padding = new Thickness(20)
        };
        var stack = new StackPanel();

        stack.Children.Add(new TextBlock
        {
            Text = TranslationService.Get("AddToProjectComponentInfo", component.Sku, component.Description),
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14),
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"]
        });

        stack.Children.Add(Lbl(TranslationService.Get("AddToProjectProjectLabel")));
        _cmbProject = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 10),
            DisplayMemberPath = "DisplayName"
        };
        var projects = db.GetAllProjects();
        foreach (var p in projects) _cmbProject.Items.Add(p);
        if (projects.Count > 0) _cmbProject.SelectedIndex = 0;
        stack.Children.Add(_cmbProject);

        stack.Children.Add(Lbl(TranslationService.Get("AddToProjectQuantityLabel")));
        _txQty = new TextBox { Text = "1", Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(_txQty);

        stack.Children.Add(Lbl(TranslationService.Get("AddToProjectDesignatorsLabel")));
        _txParts = new TextBox { Margin = new Thickness(0, 0, 0, 14) };
        stack.Children.Add(_txParts);

        var btnRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var btnCancel = new Button { Content = TranslationService.Get("Cancel"), Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 6, 0) };
        var btnSave = new Button { Content = TranslationService.Get("AddToProjectAddButton"), Padding = new Thickness(18, 7, 18, 7) };
        btnSave.Style = (Style)Application.Current.Resources["AccentButton"];
        btnCancel.Click += (_, _) => { DialogResult = false; Close(); };
        btnSave.Click += Save_Click;
        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnSave);
        stack.Children.Add(btnRow);

        outer.Child = stack;
        Content = outer;

        Loaded += (_, _) => _txQty.Focus();
    }

    private void Save_Click(object s, RoutedEventArgs e)
    {
        if (_cmbProject.SelectedItem is not Project p)
        { MessageBox.Show(TranslationService.Get("AddToProjectSelectProjectError"), TranslationService.Get("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        if (!double.TryParse(_txQty.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double qty) || qty <= 0)
        { MessageBox.Show(TranslationService.Get("QtyRequired"), TranslationService.Get("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        SelectedProject = p;
        Quantity = qty;
        Designators = _txParts.Text.Trim();
        DialogResult = true;
        Close();
    }

    private static TextBlock Lbl(string text) => new()
    {
        Text = text,
        Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SubTextBrush"],
        FontSize = 11,
        Margin = new Thickness(0, 6, 0, 2)
    };
}
