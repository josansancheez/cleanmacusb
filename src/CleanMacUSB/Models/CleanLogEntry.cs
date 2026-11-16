using System.ComponentModel;
using CleanMacUSB.Services;

namespace CleanMacUSB.Models;

public enum CleanItemKind
{
    File,
    Folder,
    Info,
    Notice
}

/// <summary>
/// Entrada individual del Registro de Actividad (Log) en tiempo real.
/// </summary>
public sealed class CleanLogEntry : INotifyPropertyChanged
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public CleanItemKind Kind { get; init; }
    public string FullPath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TimeText => Timestamp.ToString("HH:mm:ss");

    public string TypeDisplay
    {
        get
        {
            var loc = LocalizationService.Instance;
            return Kind switch
            {
                CleanItemKind.File => loc["LogTypeFile"],
                CleanItemKind.Folder => loc["LogTypeFolder"],
                CleanItemKind.Notice => loc["LogTypeError"],
                _ => loc["LogTypeInfo"]
            };
        }
    }

    public string SizeDisplay =>
        Kind is CleanItemKind.File or CleanItemKind.Folder
            ? UsbDriveItem.FormatBytes(SizeBytes)
            : "—";

    public void RefreshLocalization()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TypeDisplay)));
    }
}
