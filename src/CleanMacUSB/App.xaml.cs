using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CleanMacUSB.Models;
using CleanMacUSB.Services;

namespace CleanMacUSB;

public partial class App : System.Windows.Application
{
    public static string? InitialTargetPath { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        for (int i = 0; i < e.Args.Length; i++)
        {
            var arg = e.Args[i];
            if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
            {
                var langCode = arg.Substring("--lang=".Length).Trim();
                LocalizationService.Instance.CurrentCulture = langCode;
            }
            else if (string.Equals(arg, "--target", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                InitialTargetPath = e.Args[i + 1].Trim().Trim('"');
                i++;
            }
            else if (string.Equals(arg, "--headless-clean", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                string targetPath = e.Args[i + 1];
                bool simulateAbort = e.Args.Any(a => string.Equals(a, "--simulate-abort", StringComparison.OrdinalIgnoreCase));

                using var cts = new CancellationTokenSource();
                if (simulateAbort)
                {
                    cts.Cancel();
                }

                var summary = await MacJunkCleanerService.CleanDriveAsync(
                    targetPath,
                    new CleanOptions(),
                    new Progress<CleanLogEntry>(),
                    new Progress<double>(),
                    cts.Token);

                string resultFile = Path.Combine(Path.GetTempPath(), "cleanmacusb_last_test.json");
                File.WriteAllText(resultFile, JsonSerializer.Serialize(summary));
                Shutdown(0);
                return;
            }
        }

        base.OnStartup(e);
    }

    public static void ApplyTheme(bool isDark)
    {
        if (Current == null) return;
        var res = Current.Resources;

        if (isDark)
        {
            res["AppBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            res["CardBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
            res["CardBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85));
            res["SubCardBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 33, 50));
            res["SubCardBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85));
            res["SecondaryBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85));
            res["SecondaryHoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
            res["SecondaryBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
            res["TextPrimaryBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249));
            res["TextSecondaryBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
            res["StatusBarBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
            res["StatusBarTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
            res["EulaBoxBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            res["EulaTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
            res["GroupHeaderBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
            res["GroupHeaderTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
            res["SummaryBannerBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(12, 74, 110));
            res["SummaryBannerBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(3, 105, 161));
            res["SummaryBannerTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 242, 254));
        }
        else
        {
            res["AppBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 245, 248));
            res["CardBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
            res["CardBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
            res["SubCardBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));
            res["SubCardBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
            res["SecondaryBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249));
            res["SecondaryHoverBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
            res["SecondaryBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
            res["TextPrimaryBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            res["TextSecondaryBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
            res["StatusBarBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
            res["StatusBarTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85));
            res["EulaBoxBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
            res["EulaTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85));
            res["GroupHeaderBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249));
            res["GroupHeaderTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(3, 105, 161));
            res["SummaryBannerBgBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 249, 255));
            res["SummaryBannerBorderBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(186, 230, 253));
            res["SummaryBannerTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(3, 105, 161));
        }
    }
}
