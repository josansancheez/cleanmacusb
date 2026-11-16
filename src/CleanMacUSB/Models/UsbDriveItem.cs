using System.ComponentModel;
using CleanMacUSB.Services;

namespace CleanMacUSB.Models;

public enum CleanTargetKind
{
    UsbDrive,
    InternalDrive,
    CustomFolder
}

/// <summary>
/// Representa un destino de limpieza: Unidad USB extraíble, Disco duro interno o Carpeta específica.
/// </summary>
public sealed class UsbDriveItem : INotifyPropertyChanged
{
    public CleanTargetKind TargetKind { get; init; } = CleanTargetKind.UsbDrive;
    public string DriveRoot { get; init; } = string.Empty;      // Ej. "E:\" o "C:\Users\...\MiCarpeta"
    public char DriveLetter { get; init; }                      // Ej. 'E'
    public string VolumeLabel { get; init; } = string.Empty;    // Ej. "MI_USB" o nombre de la carpeta
    public string FileSystem { get; init; } = string.Empty;     // Ej. "exFAT", "NTFS", "Carpeta"
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }

    public bool CanEject => TargetKind == CleanTargetKind.UsbDrive;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName
    {
        get
        {
            var loc = LocalizationService.Instance;

            if (TargetKind == CleanTargetKind.CustomFolder)
            {
                return loc.Format("FolderFormatPattern", DriveRoot);
            }

            string prefix = TargetKind == CleanTargetKind.UsbDrive
                ? loc["TagUsbDrive"]
                : loc["TagInternalDrive"];

            var label = string.IsNullOrWhiteSpace(VolumeLabel) ? loc["NoLabel"] : VolumeLabel;
            return $"{prefix} " + loc.Format(
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
