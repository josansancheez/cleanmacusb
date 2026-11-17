using System.Diagnostics;
using System.Windows;
using CleanMacUSB.Models;
using CleanMacUSB.Services;

namespace CleanMacUSB.Views;

public partial class CleanCompletedWindow : Window
{
    public LocalizationService Loc => LocalizationService.Instance;

    public CleanCompletedWindow(string driveDisplayName, CleanSummary summary)
    {
        InitializeComponent();
        DataContext = this;

        DriveNameTextBlock.Text = driveDisplayName;
        StatsSummaryTextBlock.Text = Loc.Format(
            "CleanDoneSubheading",
            summary.DeletedFilesCount,
            summary.DeletedFoldersCount,
            UsbDriveItem.FormatBytes(summary.TotalBytesFreed));
    }

    private void SupportPayPalButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://paypal.me/josansancheez",
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignorar si no hay navegador predeterminado
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
