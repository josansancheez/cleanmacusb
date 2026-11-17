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

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch { }
    }

    private void DonateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://paypal.me/josansancheez",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
