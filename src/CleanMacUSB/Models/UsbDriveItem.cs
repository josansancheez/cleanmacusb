using System.ComponentModel;
using CleanMacUSB.Services;

namespace CleanMacUSB.Models;

/// <summary>
/// Representa una unidad USB extraíble o disco externo conectado al sistema.
/// </summary>
public sealed class UsbDriveItem : INotifyPropertyChanged
{
    public string DriveRoot { get; init; } = string.Empty;      // Ej. "E:\"
    public char DriveLetter { get; init; }                      // Ej. 'E'
    public string VolumeLabel { get; init; } = string.Empty;    // Ej. "MI_USB"
    public string FileSystem { get; init; } = string.Empty;     // Ej. "exFAT"
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName
    {
        get
        {
            var loc = LocalizationService.Instance;
            var label = string.IsNullOrWhiteSpace(VolumeLabel) ? loc["NoLabel"] : VolumeLabel;
            return loc.Format(
                "DriveFormatPattern",
                DriveRoot,
                label,
                FormatBytes(FreeBytes),
                FormatBytes(TotalBytes),
                FileSystem
            );
        }
    }

    public void RefreshLocalization()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024)
            return $"{kb:0.0} KB";
        double mb = kb / 1024.0;
        if (mb < 1024)
            return $"{mb:0.00} MB";
        double gb = mb / 1024.0;
        return $"{gb:0.1} GB";
    }

    public override string ToString() => DisplayName;
}
