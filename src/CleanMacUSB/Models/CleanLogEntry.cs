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
    private bool _isSelected = true;

    public DateTime Timestamp { get; init; } = DateTime.Now;
    public CleanItemKind Kind { get; init; }
    public string FullPath { get; init; } = string.Empty;
    public long SizeBytes { get; init; }

    public bool CanSelect => Kind is CleanItemKind.File or CleanItemKind.Folder;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

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
