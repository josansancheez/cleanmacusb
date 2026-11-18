using System.Diagnostics;
using Microsoft.Win32;

namespace CleanMacUSB.Services;

/// <summary>
/// Gestiona la integración en el menú contextual del Explorador de Windows (HKCU\Software\Classes, sin permisos de administrador)
/// para hacer clic derecho sobre cualquier unidad o carpeta -> "Limpiar con CleanMacUSB".
/// </summary>
public static class ContextMenuService
{
    private const string DriveShellKey = @"Software\Classes\Drive\shell\CleanMacUSB";
    private const string DirectoryShellKey = @"Software\Classes\Directory\shell\CleanMacUSB";

    public static bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DriveShellKey);
            return key != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled, string menuText)
    {
        try
        {
            if (!enabled)
            {
                Registry.CurrentUser.DeleteSubKeyTree(DriveShellKey, throwOnMissingSubKey: false);
                Registry.CurrentUser.DeleteSubKeyTree(DirectoryShellKey, throwOnMissingSubKey: false);
                return;
            }

            string? exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(exePath)) return;

            RegisterShellEntry(DriveShellKey, menuText, exePath, "\"%1\"");
            RegisterShellEntry(DirectoryShellKey, menuText, exePath, "\"%V\"");
        }
        catch
        {
            // Ignorar si la política del sistema restringe escritura en HKCU\Software\Classes
        }
    }

    private static void RegisterShellEntry(string subKeyPath, string menuText, string exePath, string targetPlaceholder)
    {
        using var shellKey = Registry.CurrentUser.CreateSubKey(subKeyPath);
        shellKey.SetValue(string.Empty, menuText);
        shellKey.SetValue("Icon", $"\"{exePath}\",0");

        using var cmdKey = shellKey.CreateSubKey("command");
        cmdKey.SetValue(string.Empty, $"\"{exePath}\" --target {targetPlaceholder}");
    }
}
