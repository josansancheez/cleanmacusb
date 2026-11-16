using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using CleanMacUSB.Models;
using CleanMacUSB.Services;
using CleanMacUSB.Views;
using Forms = System.Windows.Forms;

namespace CleanMacUSB;

public partial class MainWindow : Window
{
    private const int WM_DEVICECHANGE = 0x0219;
    private const int DBT_DEVICEARRIVAL = 0x8000;
    private const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

    public LocalizationService Loc => LocalizationService.Instance;

    public ObservableCollection<UsbDriveItem> Drives { get; } = new();
    public ObservableCollection<CleanLogEntry> LogEntries { get; } = new();

    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ToolStripMenuItem? _trayOpenItem;
    private Forms.ToolStripMenuItem? _trayCleanItem;
    private Forms.ToolStripMenuItem? _trayExitItem;
    private bool _forceExit;
    private bool _isCleaning;

    private string _lastCleanedDriveName = string.Empty;
    private CleanSummary? _lastCleanSummary;
    private HashSet<string> _knownDriveRoots = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        UsbDrivesComboBox.ItemsSource = Drives;
        LogListView.ItemsSource = LogEntries;

        InitializeLanguageSelector();
        InitializeSystemTray();
        UpdateSettingsPanelTexts();

        Loc.LanguageChanged += OnLanguageChanged;

        RefreshUsbDrives(isInitialLoad: true);
        BottomStatusText.Text = Loc["StatusReady"];
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private void InitializeLanguageSelector()
    {
        LanguageComboBox.SelectedIndex = Loc.IsSpanish ? 0 : 1;
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            Loc.CurrentCulture = tag;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Actualizar textos de unidades USB en tiempo real
        foreach (var d in Drives)
        {
            d.RefreshLocalization();
        }
        UsbDrivesComboBox.Items.Refresh();

        // Actualizar entradas del log en tiempo real
        foreach (var entry in LogEntries)
        {
            entry.RefreshLocalization();
        }
        LogListView.Items.Refresh();

        // Actualizar encabezados de columnas del GridView
        if (LogListView.View is GridView gv && gv.Columns.Count >= 3)
        {
            gv.Columns[0].Header = Loc["LogColType"];
            gv.Columns[1].Header = Loc["LogColPath"];
            gv.Columns[2].Header = Loc["LogColSize"];
        }

        // Actualizar resumen del log si está visible
        if (_lastCleanSummary != null && !string.IsNullOrEmpty(_lastCleanedDriveName))
        {
            UpdateLogSummaryTexts(_lastCleanedDriveName, _lastCleanSummary);
        }

        // Actualizar textos del apartado de Ajustes y de la bandeja del sistema
        UpdateSettingsPanelTexts();
        UpdateTrayLocalization();

        BottomStatusText.Text = Loc["StatusReady"];
    }

