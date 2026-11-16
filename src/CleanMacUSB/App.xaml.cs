using System.Windows;
using CleanMacUSB.Services;

namespace CleanMacUSB;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Soportar parámetro opcional de idioma desde el instalador (--lang=es o --lang=en)
        foreach (var arg in e.Args)
        {
            if (arg.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
            {
                var langCode = arg.Substring("--lang=".Length).Trim();
                LocalizationService.Instance.CurrentCulture = langCode;
            }
        }
    }
}
