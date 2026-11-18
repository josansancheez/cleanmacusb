using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

    private readonly ICollectionView _drivesView;
    private readonly List<UsbDriveItem> _customFolders = new();
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ToolStripMenuItem? _trayOpenItem;
    private Forms.ToolStripMenuItem? _trayCleanItem;
    private Forms.ToolStripMenuItem? _trayExitItem;
    private bool _forceExit;
    private bool _isCleaning;
    private CancellationTokenSource? _cleanCts;

    private string _lastCleanedDriveName = string.Empty;
    private CleanSummary? _lastCleanSummary;
    private HashSet<string> _knownDriveRoots = new(StringComparer.OrdinalIgnoreCase);
    private bool _isCheckingUpdates;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _drivesView = CollectionViewSource.GetDefaultView(Drives);
        _drivesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(UsbDriveItem.GroupCategory)));
        UsbDrivesComboBox.ItemsSource = _drivesView;
        LogListView.ItemsSource = LogEntries;

        InitializeDefaultFolders();
        LoadPersistedUserSettings();
        InitializeLanguageSelector();
        InitializeSystemTray();
        UpdateSettingsPanelTexts();

        Loc.LanguageChanged += OnLanguageChanged;

        string? initialPreferredRoot = ResolveInitialTargetPath();
        RefreshUsbDrives(isInitialLoad: string.IsNullOrEmpty(initialPreferredRoot), preferredRoot: initialPreferredRoot);
        BottomStatusText.Text = Loc["StatusReady"];

        // Si se ejecuta desde la Microsoft Store, ocultar la sección de actualizaciones propias
        if (UpdateService.IsRunningAsMicrosoftStorePackage())
        {
            AutoUpdateSectionPanel.Visibility = Visibility.Collapsed;
        }
        else if (ToggleAutoUpdates.IsChecked == true)
        {
            _ = PerformUpdateCheckAsync(isManualCheck: false);
        }
    }

    private string? ResolveInitialTargetPath()
    {
        if (string.IsNullOrWhiteSpace(App.InitialTargetPath))
            return null;

        string path = App.InitialTargetPath;
        if (Directory.Exists(path))
        {
            var root = Path.GetPathRoot(path);
            if (!string.Equals(path.TrimEnd(Path.DirectorySeparatorChar), root?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                if (!_customFolders.Any(f => string.Equals(f.DriveRoot, path, StringComparison.OrdinalIgnoreCase)))
                {
                    _customFolders.Insert(0, new UsbDriveItem
                    {
                        TargetKind = CleanTargetKind.CustomFolder,
                        DriveRoot = path,
                        DriveLetter = char.ToUpperInvariant(path[0]),
                        VolumeLabel = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)),
                        FileSystem = "Folder"
                    });
                }
            }
            else
            {
                return root;
            }
            return path;
        }
        return null;
    }

    private void LoadPersistedUserSettings()
    {
        var settings = AppSettingsService.Load();
        ToggleAutoUpdates.IsChecked = settings.AutoUpdateEnabled;
        ToggleBackgroundTray.IsChecked = settings.RunInBackground;
        ToggleDarkMode.IsChecked = settings.DarkMode;
        ToggleContextMenu.IsChecked = CleanMacUSB.Services.ContextMenuService.IsRegistered();
        App.ApplyTheme(settings.DarkMode);

        if (!string.IsNullOrWhiteSpace(settings.Language))
        {
            Loc.CurrentCulture = settings.Language;
        }
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
        foreach (ComboBoxItem item in LanguageComboBox.Items)
        {
            if (item.Tag is string tag && string.Equals(tag, Loc.CurrentCulture, StringComparison.OrdinalIgnoreCase))
            {
                LanguageComboBox.SelectedItem = item;
                return;
            }
        }
        LanguageComboBox.SelectedIndex = 1; // English fallback
    }

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            Loc.CurrentCulture = tag;
            if (IsInitialized)
            {
                AppSettingsService.Current.Language = tag;
                AppSettingsService.Save();
            }
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Actualizar textos de unidades/carpetas y encabezados de grupo en tiempo real
        foreach (var d in Drives)
        {
            d.RefreshLocalization();
        }
        _drivesView.Refresh();
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

        // Actualizar textos del apartado de Ajustes, menú contextual (si está activo) y bandeja del sistema
        UpdateSettingsPanelTexts();
        UpdateTrayLocalization();
        if (ToggleContextMenu?.IsChecked == true)
        {
            CleanMacUSB.Services.ContextMenuService.SetEnabled(true, Loc["ContextMenuVerb"]);
        }

        BottomStatusText.Text = Loc["StatusReady"];
    }

    // ==================== APARTADO DE AJUSTES (CON SOMBREADO DE BOTÓN ACTIVO) ====================

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

        UpdateSettingsButtonVisualState();
    }

    private void CloseSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        MainConfigPanel.Visibility = Visibility.Visible;
        UpdateSettingsButtonVisualState();
    }

    /// <summary>
    /// Aplica estilo sombreado/activo al botón Ajustes cuando el usuario se encuentra dentro del apartado de Ajustes.
    /// </summary>
    private void UpdateSettingsButtonVisualState()
    {
        bool isSettingsActive = SettingsPanel.Visibility == Visibility.Visible;
        string styleKey = isSettingsActive ? "FluentActiveHeaderButtonStyle" : "FluentSecondaryButtonStyle";
        if (TryFindResource(styleKey) is Style targetStyle)
        {
            SettingsHeaderButton.Style = targetStyle;
        }
    }

    private void ToggleBackgroundTray_Changed(object sender, RoutedEventArgs e)
    {
        UpdateTrayStatusIndicator();
        if (IsInitialized)
        {
            AppSettingsService.Current.RunInBackground = ToggleBackgroundTray.IsChecked == true;
            AppSettingsService.Save();
        }
    }

    private void ToggleAutoUpdates_Changed(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            AppSettingsService.Current.AutoUpdateEnabled = ToggleAutoUpdates.IsChecked == true;
            AppSettingsService.Save();
        }
    }

    private void ToggleDarkMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        bool isDark = ToggleDarkMode.IsChecked == true;
        App.ApplyTheme(isDark);
        AppSettingsService.Current.DarkMode = isDark;
        AppSettingsService.Save();
    }

    private void ToggleContextMenu_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;
        bool enabled = ToggleContextMenu.IsChecked == true;
        CleanMacUSB.Services.ContextMenuService.SetEnabled(enabled, Loc["ContextMenuVerb"]);
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        await PerformUpdateCheckAsync(isManualCheck: true);
    }

    private async Task PerformUpdateCheckAsync(bool isManualCheck)
    {
        if (_isCheckingUpdates) return;
        _isCheckingUpdates = true;

        try
        {
            if (CheckUpdatesButton != null)
            {
                CheckUpdatesButton.IsEnabled = false;
                CheckUpdatesButton.Content = Loc["BtnCheckingUpdates"];
            }

            if (UpdateStatusTextBlock != null)
            {
                UpdateStatusTextBlock.Text = Loc["UpdateStatusChecking"];
                UpdateStatusTextBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199));
            }

            var result = await UpdateService.CheckForUpdatesAsync(Loc.IsSpanish);

            if (!result.CheckSucceeded)
            {
                if (UpdateStatusTextBlock != null)
                {
                    UpdateStatusTextBlock.Text = isManualCheck ? Loc["UpdateStatusOffline"] : Loc["UpdateStatusIdle"];
                    UpdateStatusTextBlock.Foreground = new SolidColorBrush(
                        isManualCheck
                            ? System.Windows.Media.Color.FromRgb(217, 119, 6)
                            : System.Windows.Media.Color.FromRgb(100, 116, 139));
                }
                return;
            }

            if (result.IsUpdateAvailable)
            {
                if (UpdateStatusTextBlock != null)
                {
                    UpdateStatusTextBlock.Text = Loc.Format("UpdateStatusAvailable", result.LatestVersion);
                    UpdateStatusTextBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74));
                }

                var answer = System.Windows.MessageBox.Show(
                    this,
                    Loc.Format("UpdatePromptBody", result.LatestVersion, result.CurrentVersion, result.ReleaseNotes),
                    Loc.Format("UpdatePromptTitle", result.LatestVersion),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information,
                    MessageBoxResult.Yes);

                if (answer == MessageBoxResult.Yes)
                {
                    if (UpdateStatusTextBlock != null)
                    {
                        UpdateStatusTextBlock.Text = Loc.Format("UpdateDownloadingStatus", result.LatestVersion);
                    }

                    bool launched = await UpdateService.DownloadAndLaunchInstallerAsync(result.DownloadUrl);
                    if (launched)
                    {
                        _forceExit = true;
                        Close();
                    }
                }
            }
            else
            {
                if (UpdateStatusTextBlock != null)
                {
                    UpdateStatusTextBlock.Text = Loc.Format("UpdateStatusUpToDate", result.CurrentVersion);
                    UpdateStatusTextBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74));
                }
            }
        }
        finally
        {
            _isCheckingUpdates = false;
            if (CheckUpdatesButton != null)
            {
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = Loc["BtnCheckUpdatesNow"];
            }
        }
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

    // ==================== SELECTOR DE USB, DISCOS INTERNOS Y CARPETAS ====================

    private void SelectAllOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllCleaningOptions(true);
    }

    private void DeselectAllOptionsButton_Click(object sender, RoutedEventArgs e)
    {
        SetAllCleaningOptions(false);
    }

    private void SetAllCleaningOptions(bool isChecked)
    {
        ChkAppleDouble.IsChecked = isChecked;
        ChkDsStore.IsChecked = isChecked;
        ChkSpotlightTrash.IsChecked = isChecked;
        ChkWinCaches.IsChecked = isChecked;
        ChkWinRecycle.IsChecked = isChecked;
        ChkWinTempReadme.IsChecked = isChecked;
        ChkLinuxJunk.IsChecked = isChecked;
        ChkSmartTvJunk.IsChecked = isChecked;
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Loc["BrowseFolderDialogTitle"],
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            string selectedPath = dialog.FolderName;
            var existing = _customFolders.FirstOrDefault(f =>
                string.Equals(f.DriveRoot, selectedPath, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                existing = new UsbDriveItem
                {
                    TargetKind = CleanTargetKind.CustomFolder,
                    DriveRoot = selectedPath,
                    DriveLetter = char.ToUpperInvariant(selectedPath[0]),
                    VolumeLabel = Path.GetFileName(selectedPath.TrimEnd(Path.DirectorySeparatorChar)),
                    FileSystem = "Folder"
                };
                _customFolders.Insert(0, existing);
            }

            RefreshUsbDrives(isInitialLoad: false, preferredRoot: selectedPath);
            BottomStatusText.Text = Loc.Format("StatusFolderSelected", selectedPath);
        }
    }

    private void UsbDrivesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtonsForSelection();
    }

    private void UpdateActionButtonsForSelection()
    {
        if (!IsInitialized) return;

        if (UsbDrivesComboBox.SelectedItem is UsbDriveItem selected)
        {
            AnalyzeButton.IsEnabled = !_isCleaning;
            CleanUsbButton.IsEnabled = !_isCleaning;
            EjectUsbButton.IsEnabled = !_isCleaning && selected.CanEject;
            ChkAutoEject.IsEnabled = selected.CanEject;
            if (!selected.CanEject)
            {
                ChkAutoEject.IsChecked = false;
            }

            if (InternalDriveWarningBorder != null)
            {
                InternalDriveWarningBorder.Visibility = selected.TargetKind != CleanTargetKind.UsbDrive
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }
        else
        {
            AnalyzeButton.IsEnabled = false;
            CleanUsbButton.IsEnabled = false;
            EjectUsbButton.IsEnabled = false;
            if (InternalDriveWarningBorder != null)
            {
                InternalDriveWarningBorder.Visibility = Visibility.Collapsed;
            }
        }
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
            var newDrive = Drives.FirstOrDefault(d =>
                d.TargetKind == CleanTargetKind.UsbDrive && !previousRoots.Contains(d.DriveRoot));

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
                        _ = ExecuteCleaningAsync(newDrive, askConfirmation: false);
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

    private void InitializeDefaultFolders()
    {
        var defaultDirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        foreach (var dir in defaultDirs)
        {
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) &&
                !_customFolders.Any(f => string.Equals(f.DriveRoot, dir, StringComparison.OrdinalIgnoreCase)))
            {
                _customFolders.Add(new UsbDriveItem
                {
                    TargetKind = CleanTargetKind.CustomFolder,
                    DriveRoot = dir,
                    DriveLetter = char.ToUpperInvariant(dir[0]),
                    VolumeLabel = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)),
                    FileSystem = "Folder"
                });
            }
        }
    }

    private void RefreshUsbDrives(bool isInitialLoad, string? preferredRoot = null)
    {
        string? targetRoot = preferredRoot ?? (UsbDrivesComboBox.SelectedItem as UsbDriveItem)?.DriveRoot;

        // Mostrar directamente todas las unidades USB conectadas y los discos duros internos del ordenador
        var detected = UsbDriveService.GetConnectedUsbDrives(includeInternalDrives: true);
        Drives.Clear();
        _knownDriveRoots.Clear();

        // 1. Añadir memorias externas (USB)
        foreach (var drive in detected.Where(d => d.TargetKind == CleanTargetKind.UsbDrive))
        {
            Drives.Add(drive);
            _knownDriveRoots.Add(drive.DriveRoot);
        }

        // 2. Añadir memorias internas (discos locales del PC)
        foreach (var drive in detected.Where(d => d.TargetKind == CleanTargetKind.InternalDrive))
        {
            Drives.Add(drive);
        }

        // 3. Añadir carpetas que sigan existiendo en disco
        foreach (var folder in _customFolders.ToList())
        {
            if (Directory.Exists(folder.DriveRoot))
            {
                Drives.Add(folder);
            }
            else
            {
                _customFolders.Remove(folder);
            }
        }

        _drivesView.Refresh();

        if (Drives.Count > 0)
        {
            NoDrivesWarningText.Visibility = Visibility.Collapsed;
            UsbDrivesComboBox.IsEnabled = true;

            if (isInitialLoad || string.IsNullOrEmpty(targetRoot))
            {
                // Dejar el desplegable vacío sin nada seleccionado por defecto
                UsbDrivesComboBox.SelectedItem = null;
            }
            else
            {
                var match = Drives.FirstOrDefault(d => string.Equals(d.DriveRoot, targetRoot, StringComparison.OrdinalIgnoreCase));
                UsbDrivesComboBox.SelectedItem = match;
            }
        }
        else
        {
            NoDrivesWarningText.Visibility = Visibility.Visible;
            UsbDrivesComboBox.IsEnabled = false;
            UsbDrivesComboBox.SelectedItem = null;
        }

        UpdateActionButtonsForSelection();
    }

    // ==================== LIMPIEZA RECURSIVA Y LOG EN TIEMPO REAL ====================

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
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

        await ExecuteCleaningAsync(selectedDrive, askConfirmation: false, dryRun: true);
    }

    private async void DryRunCleanNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (UsbDrivesComboBox.SelectedItem is not UsbDriveItem selectedDrive) return;
        await ExecuteCleaningAsync(selectedDrive, askConfirmation: true, dryRun: false);
    }

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

        await ExecuteCleaningAsync(selectedDrive, askConfirmation: true, dryRun: false);
    }

    private async Task ExecuteCleaningAsync(UsbDriveItem targetDrive, bool askConfirmation = true, bool dryRun = false)
    {
        if (_isCleaning) return;

        bool cleanAppleDouble = ChkAppleDouble.IsChecked == true;
        bool cleanDsStore = ChkDsStore.IsChecked == true;
        bool cleanSpotlight = ChkSpotlightTrash.IsChecked == true;
        bool cleanWinCaches = ChkWinCaches.IsChecked == true;
        bool cleanWinRecycle = ChkWinRecycle.IsChecked == true;
        bool cleanWinTempReadme = ChkWinTempReadme.IsChecked == true;
        bool cleanLinux = ChkLinuxJunk.IsChecked == true;
        bool cleanSmartTv = ChkSmartTvJunk.IsChecked == true;

        if (!cleanAppleDouble && !cleanDsStore && !cleanSpotlight &&
            !cleanWinCaches && !cleanWinRecycle && !cleanWinTempReadme &&
            !cleanLinux && !cleanSmartTv)
        {
            System.Windows.MessageBox.Show(
                this,
                Loc["MsgSelectOptionBody"],
                Loc["MsgSelectOptionTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (askConfirmation)
        {
            var confirmResult = System.Windows.MessageBox.Show(
                this,
                Loc.Format("ConfirmCleanBody", targetDrive.DisplayName),
                Loc["ConfirmCleanTitle"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (confirmResult != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _isCleaning = true;
        _lastCleanedDriveName = targetDrive.DisplayName;
        _lastCleanSummary = null;
        _cleanCts = new CancellationTokenSource();

        // Mostrar pestaña / modal de Registro de Actividad (Log)
        SettingsPanel.Visibility = Visibility.Collapsed;
        MainConfigPanel.Visibility = Visibility.Collapsed;
        LogActivityPanel.Visibility = Visibility.Visible;
        UpdateSettingsButtonVisualState();

        DryRunCleanNowButton.Visibility = Visibility.Collapsed;
        AbortCleanButton.IsEnabled = true;
        AbortCleanButton.Content = Loc["BtnAbortClean"];
        SaveLogButton.IsEnabled = false;
        CloseLogButton.IsEnabled = false;
        CleanProgressBar.Value = 0;
        LogEntries.Clear();

        LogProgressStatusText.Text = dryRun
            ? Loc.Format("LogStatusAnalyzing", targetDrive.DisplayName)
            : Loc.Format("LogStatusScanning", targetDrive.DisplayName);
        LogSummaryTextBlock.Text = Loc.Format("LogStartHeader", targetDrive.DriveRoot, targetDrive.FileSystem);

        var options = new CleanOptions
        {
            CleanAppleDouble = cleanAppleDouble,
            CleanDsStore = cleanDsStore,
            CleanSpotlightAndTrash = cleanSpotlight,
            CleanWindowsCaches = cleanWinCaches,
            CleanWindowsRecycleAndFound = cleanWinRecycle,
            CleanWindowsTempAndReadme = cleanWinTempReadme,
            CleanLinuxJunk = cleanLinux,
            CleanSmartTvJunk = cleanSmartTv
        };

        long lastScrollTick = 0;
        var logProgress = new Progress<CleanLogEntry>(entry =>
        {
            LogEntries.Add(entry);
            long now = Environment.TickCount64;
            if (now - lastScrollTick >= 80)
            {
                lastScrollTick = now;
                LogListView.ScrollIntoView(entry);
            }
        });

        var percentProgress = new Progress<double>(pct =>
        {
            CleanProgressBar.Value = pct;
        });

        CleanSummary? summary = null;
        try
        {
            summary = await MacJunkCleanerService.CleanDriveAsync(
                targetDrive.DriveRoot,
                options,
                logProgress,
                percentProgress,
                _cleanCts.Token,
                dryRun: dryRun);

            _lastCleanSummary = summary;

            if (summary.WasAborted)
            {
                LogEntries.Add(new CleanLogEntry
                {
                    Kind = CleanItemKind.Notice,
                    FullPath = Loc["LogAbortedEntryMsg"],
                    SizeBytes = 0
                });
            }
            else if (summary.DeletedFilesCount == 0 && summary.DeletedFoldersCount == 0)
            {
                LogEntries.Add(new CleanLogEntry
                {
                    Kind = CleanItemKind.Info,
                    FullPath = Loc["LogNoItemsMessage"],
                    SizeBytes = 0
                });
            }

            if (LogEntries.Count > 0)
            {
                LogListView.ScrollIntoView(LogEntries[^1]);
            }

            UpdateLogSummaryTexts(targetDrive.DisplayName, summary);
        }
        finally
        {
            _isCleaning = false;
            _cleanCts.Dispose();
            _cleanCts = null;

            AbortCleanButton.IsEnabled = false;
            AbortCleanButton.Content = Loc["BtnAbortClean"];
            SaveLogButton.IsEnabled = true;
            CloseLogButton.IsEnabled = true;
            RefreshUsbDrives(isInitialLoad: false, preferredRoot: targetDrive.DriveRoot);
        }

        // Si fue vista previa (Analizar sin borrar) y encontró basura, mostrar botón para limpiar ahora mismo
        if (dryRun && summary != null && !summary.WasAborted &&
            (summary.DeletedFilesCount > 0 || summary.DeletedFoldersCount > 0))
        {
            DryRunCleanNowButton.Visibility = Visibility.Visible;
        }

        // Mostrar ventana de limpieza completada con opción de apoyar por PayPal (solo en limpieza real no abortada)
        if (!dryRun && summary != null && !summary.WasAborted)
        {
            var doneWindow = new CleanCompletedWindow(targetDrive.DisplayName, summary)
            {
                Owner = this
            };
            doneWindow.ShowDialog();
        }

        // Si el usuario marcó "Expulsar automáticamente al terminar la limpieza", es un USB, es limpieza real y NO fue abortado
        if (!dryRun && ChkAutoEject.IsChecked == true && targetDrive.CanEject && summary != null && !summary.WasAborted)
        {
            PerformSafeEject(targetDrive);
        }
    }

    private void AbortCleanButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isCleaning || _cleanCts == null || _cleanCts.IsCancellationRequested)
            return;

        AbortCleanButton.IsEnabled = false;
        AbortCleanButton.Content = Loc["BtnAbortingClean"];
        _cleanCts.Cancel();
    }

    private void UpdateLogSummaryTexts(string driveDisplay, CleanSummary summary)
    {
        if (summary.WasAborted)
        {
            LogProgressStatusText.Text = Loc.Format("LogStatusAborted", driveDisplay);
            LogSummaryTextBlock.Text = Loc.Format(
                "LogSummaryAbortedFormat",
                summary.DeletedFilesCount,
                summary.DeletedFoldersCount,
                UsbDriveItem.FormatBytes(summary.TotalBytesFreed));
            BottomStatusText.Text = LogSummaryTextBlock.Text;
            return;
        }

        if (summary.IsDryRun)
        {
            LogProgressStatusText.Text = (summary.DeletedFilesCount == 0 && summary.DeletedFoldersCount == 0)
                ? Loc.Format("LogStatusNothingFound", driveDisplay)
                : Loc.Format("LogStatusDryRunCompleted", driveDisplay);

            LogSummaryTextBlock.Text = Loc.Format(
                "LogDryRunSummaryFormat",
                summary.DeletedFilesCount,
                summary.DeletedFoldersCount,
                UsbDriveItem.FormatBytes(summary.TotalBytesFreed));

            BottomStatusText.Text = LogSummaryTextBlock.Text;
            return;
        }

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
        UpdateSettingsButtonVisualState();
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

        if (!selectedDrive.CanEject)
        {
            System.Windows.MessageBox.Show(
                this,
                Loc["MsgEjectOnlyUsbBody"],
                Loc["MsgEjectOnlyUsbTitle"],
                MessageBoxButton.OK,
                MessageBoxImage.Information);
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

    // ==================== VENTANA ACERCA DE Y DONACIÓN PAYPAL ====================

    private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch
        {
            // Ignorar si no hay navegador predeterminado
        }
    }

    private void DonatePayPalButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
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

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }
}
