using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace CleanMacUSB.Services;

/// <summary>
/// Sistema de internacionalización dinámico en tiempo real (es-ES / en-US).
/// Permite cambiar todo el idioma de la interfaz, ajustes, logs, diálogos y bandeja del sistema sin reiniciar.
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    public static LocalizationService Instance { get; } = new();

    private string _currentCulture = "es-ES";
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    private readonly Dictionary<string, string> _es = new()
    {
        ["AppTitle"] = "CleanMacUSB",
        ["AppSubtitle"] = "Limpiador nativo de archivos residuales de macOS para unidades USB",
        ["SettingsButton"] = "Ajustes",
        ["AboutButton"] = "Acerca de",
        ["LanguageLabel"] = "Idioma:",

        // Selector USB
        ["UsbSectionTitle"] = "Unidad USB / Disco Externo",
        ["UsbSectionSubtitle"] = "Selecciona la memoria USB o unidad externa que deseas limpiar o expulsar",
        ["RefreshDrivesButton"] = "Actualizar unidades",
        ["NoUsbDetected"] = "No se han detectado memorias USB ni unidades extraíbles conectadas",
        ["NoLabel"] = "SIN ETIQUETA",
        ["DriveFormatPattern"] = "{0} [{1}] — {2} libres de {3} ({4})",

        // Opciones de limpieza
        ["OptionsSectionTitle"] = "Configuración de Limpieza",
        ["OptAppleDoubleTitle"] = "Archivos AppleDouble (._*)",
        ["OptAppleDoubleDesc"] = "Metadatos bifurcados ocultos creados por Finder en sistemas exFAT, FAT32 y NTFS (._*, .AppleDouble)",
        ["OptDsStoreTitle"] = "Archivos de configuración de carpetas (.DS_Store)",
        ["OptDsStoreDesc"] = "Preferencias visuales de carpetas e iconos de volumen (.DS_Store, .VolumeIcon.icns, .apdisk)",
        ["OptSpotlightTrashTitle"] = "Índices de Spotlight y papelera de Mac (.Spotlight-V100, .Trashes, .fseventsd, .TemporaryItems)",
        ["OptSpotlightTrashDesc"] = "Carpetas ocultas de indexación, eventos de sistema de archivos, papeleras y temporales de macOS",
        ["OptAutoEjectTitle"] = "Expulsar automáticamente la unidad USB al terminar la limpieza",

        // Apartado de Ajustes (Settings)
        ["SettingsPanelTitle"] = "Ajustes de la Aplicación",
        ["SettingsPanelSubtitle"] = "Configura el comportamiento en segundo plano y consulta la versión e información legal",
        ["SettingsBackButton"] = "← Volver al inicio",
        ["SettingsGeneralHeader"] = "Comportamiento y Sistema",
        ["OptTrayTitle"] = "Ejecutar en segundo plano (Bandeja del sistema)",
        ["OptTrayDesc"] = "Al cerrar la ventana, CleanMacUSB permanece activo junto al reloj de Windows y muestra un aviso automático al conectar un USB nuevo.",
        ["SettingsTrayStatusOn"] = "Estado actual: ACTIVO (Se minimizará en la bandeja del sistema al cerrar)",
        ["SettingsTrayStatusOff"] = "Estado actual: DESACTIVADO (La aplicación se cerrará por completo al salir)",
        ["SettingsAboutSectionHeader"] = "Acerca de CleanMacUSB y Versión",
        ["SettingsVersionLabel"] = "Versión instalada:",
        ["SettingsVersionValue"] = "1.0.0 (Release 64-bit — Windows 10 / 11)",
        ["SettingsLicenseTypeLabel"] = "Tipo de licencia:",
        ["SettingsLicenseTypeValue"] = "Software Gratuito y Propietario (Freeware)",
        ["SettingsAuthorLabel"] = "Desarrollador y Autor:",
        ["SettingsAuthorValue"] = "Josan Sánchez — Todos los derechos reservados",
        ["SettingsViewFullEulaBtn"] = "Ver licencia en ventana completa",

        // Botones principales
        ["BtnCleanUsb"] = "Limpiar USB ahora",
        ["BtnEjectUsb"] = "Expulsar USB de forma segura",
        ["StatusReady"] = "Listo. Selecciona una unidad USB y pulsa «Limpiar USB ahora».",
        ["StatusDrivesUpdated"] = "Lista de unidades USB actualizada ({0} detectadas).",
        ["StatusNewUsbConnected"] = "Nueva unidad USB detectada: {0}",
        ["StatusUsbRemoved"] = "Se ha desconectado una unidad USB.",

        // Panel / Modal de Registro (Log)
        ["LogTabTitle"] = "Registro de Actividad de Limpieza",
        ["LogStatusScanning"] = "Escaneando y limpiando unidad {0}...",
        ["LogStatusCompleted"] = "Limpieza completada con éxito en {0}",
        ["LogStatusAborted"] = "Limpieza abortada por el usuario en {0}",
        ["LogStatusNothingFound"] = "Limpieza finalizada: la unidad {0} ya estaba completamente limpia.",
        ["LogSummaryFormat"] = "Resumen final: {0} archivos eliminados | {1} carpetas eliminadas | Espacio total liberado: {2}",
        ["LogSummaryAbortedFormat"] = "Proceso abortado — Eliminados hasta el momento: {0} archivos | {1} carpetas | Espacio liberado: {2}",
        ["LogColType"] = "Tipo",
        ["LogColPath"] = "Ruta completa del elemento eliminado",
        ["LogColSize"] = "Tamaño",
        ["LogTypeFile"] = "ARCHIVO",
        ["LogTypeFolder"] = "CARPETA",
        ["LogTypeInfo"] = "INFO",
        ["LogTypeError"] = "AVISO",
        ["LogStartHeader"] = "Iniciando análisis recursivo en {0} ({1})...",
        ["LogNoItemsMessage"] = "No se encontraron archivos ni carpetas residuales de macOS en esta unidad.",
        ["LogAbortedEntryMsg"] = "Limpieza detenida y abortada a petición del usuario.",
        ["BtnAbortClean"] = "⏹ Abortar limpieza",
        ["BtnAbortingClean"] = "Abortando...",
        ["BtnSaveLog"] = "Guardar registro (.txt)",
        ["BtnCloseLog"] = "Cerrar registro",
        ["SaveLogDialogTitle"] = "Guardar registro de limpieza de CleanMacUSB",
        ["SaveLogSuccessTitle"] = "Registro guardado",
        ["SaveLogSuccessMsg"] = "El archivo de registro se ha guardado correctamente en:\n{0}",
        ["LogFileHeaderTitle"] = "REGISTRO DE ACTIVIDAD — CLEANMACUSB v1.0 (por Josan Sánchez)",
        ["LogFileDate"] = "Fecha y hora",
        ["LogFileDrive"] = "Unidad analizada",

        // Alertas y expulsión segura
        ["MsgSelectDriveTitle"] = "Ninguna unidad seleccionada",
        ["MsgSelectDriveBody"] = "Por favor, conecta y selecciona una unidad USB válida antes de continuar.",
        ["MsgSelectOptionTitle"] = "Sin opciones de limpieza",
        ["MsgSelectOptionBody"] = "Marca al menos una categoría de archivos residuales de macOS para limpiar.",
        ["MsgEjectSuccessTitle"] = "Expulsión segura completada",
        ["MsgEjectSuccessBody"] = "La unidad {0} se ha desmontado y expulsado de forma segura.\nYa puedes retirar físicamente el dispositivo USB del equipo.",
        ["MsgEjectFailTitle"] = "No se pudo expulsar la unidad",
        ["MsgEjectFailBody"] = "Windows indica que la unidad {0} está siendo utilizada por otro programa o proceso.\nCierra cualquier ventana o archivo abierto de la unidad e inténtalo de nuevo.\n\nDetalle: {1}",

        // Bandeja del sistema (System Tray)
        ["TrayTooltip"] = "CleanMacUSB v1.0 — Ejecutándose en segundo plano",
        ["TrayMenuOpen"] = "Abrir CleanMacUSB",
        ["TrayMenuCleanNow"] = "Limpiar unidad USB seleccionada",
        ["TrayMenuExit"] = "Salir completamente",
        ["TrayMinimizedTitle"] = "CleanMacUSB sigue activo",
        ["TrayMinimizedBody"] = "La aplicación permanece en la bandeja del sistema. Te avisaremos cuando conectes un USB.",
        ["TrayUsbDetectedTitle"] = "Memoria USB detectada ({0})",
        ["TrayUsbDetectedBody"] = "Se ha conectado {0}. Haz clic aquí para limpiar los archivos ocultos de macOS.",
        ["TrayUsbPromptQuestion"] = "Se ha detectado la unidad USB {0}.\n¿Deseas analizarla y eliminar los archivos residuales de macOS ahora mismo?",

        // Ventana Acerca de (About)
        ["AboutWindowTitle"] = "Acerca de CleanMacUSB",
        ["AboutVersionText"] = "CleanMacUSB v1.0 (Edición Windows 11 / 64-bit)",
        ["AboutCopyrightText"] = "Software gratuito y propietario (Freeware).\nDesarrollado por Josan Sánchez. Todos los derechos reservados.",
        ["AboutDescription"] = "Utilidad nativa de alto rendimiento diseñada para erradicar archivos ocultos de macOS (._*, .DS_Store, .Spotlight-V100, .Trashes, .fseventsd, .TemporaryItems, .VolumeIcon.icns, .apdisk) y expulsar memorias USB con seguridad.",
        ["AboutLicenseHeader"] = "Contrato de Licencia de Usuario Final (EULA):",
        ["AboutCloseButton"] = "Cerrar"
    };

    private readonly Dictionary<string, string> _en = new()
    {
        ["AppTitle"] = "CleanMacUSB",
        ["AppSubtitle"] = "Native macOS junk & hidden file cleaner for USB flash drives",
        ["SettingsButton"] = "Settings",
        ["AboutButton"] = "About",
        ["LanguageLabel"] = "Language:",

        // Selector USB
        ["UsbSectionTitle"] = "USB Flash Drive / External Disk",
        ["UsbSectionSubtitle"] = "Select the removable USB drive or external disk you want to clean or safely eject",
        ["RefreshDrivesButton"] = "Refresh drives",
        ["NoUsbDetected"] = "No connected USB flash drives or removable disks detected",
        ["NoLabel"] = "NO LABEL",
        ["DriveFormatPattern"] = "{0} [{1}] — {2} free of {3} ({4})",

        // Opciones de limpieza
        ["OptionsSectionTitle"] = "Cleaning Configuration",
        ["OptAppleDoubleTitle"] = "AppleDouble Resource Fork Files (._*)",
        ["OptAppleDoubleDesc"] = "Hidden resource fork metadata created by macOS Finder on exFAT, FAT32, and NTFS drives (._*, .AppleDouble)",
        ["OptDsStoreTitle"] = "Folder View Settings & Icons (.DS_Store)",
        ["OptDsStoreDesc"] = "Desktop Services Store files and custom volume metadata (.DS_Store, .VolumeIcon.icns, .apdisk)",
        ["OptSpotlightTrashTitle"] = "Spotlight Indexes & Mac Trash (.Spotlight-V100, .Trashes, .fseventsd, .TemporaryItems)",
        ["OptSpotlightTrashDesc"] = "Hidden macOS indexing databases, file system events, trash folders, and temporary items",
        ["OptAutoEjectTitle"] = "Automatically eject USB drive when cleaning finishes",

        // Apartado de Ajustes (Settings)
        ["SettingsPanelTitle"] = "Application Settings",
        ["SettingsPanelSubtitle"] = "Configure background behavior and view version & legal information",
        ["SettingsBackButton"] = "← Back to Home",
        ["SettingsGeneralHeader"] = "Behavior & System",
        ["OptTrayTitle"] = "Run in background (System Tray)",
        ["OptTrayDesc"] = "When closing the window, CleanMacUSB stays active near the Windows clock and automatically notifies you when a new USB drive is plugged in.",
        ["SettingsTrayStatusOn"] = "Current status: ENABLED (Will minimize to the system tray when closed)",
        ["SettingsTrayStatusOff"] = "Current status: DISABLED (The application will exit completely when closed)",
        ["SettingsAboutSectionHeader"] = "About CleanMacUSB & Version",
        ["SettingsVersionLabel"] = "Installed version:",
        ["SettingsVersionValue"] = "1.0.0 (64-bit Release — Windows 10 / 11)",
        ["SettingsLicenseTypeLabel"] = "License type:",
        ["SettingsLicenseTypeValue"] = "Proprietary Freeware Software",
        ["SettingsAuthorLabel"] = "Developer & Author:",
        ["SettingsAuthorValue"] = "Josan Sánchez — All rights reserved",
        ["SettingsViewFullEulaBtn"] = "View license in full window",

        // Botones principales
        ["BtnCleanUsb"] = "Clean USB Now",
        ["BtnEjectUsb"] = "Safely Eject USB",
        ["StatusReady"] = "Ready. Select a USB drive and click \"Clean USB Now\".",
        ["StatusDrivesUpdated"] = "USB drive list updated ({0} detected).",
        ["StatusNewUsbConnected"] = "New USB drive detected: {0}",
        ["StatusUsbRemoved"] = "A USB drive was disconnected.",

        // Panel / Modal de Registro (Log)
        ["LogTabTitle"] = "Cleaning Activity Log",
        ["LogStatusScanning"] = "Scanning and cleaning drive {0}...",
        ["LogStatusCompleted"] = "Cleaning successfully completed on {0}",
        ["LogStatusAborted"] = "Cleaning aborted by user on {0}",
        ["LogStatusNothingFound"] = "Cleaning finished: drive {0} was already completely clean.",
        ["LogSummaryFormat"] = "Final Summary: {0} files deleted | {1} folders deleted | Total space freed: {2}",
        ["LogSummaryAbortedFormat"] = "Process aborted — Deleted so far: {0} files | {1} folders | Space freed: {2}",
        ["LogColType"] = "Type",
        ["LogColPath"] = "Full path of deleted item",
        ["LogColSize"] = "Size",
        ["LogTypeFile"] = "FILE",
        ["LogTypeFolder"] = "FOLDER",
        ["LogTypeInfo"] = "INFO",
        ["LogTypeError"] = "NOTICE",
        ["LogStartHeader"] = "Starting recursive scan on {0} ({1})...",
        ["LogNoItemsMessage"] = "No residual macOS files or folders were found on this drive.",
        ["LogAbortedEntryMsg"] = "Cleaning stopped and aborted at user request.",
        ["BtnAbortClean"] = "⏹ Abort Cleaning",
        ["BtnAbortingClean"] = "Aborting...",
        ["BtnSaveLog"] = "Save Log (.txt)",
        ["BtnCloseLog"] = "Close Log",
        ["SaveLogDialogTitle"] = "Save CleanMacUSB Cleaning Log",
        ["SaveLogSuccessTitle"] = "Log Saved",
        ["SaveLogSuccessMsg"] = "The activity log file was successfully saved to:\n{0}",
        ["LogFileHeaderTitle"] = "ACTIVITY LOG — CLEANMACUSB v1.0 (by Josan Sánchez)",
        ["LogFileDate"] = "Date & Time",
        ["LogFileDrive"] = "Scanned Drive",

        // Alertas y expulsión segura
        ["MsgSelectDriveTitle"] = "No USB Drive Selected",
        ["MsgSelectDriveBody"] = "Please connect and select a valid USB drive before continuing.",
        ["MsgSelectOptionTitle"] = "No Cleaning Options Selected",
        ["MsgSelectOptionBody"] = "Please check at least one category of residual macOS files to clean.",
        ["MsgEjectSuccessTitle"] = "Safe Eject Completed",
        ["MsgEjectSuccessBody"] = "Drive {0} has been safely dismounted and ejected.\nYou can now physically remove the USB device from your computer.",
        ["MsgEjectFailTitle"] = "Could Not Eject Drive",
        ["MsgEjectFailBody"] = "Windows reported that drive {0} is currently in use by another program or process.\nPlease close any open files or Explorer windows on that drive and try again.\n\nDetail: {1}",

        // Bandeja del sistema (System Tray)
        ["TrayTooltip"] = "CleanMacUSB v1.0 — Running in background",
        ["TrayMenuOpen"] = "Open CleanMacUSB",
        ["TrayMenuCleanNow"] = "Clean selected USB drive",
        ["TrayMenuExit"] = "Exit completely",
        ["TrayMinimizedTitle"] = "CleanMacUSB is still running",
        ["TrayMinimizedBody"] = "The application stays minimized in the system tray. We will notify you when a new USB drive is plugged in.",
        ["TrayUsbDetectedTitle"] = "USB Flash Drive Detected ({0})",
        ["TrayUsbDetectedBody"] = "Drive {0} was plugged in. Click here to clean hidden macOS junk files.",
        ["TrayUsbPromptQuestion"] = "USB drive {0} was just connected.\nWould you like to scan it and clean residual macOS files right now?",

        // Ventana Acerca de (About)
        ["AboutWindowTitle"] = "About CleanMacUSB",
        ["AboutVersionText"] = "CleanMacUSB v1.0 (Windows 11 / 64-bit Edition)",
        ["AboutCopyrightText"] = "Freeware & Proprietary Software.\nDeveloped by Josan Sánchez. All rights reserved.",
        ["AboutDescription"] = "High-performance native utility designed to eradicate hidden macOS files (._*, .DS_Store, .Spotlight-V100, .Trashes, .fseventsd, .TemporaryItems, .VolumeIcon.icns, .apdisk) and safely eject USB drives.",
        ["AboutLicenseHeader"] = "End-User License Agreement (EULA):",
        ["AboutCloseButton"] = "Close"
    };

    private LocalizationService()
    {
        var osLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        _currentCulture = osLang == "es" ? "es-ES" : "en-US";
    }

    public string CurrentCulture
    {
        get => _currentCulture;
        set
        {
            var normalized = value.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "es-ES" : "en-US";
            if (_currentCulture == normalized) return;
            _currentCulture = normalized;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentCulture)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSpanish)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsSpanish => _currentCulture == "es-ES";

    public string this[string key]
    {
        get
        {
            var dict = IsSpanish ? _es : _en;
            return dict.TryGetValue(key, out var val) ? val : key;
        }
    }

    public string Format(string key, params object[] args)
    {
        return string.Format(this[key], args);
    }

    public string GetEulaText()
    {
        try
        {
            var resourceName = IsSpanish ? "Assets/EULA_ES.txt" : "Assets/EULA_EN.txt";
            var uri = new Uri($"pack://application:,,,/{resourceName}", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var reader = new StreamReader(streamInfo.Stream);
                return reader.ReadToEnd();
            }
        }
        catch
        {
            // Fallback en caso de lectura externa
        }

        return this["AboutCopyrightText"];
    }
}
