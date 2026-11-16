using System.Windows;
using CleanMacUSB.Services;

namespace CleanMacUSB.Views;

public partial class AboutWindow : Window
{
    public LocalizationService Loc => LocalizationService.Instance;

    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;
        EulaTextBox.Text = Loc.GetEulaText();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
