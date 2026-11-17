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
        ["AppSubtitle"] = "Limpiador nativo de archivos basura (macOS, Windows, Linux y Smart TV) para USB, discos y carpetas",
        ["SettingsButton"] = "Ajustes",
        ["AboutButton"] = "Acerca de",
        ["LanguageLabel"] = "Idioma:",

        // Selector de Unidad USB / Disco Interno / Carpeta
        ["UsbSectionTitle"] = "Origen a Limpiar (Unidad USB, Disco Duro o Carpeta)",
        ["UsbSectionSubtitle"] = "Selecciona una memoria USB, un disco duro del ordenador o elige una carpeta específica",
        ["RefreshDrivesButton"] = "Actualizar",
        ["BrowseFolderButton"] = "📁 Elegir carpeta...",
        ["BrowseFolderDialogTitle"] = "Selecciona la carpeta que deseas analizar y limpiar",
        ["NoUsbDetected"] = "No se han encontrado unidades disponibles.",
        ["NoLabel"] = "SIN ETIQUETA",
        ["TagUsbDrive"] = "[USB]",
        ["TagInternalDrive"] = "[DISCO LOCAL]",
        ["DriveFormatPattern"] = "{0} [{1}] — {2} libres de {3} ({4})",
        ["FolderFormatPattern"] = "[CARPETA] {0}",
        ["InternalDriveWarning"] = "⚠ Atención: Al limpiar un disco duro del equipo, es posible que algunas aplicaciones, programas o juegos instalados puedan verse alterados por la similitud de ciertos archivos (como README, *.tmp o *.bak).",

        // Opciones de limpieza (macOS + Windows + Linux + Smart TV / Android)
        ["OptionsSectionTitle"] = "Configuración de Limpieza",
        ["BtnSelectAll"] = "☑ Elegir todo",
        ["BtnDeselectAll"] = "☐ Desmarcar todo",
        ["BadgeMacOs"] = "macOS",
        ["BadgeWindows"] = "Windows",
        ["BadgeLinux"] = "Linux",
        ["BadgeSmartTv"] = "Smart TV / Android",
        ["OptAppleDoubleTitle"] = "Archivos AppleDouble y carpetas de descompresión (._*, __MACOSX, .AppleDouble)",
        ["OptAppleDoubleDesc"] = "Motivo: Evita que aparezcan archivos fantasma duplicados (._*) y carpetas __MACOSX (al descomprimir ZIPs de Mac) que provocan errores de lectura en radios de coche, equipos DJ y televisores.",
        ["OptDsStoreTitle"] = "Configuración de carpetas e iconos de Mac (.DS_Store, .VolumeIcon.icns, .apdisk)",
        ["OptDsStoreDesc"] = "Motivo: Elimina los archivos ocultos de diseño de ventana que Finder crea en cada carpeta y que ensucian el explorador en Windows y reproductores multimedia.",
        ["OptSpotlightTrashTitle"] = "Índices Spotlight, Vista Rápida y Papelera (.Spotlight-V100/V200, .Trashes, .fseventsd, .ql_tcache)",
        ["OptSpotlightTrashDesc"] = "Motivo: Recupera el espacio libre real retenido por la papelera oculta (.Trashes) y borra las bases de datos de indexación y miniaturas de macOS (.TemporaryItems, .QuickLook, .hotfiles.btree).",
        ["OptWinCacheTitle"] = "Cachés de miniaturas y carpeta System Volume Information (Thumbs.db, desktop.ini, WPSettings.dat)",
        ["OptWinCacheDesc"] = "Motivo: Elimina cachés ocultas del Explorador (ehthumbs.db, ZbThumbnail.info) y limpia System Volume Information para evitar que las radios de coche lean esa carpeta oculta como si fuera la «Carpeta 01».",
        ["OptWinRecycleTitle"] = "Papelera de Windows y fragmentos de reparación ($RECYCLE.BIN, FOUND.000, *.chk)",
        ["OptWinRecycleDesc"] = "Motivo: Libera el espacio ocupado por la papelera oculta de Windows en el USB ($RECYCLE.BIN, RECYCLER) y elimina fragmentos huérfanos generados tras un escaneo de errores CHKDSK.",
        ["OptWinTempReadmeTitle"] = "Archivos residuales README., bloqueos de Office y temporales (README., ~$*, *.tmp, *.bak)",
        ["OptWinTempReadmeDesc"] = "Motivo: Elimina archivos README. autogenerados (incluso con punto final inválido en Windows), archivos fantasma de bloqueo de Word/Excel (~$*, ~WRL*.tmp) y basura temporal (msdownld.tmp).",
        ["OptLinuxTitle"] = "Papeleras, vistas de carpeta y temporales de Linux (.Trash-1000, .directory, .fuse_hidden*)",
        ["OptLinuxDesc"] = "Motivo: Libera el espacio retenido en las papeleras ocultas de Linux (.Trash-*), limpia preferencias de carpetas KDE/GNOME (.directory, .hidden) y borra bloqueos de montaje FUSE y editores (.*.swp).",
        ["OptSmartTvTitle"] = "Basura de televisores Smart TV y móviles Android (LOST.DIR, LG Smart TV, .thumbnails, Android vacía)",
        ["OptSmartTvDesc"] = "Motivo: Al conectar un USB a una Smart TV o móvil Android por OTG se crean decenas de carpetas vacías (Android, Alarms, Ringtones, LOST.DIR) y cachés pesadas (.thumbdata*, .database_uuid, .estrongs). Las limpia respetando tus archivos.",
        ["OptAutoEjectTitle"] = "Expulsar automáticamente la unidad USB al terminar la limpieza",

        // Apartado de Ajustes (Settings)
        ["SettingsPanelTitle"] = "Ajustes de la Aplicación",
        ["SettingsPanelSubtitle"] = "Configura el idioma, las actualizaciones automáticas, el segundo plano y la información legal",
        ["SettingsBackButton"] = "← Volver al inicio",
        ["SettingsGeneralHeader"] = "Idioma, Actualizaciones y Sistema",
        ["SettingsLangTitle"] = "Idioma de la interfaz (Language)",
        ["SettingsLangDesc"] = "Cambia en tiempo real todos los textos, botones, registros y avisos sin necesidad de reiniciar.",
        ["OptAutoUpdateTitle"] = "Actualizaciones automáticas",
        ["OptAutoUpdateDesc"] = "Comprueba automáticamente al iniciar la aplicación si hay una nueva versión disponible en GitHub.",
        ["BtnCheckUpdatesNow"] = "🔄 Buscar actualizaciones",
        ["BtnCheckingUpdates"] = "Buscando...",
        ["UpdateStatusIdle"] = "Versión actual instalada: v1.0.0",
        ["UpdateStatusChecking"] = "Comprobando si existen nuevas versiones en el repositorio oficial...",
        ["UpdateStatusUpToDate"] = "✔ Estás al día: ya tienes instalada la última versión (v{0}).",
        ["UpdateStatusAvailable"] = "⬇ ¡Nueva versión v{0} disponible! Pulsa para descargar y actualizar.",
        ["UpdateStatusOffline"] = "No se pudo conectar con el servidor de actualizaciones. Comprueba tu conexión.",
        ["UpdatePromptTitle"] = "Nueva actualización disponible (v{0})",
        ["UpdatePromptBody"] = "Se ha encontrado una nueva versión de CleanMacUSB (v{0}).\nVersión instalada actualmente: v{1}\n\n{2}\n\n¿Deseas descargar e instalar la actualización automáticamente ahora?",
        ["UpdateDownloadingStatus"] = "Descargando instalador de la versión v{0}...",
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
        ["BtnDonateCoffee"] = "☕ Invítame a un café (PayPal)",
        ["DonateTooltip"] = "Apoya el desarrollo gratuito de CleanMacUSB por Josan Sánchez (paypal.me/josansancheez)",

        // Botones principales
        ["BtnCleanUsb"] = "Limpiar ahora",
        ["BtnEjectUsb"] = "Expulsar USB de forma segura",
        ["StatusReady"] = "Listo. Selecciona una unidad USB, disco duro o carpeta y pulsa «Limpiar ahora».",
        ["StatusDrivesUpdated"] = "Lista de unidades actualizada ({0} disponibles).",
        ["StatusFolderSelected"] = "Carpeta seleccionada para limpiar: {0}",
        ["StatusNewUsbConnected"] = "Nueva unidad USB detectada: {0}",
        ["StatusUsbRemoved"] = "Se ha desconectado una unidad USB.",

        // Panel / Modal de Registro (Log)
        ["LogTabTitle"] = "Registro de Actividad de Limpieza",
        ["LogStatusScanning"] = "Escaneando y limpiando {0}...",
        ["LogStatusCompleted"] = "Limpieza completada con éxito en {0}",
        ["LogStatusAborted"] = "Limpieza abortada por el usuario en {0}",
        ["LogStatusNothingFound"] = "Limpieza finalizada: {0} ya estaba completamente limpio.",
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
        ["LogNoItemsMessage"] = "No se encontraron archivos ni carpetas residuales en la ubicación seleccionada.",
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
        ["LogFileDrive"] = "Origen analizado",

        // Alertas y expulsión segura
        ["ConfirmCleanTitle"] = "Confirmar limpieza",
        ["ConfirmCleanBody"] = "¿Estás seguro de que deseas iniciar la limpieza de archivos basura en la siguiente ubicación?\n\n{0}\n\nLos archivos residuales encontrados se eliminarán de forma permanente.",
        ["MsgSelectDriveTitle"] = "Ningún origen seleccionado",
        ["MsgSelectDriveBody"] = "Por favor, selecciona una unidad USB, un disco duro o elige una carpeta antes de continuar.",
        ["MsgSelectOptionTitle"] = "Sin opciones de limpieza",
        ["MsgSelectOptionBody"] = "Marca al menos una categoría de archivos residuales para limpiar.",
        ["MsgEjectOnlyUsbTitle"] = "Expulsión solo para unidades USB",
        ["MsgEjectOnlyUsbBody"] = "Solo es posible expulsar físicamente memorias USB y discos externos extraíbles.",
        ["MsgEjectSuccessTitle"] = "Expulsión segura completada",
        ["MsgEjectSuccessBody"] = "La unidad {0} se ha desmontado y expulsado de forma segura.\nYa puedes retirar físicamente el dispositivo USB del equipo.",
        ["MsgEjectFailTitle"] = "No se pudo expulsar la unidad",
        ["MsgEjectFailBody"] = "Windows indica que la unidad {0} está siendo utilizada por otro programa o proceso.\nCierra cualquier ventana o archivo abierto de la unidad e inténtalo de nuevo.\n\nDetalle: {1}",

        // Bandeja del sistema (System Tray)
        ["TrayTooltip"] = "CleanMacUSB v1.0 — Ejecutándose en segundo plano",
        ["TrayMenuOpen"] = "Abrir CleanMacUSB",
        ["TrayMenuCleanNow"] = "Limpiar origen seleccionado",
        ["TrayMenuExit"] = "Salir completamente",
        ["TrayMinimizedTitle"] = "CleanMacUSB sigue activo",
        ["TrayMinimizedBody"] = "La aplicación permanece en la bandeja del sistema. Te avisaremos cuando conectes un USB.",
        ["TrayUsbDetectedTitle"] = "Memoria USB detectada ({0})",
        ["TrayUsbDetectedBody"] = "Se ha conectado {0}. Haz clic aquí para limpiar los archivos ocultos.",
        ["TrayUsbPromptQuestion"] = "Se ha detectado la unidad USB {0}.\n¿Deseas analizarla y eliminar los archivos residuales ahora mismo?",

        // Ventana de Limpieza Completada + Apoyar con PayPal
        ["CleanDoneWindowTitle"] = "¡Limpieza completada! — CleanMacUSB",
        ["CleanDoneHeading"] = "✔ ¡Ya has limpiado tu unidad con éxito!",
        ["CleanDoneSubheading"] = "Se han eliminado {0} archivos y {1} carpetas residuales ({2} liberados).",
        ["CleanDoneSupportTitle"] = "💛 Si te ha ayudado a ti, ¡ayúdame tú a mí!",
        ["CleanDoneSupportBody"] = "CleanMacUSB es una herramienta 100 % gratuita creada por Josan Sánchez. Si esta aplicación te ha resultado útil y te ha ahorrado tiempo, puedes apoyar el proyecto con una pequeña aportación por PayPal para que pueda seguir manteniéndola y mejorándola.",
        ["CleanDoneBtnPayPal"] = "☕ Apoyar con PayPal",
        ["CleanDoneBtnClose"] = "Cerrar",

        // Ventana Acerca de (About)
        ["AboutWindowTitle"] = "Acerca de CleanMacUSB",
        ["AboutVersionText"] = "CleanMacUSB v1.0 (Edición Windows 11 / 64-bit)",
        ["AboutCopyrightText"] = "Software gratuito y propietario (Freeware).\nDesarrollado por Josan Sánchez. Todos los derechos reservados.",
        ["AboutDescription"] = "Utilidad nativa de alto rendimiento diseñada para erradicar archivos ocultos de macOS (._*, .DS_Store, .Spotlight-V100), Windows (Thumbs.db, desktop.ini, $RECYCLE.BIN, README.), Linux (.Trash-1000, .directory) y Smart TVs (LOST.DIR, LG Smart TV).",
        ["AboutLicenseHeader"] = "Contrato de Licencia de Usuario Final (EULA):",
        ["AboutCloseButton"] = "Cerrar"
    };

    private readonly Dictionary<string, string> _en = new()
    {
        ["AppTitle"] = "CleanMacUSB",
        ["AppSubtitle"] = "Native junk cleaner (macOS, Windows, Linux & Smart TV) for USB drives, disks, and folders",
        ["SettingsButton"] = "Settings",
        ["AboutButton"] = "About",
        ["LanguageLabel"] = "Language:",

        // Selector de Unidad USB / Disco Interno / Carpeta
        ["UsbSectionTitle"] = "Target to Clean (USB Drive, Hard Disk, or Folder)",
        ["UsbSectionSubtitle"] = "Select a removable USB drive, an internal computer hard disk, or browse for a specific folder",
        ["RefreshDrivesButton"] = "Refresh",
        ["BrowseFolderButton"] = "📁 Browse folder...",
        ["BrowseFolderDialogTitle"] = "Select the folder you want to scan and clean",
        ["NoUsbDetected"] = "No available drives found.",
        ["NoLabel"] = "NO LABEL",
        ["TagUsbDrive"] = "[USB]",
        ["TagInternalDrive"] = "[LOCAL DISK]",
        ["DriveFormatPattern"] = "{0} [{1}] — {2} free of {3} ({4})",
        ["FolderFormatPattern"] = "[FOLDER] {0}",
        ["InternalDriveWarning"] = "⚠ Warning: When cleaning a computer hard drive, some installed applications, programs, or games may be altered due to the similarity of certain files (such as README, *.tmp, or *.bak).",

        // Opciones de limpieza (macOS + Windows + Linux + Smart TV / Android)
        ["OptionsSectionTitle"] = "Cleaning Configuration",
        ["BtnSelectAll"] = "☑ Select all",
        ["BtnDeselectAll"] = "☐ Unselect all",
        ["BadgeMacOs"] = "macOS",
        ["BadgeWindows"] = "Windows",
        ["BadgeLinux"] = "Linux",
        ["BadgeSmartTv"] = "Smart TV / Android",
        ["OptAppleDoubleTitle"] = "AppleDouble Files & Mac Archive Folders (._*, __MACOSX, .AppleDouble)",
        ["OptAppleDoubleDesc"] = "Reason: Prevents duplicate ghost files (._*) and __MACOSX folders (from Mac ZIPs) that cause playback errors on car stereos, DJ players, and Smart TVs.",
        ["OptDsStoreTitle"] = "Mac Folder View Settings & Icons (.DS_Store, .VolumeIcon.icns, .apdisk)",
        ["OptDsStoreDesc"] = "Reason: Removes hidden window layout files created by Finder in every folder that clutter file explorers on Windows and media devices.",
        ["OptSpotlightTrashTitle"] = "Spotlight Indexes, QuickLook & Trash (.Spotlight-V100/V200, .Trashes, .fseventsd, .ql_tcache)",
        ["OptSpotlightTrashDesc"] = "Reason: Reclaims real free space held by the hidden Mac trash (.Trashes) and deletes indexing and thumbnail caches (.TemporaryItems, .QuickLook, .hotfiles.btree).",
        ["OptWinCacheTitle"] = "Thumbnail Caches & System Volume Information (Thumbs.db, desktop.ini, WPSettings.dat)",
        ["OptWinCacheDesc"] = "Reason: Deletes hidden Explorer caches (ehthumbs.db, ZbThumbnail.info) and cleans System Volume Information so car radios don't read that hidden folder as \"Folder 01\".",
        ["OptWinRecycleTitle"] = "Windows Recycle Bin & Repair Fragments ($RECYCLE.BIN, FOUND.000, *.chk)",
        ["OptWinRecycleDesc"] = "Reason: Frees space occupied by the hidden Windows Recycle Bin on the USB ($RECYCLE.BIN, RECYCLER) and removes orphaned CHKDSK repair fragments.",
        ["OptWinTempReadmeTitle"] = "Residual README., Office Locks & Temp Files (README., ~$*, *.tmp, *.bak)",
        ["OptWinTempReadmeDesc"] = "Reason: Removes auto-generated README. files (including invalid trailing-dot names), hidden Word/Excel lock files (~$*, ~WRL*.tmp), and temp folders (msdownld.tmp).",
        ["OptLinuxTitle"] = "Linux Trash, Folder Settings & Temp Files (.Trash-1000, .directory, .fuse_hidden*)",
        ["OptLinuxDesc"] = "Reason: Frees space trapped in hidden Linux trash folders (.Trash-*), cleans KDE/GNOME folder view files (.directory, .hidden), and deletes FUSE mount and editor swap locks (.*.swp).",
        ["OptSmartTvTitle"] = "Smart TV & Android Mobile Junk (LOST.DIR, LG Smart TV, .thumbnails, empty Android)",
        ["OptSmartTvDesc"] = "Reason: Plugging a USB into a Smart TV or Android phone via OTG creates dozens of empty folders (Android, Alarms, Ringtones, LOST.DIR) and heavy caches (.thumbdata*, .database_uuid, .estrongs). Cleans them while keeping your files safe.",
        ["OptAutoEjectTitle"] = "Automatically eject USB drive when cleaning finishes",

        // Apartado de Ajustes (Settings)
        ["SettingsPanelTitle"] = "Application Settings",
        ["SettingsPanelSubtitle"] = "Configure language, automatic updates, background behavior, and view version & legal info",
        ["SettingsBackButton"] = "← Back to Home",
        ["SettingsGeneralHeader"] = "Language, Updates & System",
        ["SettingsLangTitle"] = "Interface Language (Idioma)",
        ["SettingsLangDesc"] = "Changes all texts, buttons, activity logs, and alerts in real time without restarting.",
        ["OptAutoUpdateTitle"] = "Automatic updates",
        ["OptAutoUpdateDesc"] = "Automatically check on startup if a newer version of CleanMacUSB is available on GitHub.",
        ["BtnCheckUpdatesNow"] = "🔄 Check for updates",
        ["BtnCheckingUpdates"] = "Checking...",
        ["UpdateStatusIdle"] = "Currently installed version: v1.0.0",
        ["UpdateStatusChecking"] = "Checking for newer versions on the official repository...",
        ["UpdateStatusUpToDate"] = "✔ Up to date: you already have the latest version installed (v{0}).",
        ["UpdateStatusAvailable"] = "⬇ New version v{0} available! Click to download and update.",
        ["UpdateStatusOffline"] = "Could not connect to the update server. Please check your connection.",
        ["UpdatePromptTitle"] = "New Update Available (v{0})",
        ["UpdatePromptBody"] = "A new version of CleanMacUSB (v{0}) is available.\nCurrently installed version: v{1}\n\n{2}\n\nWould you like to automatically download and install the update now?",
        ["UpdateDownloadingStatus"] = "Downloading installer for version v{0}...",
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
        ["BtnDonateCoffee"] = "☕ Buy me a coffee (PayPal)",
        ["DonateTooltip"] = "Support the free development of CleanMacUSB by Josan Sánchez (paypal.me/josansancheez)",

        // Botones principales
        ["BtnCleanUsb"] = "Clean Now",
        ["BtnEjectUsb"] = "Safely Eject USB",
        ["StatusReady"] = "Ready. Select a USB drive, hard disk, or folder and click \"Clean Now\".",
        ["StatusDrivesUpdated"] = "Drive list updated ({0} available).",
        ["StatusFolderSelected"] = "Custom folder selected for cleaning: {0}",
        ["StatusNewUsbConnected"] = "New USB drive detected: {0}",
        ["StatusUsbRemoved"] = "A USB drive was disconnected.",

        // Panel / Modal de Registro (Log)
        ["LogTabTitle"] = "Cleaning Activity Log",
        ["LogStatusScanning"] = "Scanning and cleaning {0}...",
        ["LogStatusCompleted"] = "Cleaning successfully completed on {0}",
        ["LogStatusAborted"] = "Cleaning aborted by user on {0}",
        ["LogStatusNothingFound"] = "Cleaning finished: {0} was already completely clean.",
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
        ["LogNoItemsMessage"] = "No residual junk files or folders were found in the selected location.",
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
        ["LogFileDrive"] = "Scanned Target",

        // Alertas y expulsión segura
        ["ConfirmCleanTitle"] = "Confirm Cleaning",
        ["ConfirmCleanBody"] = "Are you sure you want to start cleaning residual junk files in the following location?\n\n{0}\n\nFound residual files will be permanently deleted.",
        ["MsgSelectDriveTitle"] = "No Target Selected",
        ["MsgSelectDriveBody"] = "Please select a USB drive, an internal hard disk, or browse for a folder before continuing.",
        ["MsgSelectOptionTitle"] = "No Cleaning Options Selected",
        ["MsgSelectOptionBody"] = "Please check at least one category of residual files to clean.",
        ["MsgEjectOnlyUsbTitle"] = "Eject Only Available for USB Drives",
        ["MsgEjectOnlyUsbBody"] = "Physical hardware ejection is only applicable to removable USB drives and external disks.",
        ["MsgEjectSuccessTitle"] = "Safe Eject Completed",
        ["MsgEjectSuccessBody"] = "Drive {0} has been safely dismounted and ejected.\nYou can now physically remove the USB device from your computer.",
        ["MsgEjectFailTitle"] = "Could Not Eject Drive",
        ["MsgEjectFailBody"] = "Windows reported that drive {0} is currently in use by another program or process.\nPlease close any open files or Explorer windows on that drive and try again.\n\nDetail: {1}",

        // Bandeja del sistema (System Tray)
        ["TrayTooltip"] = "CleanMacUSB v1.0 — Running in background",
        ["TrayMenuOpen"] = "Open CleanMacUSB",
        ["TrayMenuCleanNow"] = "Clean selected target",
        ["TrayMenuExit"] = "Exit completely",
        ["TrayMinimizedTitle"] = "CleanMacUSB is still running",
        ["TrayMinimizedBody"] = "The application stays minimized in the system tray. We will notify you when a new USB drive is plugged in.",
        ["TrayUsbDetectedTitle"] = "USB Flash Drive Detected ({0})",
        ["TrayUsbDetectedBody"] = "Drive {0} was plugged in. Click here to clean hidden junk files.",
        ["TrayUsbPromptQuestion"] = "USB drive {0} was just connected.\nWould you like to scan it and clean residual files right now?",

        // Ventana de Limpieza Completada + Apoyar con PayPal
        ["CleanDoneWindowTitle"] = "Cleaning Completed! — CleanMacUSB",
        ["CleanDoneHeading"] = "✔ Your drive has been successfully cleaned!",
        ["CleanDoneSubheading"] = "Deleted {0} residual files and {1} folders ({2} freed).",
        ["CleanDoneSupportTitle"] = "💛 If this app helped you, please help me back!",
        ["CleanDoneSupportBody"] = "CleanMacUSB is a 100% free tool created by Josan Sánchez. If this app has been useful and saved you time, you can support the project with a small contribution via PayPal so I can keep maintaining and improving it.",
        ["CleanDoneBtnPayPal"] = "☕ Support with PayPal",
        ["CleanDoneBtnClose"] = "Close",

        // Ventana Acerca de (About)
        ["AboutWindowTitle"] = "About CleanMacUSB",
        ["AboutVersionText"] = "CleanMacUSB v1.0 (Windows 11 / 64-bit Edition)",
        ["AboutCopyrightText"] = "Freeware & Proprietary Software.\nDeveloped by Josan Sánchez. All rights reserved.",
        ["AboutDescription"] = "High-performance native utility designed to eradicate hidden files from macOS (._*, .DS_Store, .Spotlight-V100), Windows (Thumbs.db, desktop.ini, $RECYCLE.BIN, README.), Linux (.Trash-1000, .directory), and Smart TVs (LOST.DIR, LG Smart TV).",
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
