using System.IO;
using System.Text.Json;
using System.Windows;
using CleanMacUSB.Models;
using CleanMacUSB.Services;

namespace CleanMacUSB;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        // Soportar parámetro opcional de idioma desde el instalador (--lang=es o --lang=en)
        for (int i = 0; i < e.Args.Length; i++)
        {
            var arg = e.Args[i];
            if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
            {
                var langCode = arg.Substring("--lang=".Length).Trim();
                LocalizationService.Instance.CurrentCulture = langCode;
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
}