    // ==================== APARTADO DE AJUSTES (SETTINGS) ====================

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCleaning) return;

        if (SettingsPanel.Visibility == Visibility.Visible)
        {
            SettingsPanel.Visibility = Visibility.Collapsed;
            MainConfigPanel.Visibility = Visibility.Visible;
        }
        else
        {
            UpdateSettingsPanelTexts();
            MainConfigPanel.Visibility = Visibility.Collapsed;
            LogActivityPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Visible;
        }
    }

    private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        MainConfigPanel.Visibility = Visibility.Visible;
    }

    private void ToggleBackgroundTray_Changed(object sender, RoutedEventArgs e)
    {
        UpdateTrayStatusIndicator();
    }

    private void UpdateSettingsPanelTexts()
    {
        if (SettingsEulaTextBox != null)
        {
            SettingsEulaTextBox.Text = Loc.GetEulaText();
        }
        UpdateTrayStatusIndicator();
    }

    private void UpdateTrayStatusIndicator()
    {
        if (TrayStatusIndicatorText == null || ToggleBackgroundTray == null) return;

        bool isEnabled = ToggleBackgroundTray.IsChecked == true;
        TrayStatusIndicatorText.Text = isEnabled
            ? Loc["SettingsTrayStatusOn"]
            : Loc["SettingsTrayStatusOff"];

        TrayStatusIndicatorText.Foreground = isEnabled
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74))   // Verde activo
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139)); // Gris inactivo
    }

    // ==================== DETECCIÓN AUTOMÁTICA DE USB (WM_DEVICECHANGE) ====================

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DEVICECHANGE)
        {
            int eventType = wParam.ToInt32();
            if (eventType == DBT_DEVICEARRIVAL || eventType == DBT_DEVICEREMOVECOMPLETE)
            {
                Dispatcher.BeginInvoke(new Action(() => HandleUsbDeviceChange(eventType == DBT_DEVICEARRIVAL)));
            }
        }
        return IntPtr.Zero;
    }

    private void HandleUsbDeviceChange(bool isArrival)
    {
        var previousRoots = new HashSet<string>(_knownDriveRoots, StringComparer.OrdinalIgnoreCase);
        RefreshUsbDrives(isInitialLoad: false);

        if (isArrival)
        {
            var newDrive = Drives.FirstOrDefault(d => !previousRoots.Contains(d.DriveRoot));
            if (newDrive != null)
            {
                UsbDrivesComboBox.SelectedItem = newDrive;
                BottomStatusText.Text = Loc.Format("StatusNewUsbConnected", newDrive.DisplayName);

                // Si está ejecutándose en segundo plano (Bandeja del sistema), mostrar notificación nativa y preguntar
                if (ToggleBackgroundTray.IsChecked == true && !IsVisible)
                {
                    _notifyIcon?.ShowBalloonTip(
                        4500,
                        Loc.Format("TrayUsbDetectedTitle", newDrive.DriveRoot),
                        Loc.Format("TrayUsbDetectedBody", newDrive.DisplayName),
                        Forms.ToolTipIcon.Info);

                    var answer = System.Windows.MessageBox.Show(
                        Loc.Format("TrayUsbPromptQuestion", newDrive.DisplayName),
                        "CleanMacUSB",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question,
                        MessageBoxResult.Yes,
                        System.Windows.MessageBoxOptions.DefaultDesktopOnly);

                    if (answer == MessageBoxResult.Yes)
                    {
                        RestoreFromTray();
                        UsbDrivesComboBox.SelectedItem = newDrive;
                        _ = ExecuteCleaningAsync(newDrive);
                    }
                }
            }
        }
        else
        {
            BottomStatusText.Text = Loc["StatusUsbRemoved"];
        }
    }

    private void RefreshDrivesButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshUsbDrives(isInitialLoad: false);
        BottomStatusText.Text = Loc.Format("StatusDrivesUpdated", Drives.Count);
    }

    private void RefreshUsbDrives(bool isInitialLoad)
    {
        string? previousSelectedRoot = (UsbDrivesComboBox.SelectedItem as UsbDriveItem)?.DriveRoot;

        var detected = UsbDriveService.GetConnectedUsbDrives();
        Drives.Clear();
        _knownDriveRoots.Clear();

        foreach (var drive in detected)
        {
            Drives.Add(drive);
            _knownDriveRoots.Add(drive.DriveRoot);
        }

        if (Drives.Count > 0)
        {
            NoDrivesWarningText.Visibility = Visibility.Collapsed;
            UsbDrivesComboBox.IsEnabled = true;
            CleanUsbButton.IsEnabled = !_isCleaning;
            EjectUsbButton.IsEnabled = !_isCleaning;

            var match = Drives.FirstOrDefault(d => string.Equals(d.DriveRoot, previousSelectedRoot, StringComparison.OrdinalIgnoreCase));
            UsbDrivesComboBox.SelectedItem = match ?? Drives[0];
        }
        else
        {
            NoDrivesWarningText.Visibility = Visibility.Visible;
            UsbDrivesComboBox.IsEnabled = false;
            CleanUsbButton.IsEnabled = false;
            EjectUsbButton.IsEnabled = false;
        }
    }

    // ==================== LIMPIEZA RECURSIVA Y LOG EN TIEMPO REAL ====================

    private async void CleanUsbButton_Click(object sender, RoutedEventArgs e)
    {
        if (UsbDrivesComboBox.SelectedItem is not UsbDriveItem selectedDrive)
        {
            System.Windows.MessageBox.Show(
                this,
                Loc["MsgSelectDriveBody"],
                Loc["MsgSelectDriveTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        await ExecuteCleaningAsync(selectedDrive);
    }

    private async Task ExecuteCleaningAsync(UsbDriveItem targetDrive)
    {
        if (_isCleaning) return;

        bool cleanAppleDouble = ChkAppleDouble.IsChecked == true;
        bool cleanDsStore = ChkDsStore.IsChecked == true;
        bool cleanSpotlight = ChkSpotlightTrash.IsChecked == true;

        if (!cleanAppleDouble && !cleanDsStore && !cleanSpotlight)
        {
            System.Windows.MessageBox.Show(
                this,
                Loc["MsgSelectOptionBody"],
                Loc["MsgSelectOptionTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _isCleaning = true;
        _lastCleanedDriveName = targetDrive.DisplayName;
        _lastCleanSummary = null;

        // Mostrar pestaña / modal de Registro de Actividad (Log)
        SettingsPanel.Visibility = Visibility.Collapsed;
        MainConfigPanel.Visibility = Visibility.Collapsed;
        LogActivityPanel.Visibility = Visibility.Visible;

        SaveLogButton.IsEnabled = false;
        CloseLogButton.IsEnabled = false;
        CleanProgressBar.Value = 0;
        LogEntries.Clear();

        LogProgressStatusText.Text = Loc.Format("LogStatusScanning", targetDrive.DisplayName);
        LogSummaryTextBlock.Text = Loc.Format("LogStartHeader", targetDrive.DriveRoot, targetDrive.FileSystem);

        var options = new CleanOptions
        {
            CleanAppleDouble = cleanAppleDouble,
            CleanDsStore = cleanDsStore,
            CleanSpotlightAndTrash = cleanSpotlight
        };

        var logProgress = new Progress<CleanLogEntry>(entry =>
        {
            LogEntries.Add(entry);
            if (LogEntries.Count > 0)
            {
                LogListView.ScrollIntoView(entry);
            }
        });

        var percentProgress = new Progress<double>(pct =>
        {
            CleanProgressBar.Value = pct;
        });

        try
        {
            var summary = await MacJunkCleanerService.CleanDriveAsync(
                targetDrive.DriveRoot,
                options,
                logProgress,
                percentProgress);

            _lastCleanSummary = summary;

            if (summary.DeletedFilesCount == 0 && summary.DeletedFoldersCount == 0)
            {
                LogEntries.Add(new CleanLogEntry
                {
                    Kind = CleanItemKind.Info,
                    FullPath = Loc["LogNoItemsMessage"],
                    SizeBytes = 0
                });
            }

            UpdateLogSummaryTexts(targetDrive.DisplayName, summary);
        }
        finally
        {
            _isCleaning = false;
            SaveLogButton.IsEnabled = true;
            CloseLogButton.IsEnabled = true;
            RefreshUsbDrives(isInitialLoad: false);
        }

        // Si el usuario marcó "Expulsar automáticamente al terminar la limpieza"
        if (ChkAutoEject.IsChecked == true)
        {
            PerformSafeEject(targetDrive);
        }
    }

    private void UpdateLogSummaryTexts(string driveDisplay, CleanSummary summary)
    {
        if (summary.DeletedFilesCount == 0 && summary.DeletedFoldersCount == 0)
        {
            LogProgressStatusText.Text = Loc.Format("LogStatusNothingFound", driveDisplay);
        }
        else
        {
            LogProgressStatusText.Text = Loc.Format("LogStatusCompleted", driveDisplay);
        }

        LogSummaryTextBlock.Text = Loc.Format(
            "LogSummaryFormat",
            summary.DeletedFilesCount,
            summary.DeletedFoldersCount,
            UsbDriveItem.FormatBytes(summary.TotalBytesFreed));

        BottomStatusText.Text = LogSummaryTextBlock.Text;
    }

    private void SaveLogButton_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = Loc["SaveLogDialogTitle"],
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = $"CleanMacUSB_Log_{DateTime.Now:yyyyMMdd_HHmm}.txt",
            DefaultExt = ".txt"
        };

        if (sfd.ShowDialog(this) == true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine(Loc["LogFileHeaderTitle"]);
            sb.AppendLine("================================================================================");
            sb.AppendLine($"{Loc["LogFileDate"]}: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"{Loc["LogFileDrive"]}: {_lastCleanedDriveName}");
            sb.AppendLine("--------------------------------------------------------------------------------");

            foreach (var entry in LogEntries)
            {
                sb.AppendLine($"[{entry.TimeText}] [{entry.TypeDisplay,-8}] {entry.FullPath} ({entry.SizeDisplay})");
            }

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine(LogSummaryTextBlock.Text);
            sb.AppendLine("================================================================================");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);

            System.Windows.MessageBox.Show(
                this,
                Loc.Format("SaveLogSuccessMsg", sfd.FileName),
                Loc["SaveLogSuccessTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void CloseLogButton_Click(object sender, RoutedEventArgs e)
    {
        LogActivityPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Collapsed;
        MainConfigPanel.Visibility = Visibility.Visible;
        RefreshUsbDrives(isInitialLoad: false);
    }

    // ==================== EXPULSIÓN SEGURA DE USB ====================

    private void EjectUsbButton_Click(object sender, RoutedEventArgs e)
    {
        if (UsbDrivesComboBox.SelectedItem is not UsbDriveItem selectedDrive)
        {
            System.Windows.MessageBox.Show(
                this,
                Loc["MsgSelectDriveBody"],
                Loc["MsgSelectDriveTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        PerformSafeEject(selectedDrive);
    }

    private void PerformSafeEject(UsbDriveItem drive)
    {
        bool ejected = UsbDriveService.SafelyEjectDrive(drive.DriveLetter, out string errorDetail);
        if (ejected)
        {
            System.Windows.MessageBox.Show(
                this,
                Loc.Format("MsgEjectSuccessBody", drive.DisplayName),
                Loc["MsgEjectSuccessTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            RefreshUsbDrives(isInitialLoad: false);
        }
        else
        {
            System.Windows.MessageBox.Show(
                this,
                Loc.Format("MsgEjectFailBody", drive.DisplayName, errorDetail),
                Loc["MsgEjectFailTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    // ==================== BANDEJA DEL SISTEMA (SYSTEM TRAY) ====================

    private void InitializeSystemTray()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Visible = false,
            Text = Loc["TrayTooltip"]
        };

        try
        {
            var iconUri = new Uri("pack://application:,,,/Assets/CleanMacUSB.ico", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                _notifyIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
            }
        }
        catch
        {
            // Fallback al icono del ejecutable
        }

        var contextMenu = new Forms.ContextMenuStrip();
        _trayOpenItem = new Forms.ToolStripMenuItem(Loc["TrayMenuOpen"], null, (_, _) => RestoreFromTray());
        _trayCleanItem = new Forms.ToolStripMenuItem(Loc["TrayMenuCleanNow"], null, (_, _) =>
        {
            RestoreFromTray();
            if (UsbDrivesComboBox.SelectedItem is UsbDriveItem drive)
            {
                _ = ExecuteCleaningAsync(drive);
            }
        });
        _trayExitItem = new Forms.ToolStripMenuItem(Loc["TrayMenuExit"], null, (_, _) =>
        {
            _forceExit = true;
            Close();
        });

        contextMenu.Items.Add(_trayOpenItem);
        contextMenu.Items.Add(_trayCleanItem);
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(_trayExitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
        _notifyIcon.BalloonTipClicked += (_, _) =>
        {
            RestoreFromTray();
            if (UsbDrivesComboBox.SelectedItem is UsbDriveItem drive && !_isCleaning)
            {
                _ = ExecuteCleaningAsync(drive);
            }
        };
    }

    private void UpdateTrayLocalization()
    {
        if (_notifyIcon == null) return;
        _notifyIcon.Text = Loc["TrayTooltip"];
        if (_trayOpenItem != null) _trayOpenItem.Text = Loc["TrayMenuOpen"];
        if (_trayCleanItem != null) _trayCleanItem.Text = Loc["TrayMenuCleanNow"];
        if (_trayExitItem != null) _trayExitItem.Text = Loc["TrayMenuExit"];
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceExit && ToggleBackgroundTray.IsChecked == true && _notifyIcon != null)
        {
            e.Cancel = true;
            _notifyIcon.Visible = true;
            Hide();
            _notifyIcon.ShowBalloonTip(
                3000,
                Loc["TrayMinimizedTitle"],
                Loc["TrayMinimizedBody"],
                Forms.ToolTipIcon.Info);
            return;
        }

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }

    // ==================== VENTANA ACERCA DE (ABOUT) ====================

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }
}
