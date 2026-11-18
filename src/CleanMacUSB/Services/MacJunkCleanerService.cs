using System.IO;
using CleanMacUSB.Models;

namespace CleanMacUSB.Services;

public sealed class CleanOptions
{
    // Categorías de macOS
    public bool CleanAppleDouble { get; init; } = true;
    public bool CleanDsStore { get; init; } = true;
    public bool CleanSpotlightAndTrash { get; init; } = true;

    // Categorías de Windows
    public bool CleanWindowsCaches { get; init; } = true;
    public bool CleanWindowsRecycleAndFound { get; init; } = true;
    public bool CleanWindowsTempAndReadme { get; init; } = true;

    // Categorías de Linux y Smart TV / Android
    public bool CleanLinuxJunk { get; init; } = true;
    public bool CleanSmartTvJunk { get; init; } = true;
}

public sealed class CleanSummary
{
    public int DeletedFilesCount { get; set; }
    public int DeletedFoldersCount { get; set; }
    public long TotalBytesFreed { get; set; }
    public bool WasAborted { get; set; }
    public bool IsDryRun { get; set; }
}

/// <summary>
/// Motor de escaneo y limpieza recursiva de archivos y carpetas residuales de macOS, Windows, Linux, Smart TVs y Android.
/// Soporta rutas extendidas (\\?\), nombres con punto final (README.), atributos Hidden/System/ReadOnly,
/// protección contra bucles de enlaces simbólicos (ReparsePoint) y protección de carpetas críticas del SO.
/// </summary>
public static class MacJunkCleanerService
{
    // Carpetas críticas del sistema operativo Windows que nunca deben escanearse al limpiar un disco interno (ej. C:\)
    private static readonly HashSet<string> CriticalOsRootDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Windows",
        "Program Files",
        "Program Files (x86)",
        "ProgramData",
        "Recovery",
        "$WinREAgent",
        "$SysReset",
        "PerfLogs",
        "Config.Msi",
        "Documents and Settings",
        "MSOCache",
        "bootmgr",
        "EFI"
    };

    private static readonly HashSet<string> AppleDoubleFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "__MACOSX",
        ".MACOSX",
        ".AppleDouble",
        ".AppleDB",
        ".AppleDesktop"
    };

    private static readonly HashSet<string> DsStoreFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".DS_Store",
        ".VolumeIcon.icns",
        ".apdisk",
        ".com.apple.timemachine.donotpresent",
        ".metadata_never_index"
    };

    private static readonly HashSet<string> SpotlightAndTrashNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".Spotlight-V100",
        ".Spotlight-V200",
        ".Trashes",
        ".fseventsd",
        ".TemporaryItems",
        ".DocumentRevisions-V100",
        ".PKInstallSandboxManager",
        ".PKInstallSandboxManager-SystemSoftware",
        ".ql_tcache",
        ".QuickLook",
        ".hotfiles.btree"
    };

    private static readonly HashSet<string> WindowsCacheFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Thumbs.db",
        "ehthumbs.db",
        "ehthumbs_vista.db",
        "desktop.ini",
        "WPSettings.dat",
        "IndexerVolumeGuid",
        "ZbThumbnail.info"
    };

    private static readonly HashSet<string> WindowsRecycleDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$RECYCLE.BIN",
        "RECYCLER",
        "RECYCLED",
        "Recycle",
        ".Recycle",
        "$Recycle"
    };

    private static readonly HashSet<string> WindowsTempFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "msdownld.tmp"
    };

    private static readonly HashSet<string> ReadmeResidualFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "README."
    };

    private static readonly HashSet<string> LinuxJunkFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".directory",
        ".hidden"
    };

    // Carpetas residuales creadas por Smart TVs y dispositivos Android (OTG / Android TV)
    private static readonly HashSet<string> SmartTvAndAndroidJunkFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "LOST.DIR",
        ".android_secure",
        "LG Smart TV",
        ".lg_tv",
        ".Samsung",
        ".DEV_INFO",
        ".thumbnails",
        "mstpvr",
        "_mstpvr",
        ".tv_temp",
        ".Timeshift",
        ".pvr",
        ".estrongs",
        ".utsystemconfig",
        ".DataStorage",
        ".Trash-Android"
    };

    // Carpetas esqueleto que Android y Android TV crean vacías en la raíz del USB al conectarlo (solo se borran si están 100% vacías de archivos del usuario)
    private static readonly HashSet<string> AndroidEmptySkeletonRootDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "Android",
        "Alarms",
        "Audiobooks",
        "Notifications",
        "Podcasts",
        "Ringtones",
        "Recordings"
    };

    private static readonly HashSet<string> SmartTvAndAndroidJunkFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".database_uuid",
        "albumart.pamp",
        "device_info.xml",
        ".rec_info",
        ".VolumeGuid"
    };

    public static async Task<CleanSummary> CleanDriveAsync(
        string targetPath,
        CleanOptions options,
        IProgress<CleanLogEntry> logProgress,
        IProgress<double> percentProgress,
        CancellationToken cancellationToken = default,
        bool dryRun = false)
    {
        return await Task.Run(() =>
        {
            var summary = new CleanSummary { IsDryRun = dryRun };
            try
            {
                var rootDir = new DirectoryInfo(targetPath);
                if (!rootDir.Exists) return summary;

                bool isDriveRoot = string.Equals(
                    rootDir.FullName.TrimEnd(Path.DirectorySeparatorChar),
                    rootDir.Root.FullName.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);

                // Obtener subdirectorios de primer nivel para calcular progreso real
                DirectoryInfo[] topDirs;
                try
                {
                    topDirs = rootDir.GetDirectories("*", new EnumerationOptions
                    {
                        AttributesToSkip = 0,
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = false
                    });
                }
                catch
                {
                    topDirs = Array.Empty<DirectoryInfo>();
                }

                int totalSteps = Math.Max(1, topDirs.Length + 1);
                int completedSteps = 0;

                // 1. Analizar/limpiar archivos directamente en la carpeta o raíz seleccionada
                ScanFilesInDirectory(rootDir, options, summary, logProgress, cancellationToken, dryRun);
                completedSteps++;
                percentProgress.Report((completedSteps * 100.0) / totalSteps);

                // 2. Recorrer subdirectorios de primer nivel
                foreach (var subDir in topDirs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Si estamos en la raíz de un disco duro, proteger carpetas críticas del sistema operativo Windows
                    if (isDriveRoot && CriticalOsRootDirectories.Contains(subDir.Name))
                    {
                        completedSteps++;
                        percentProgress.Report((completedSteps * 100.0) / totalSteps);
                        continue;
                    }

                    // System Volume Information: limpiar WPSettings.dat / IndexerVolumeGuid dentro y, si queda vacía en un USB (FAT32/exFAT), eliminar la carpeta vacía
                    if (string.Equals(subDir.Name, "System Volume Information", StringComparison.OrdinalIgnoreCase))
                    {
                        if (options.CleanWindowsCaches)
                        {
                            ScanFilesInDirectory(subDir, options, summary, logProgress, cancellationToken, dryRun);
                            if (IsEmptySkeletonFolder(subDir))
                            {
                                TryDeleteDirectoryTree(subDir, summary, logProgress, cancellationToken, dryRun);
                            }
                        }
                        completedSteps++;
                        percentProgress.Report((completedSteps * 100.0) / totalSteps);
                        continue;
                    }

                    if (ShouldDeleteDirectory(subDir, options, isRootLevel: isDriveRoot))
                    {
                        TryDeleteDirectoryTree(subDir, summary, logProgress, cancellationToken, dryRun);
                    }
                    else if (!IsSkippedSystemFolder(subDir, options))
                    {
                        ScanDirectoryRecursive(subDir, options, summary, logProgress, cancellationToken, currentDepth: 1, dryRun);
                    }

                    completedSteps++;
                    percentProgress.Report((completedSteps * 100.0) / totalSteps);
                }

                percentProgress.Report(100.0);
                return summary;
            }
            catch (OperationCanceledException)
            {
                summary.WasAborted = true;
                return summary;
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// Elimina únicamente los archivos y carpetas específicos seleccionados por el usuario tras un análisis previo.
    /// </summary>
    public static async Task<CleanSummary> DeleteSelectedItemsAsync(
        IReadOnlyList<CleanLogEntry> selectedItems,
        IProgress<CleanLogEntry> logProgress,
        IProgress<double> percentProgress,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var summary = new CleanSummary { IsDryRun = false };
            try
            {
                int total = Math.Max(1, selectedItems.Count);
                int processed = 0;

                foreach (var item in selectedItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (item.Kind == CleanItemKind.File)
                    {
                        var fi = new FileInfo(item.FullPath);
                        if (fi.Exists)
                        {
                            TryDeleteFile(fi, summary, logProgress, dryRun: false);
                        }
                    }
                    else if (item.Kind == CleanItemKind.Folder)
                    {
                        var di = new DirectoryInfo(item.FullPath);
                        if (di.Exists)
                        {
                            TryDeleteDirectoryTree(di, summary, logProgress, cancellationToken, dryRun: false);
                        }
                    }

                    processed++;
                    percentProgress.Report((processed * 100.0) / total);
                }

                percentProgress.Report(100.0);
                return summary;
            }
            catch (OperationCanceledException)
            {
                summary.WasAborted = true;
                return summary;
            }
        }, CancellationToken.None);
    }

    private static bool IsSkippedSystemFolder(DirectoryInfo dir, CleanOptions options)
    {
        string dirName = dir.Name;

        // Evitar seguir enlaces simbólicos o Junction Points (ReparsePoint) para impedir bucles infinitos en NTFS/Linux
        try
        {
            if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        catch
        {
            return true;
        }

        // Nunca escanear AppData ni carpetas de repositorios/paquetes internos cuando se escanea un disco local o carpeta
        if (string.Equals(dirName, "AppData", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dirName, ".git", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dirName, "node_modules", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!options.CleanWindowsRecycleAndFound)
        {
            if (WindowsRecycleDirNames.Contains(dirName) || IsFoundChkFolder(dirName))
                return true;
        }
        return false;
    }

    private static bool IsFoundChkFolder(string dirName)
    {
        if (!dirName.StartsWith("FOUND.", StringComparison.OrdinalIgnoreCase) || dirName.Length < 7)
            return false;

        for (int i = 6; i < dirName.Length; i++)
        {
            if (!char.IsDigit(dirName[i])) return false;
        }
        return true;
    }

    private static void ScanDirectoryRecursive(
        DirectoryInfo currentDir,
        CleanOptions options,
        CleanSummary summary,
        IProgress<CleanLogEntry> logProgress,
        CancellationToken cancellationToken,
        int currentDepth,
        bool dryRun)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Límite de seguridad de profundidad recursiva (evita desbordamiento de pila en sistemas de archivos corruptos)
        if (currentDepth > 64)
            return;

        // Primero escanear archivos del directorio actual
        ScanFilesInDirectory(currentDir, options, summary, logProgress, cancellationToken, dryRun);

        // Luego recorrer subdirectorios (incluyendo ocultos y de sistema)
        DirectoryInfo[] subDirs;
        try
        {
            subDirs = currentDir.GetDirectories("*", new EnumerationOptions
            {
                AttributesToSkip = 0, // No saltar Hidden ni System
                IgnoreInaccessible = true,
                RecurseSubdirectories = false
            });
        }
        catch
        {
            return;
        }

        foreach (var subDir in subDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldDeleteDirectory(subDir, options, isRootLevel: false))
            {
                TryDeleteDirectoryTree(subDir, summary, logProgress, cancellationToken, dryRun);
            }
            else if (!IsSkippedSystemFolder(subDir, options))
            {
                ScanDirectoryRecursive(subDir, options, summary, logProgress, cancellationToken, currentDepth + 1, dryRun);
            }
        }
    }

    private static void ScanFilesInDirectory(
        DirectoryInfo dir,
        CleanOptions options,
        CleanSummary summary,
        IProgress<CleanLogEntry> logProgress,
        CancellationToken cancellationToken,
        bool dryRun)
    {
        FileInfo[] files;
        try
        {
            files = dir.GetFiles("*", new EnumerationOptions
            {
                AttributesToSkip = 0, // Incluye FileAttributes.Hidden | FileAttributes.System
                IgnoreInaccessible = true,
                RecurseSubdirectories = false
            });
        }
        catch
        {
            return;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ShouldDeleteFile(file.Name, options))
            {
                TryDeleteFile(file, summary, logProgress, dryRun);
            }
        }
    }

    public static bool ShouldDeleteFile(string fileName, CleanOptions options)
    {
        // 1. macOS AppleDouble (._*)
        if (options.CleanAppleDouble && fileName.StartsWith("._", StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. macOS .DS_Store, .VolumeIcon.icns, .apdisk
        if (options.CleanDsStore && DsStoreFileNames.Contains(fileName))
            return true;

        // 3. macOS Spotlight / Trashes / fseventsd / TemporaryItems / QuickLook / .hotfiles.btree
        if (options.CleanSpotlightAndTrash && SpotlightAndTrashNames.Contains(fileName))
            return true;

        // 4. Windows Caches (Thumbs.db, ehthumbs.db, desktop.ini, *.ini, WPSettings.dat, IndexerVolumeGuid, ZbThumbnail.info)
        if (options.CleanWindowsCaches &&
            (WindowsCacheFileNames.Contains(fileName) ||
             fileName.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)))
            return true;

        // 5. Windows CHKDSK fragments (*.chk)
        if (options.CleanWindowsRecycleAndFound && fileName.EndsWith(".chk", StringComparison.OrdinalIgnoreCase))
            return true;

        // 6. Residual README., Office locks (~$*, ~WRL*.tmp), and temp/backup files (*.tmp, *.bak)
        if (options.CleanWindowsTempAndReadme)
        {
            if (ReadmeResidualFileNames.Contains(fileName))
                return true;

            if (fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("~WRL", StringComparison.OrdinalIgnoreCase))
                return true;

            if (fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 7. Linux Junk Files (.directory, .hidden, .fuse_hidden*, .nfs*, .goutputstream-*, .*.swp, .*.swo)
        if (options.CleanLinuxJunk)
        {
            if (LinuxJunkFileNames.Contains(fileName))
                return true;

            if (fileName.StartsWith(".fuse_hidden", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith(".nfs", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith(".goutputstream-", StringComparison.OrdinalIgnoreCase))
                return true;

            if (fileName.StartsWith(".", StringComparison.OrdinalIgnoreCase) &&
                (fileName.EndsWith(".swp", StringComparison.OrdinalIgnoreCase) ||
                 fileName.EndsWith(".swo", StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // 8. Smart TV / Android Junk Files (.database_uuid, .thumbdata*, albumart.pamp, device_info.xml, .rec_info)
        if (options.CleanSmartTvJunk)
        {
            if (SmartTvAndAndroidJunkFileNames.Contains(fileName) ||
                fileName.StartsWith(".thumbdata", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool ShouldDeleteDirectory(DirectoryInfo dir, CleanOptions options, bool isRootLevel = false)
    {
        string dirName = dir.Name;

        // 1. macOS AppleDouble & __MACOSX folders (creadas al descomprimir ZIPs de Mac)
        if (options.CleanAppleDouble && AppleDoubleFolderNames.Contains(dirName))
            return true;

        // 2. macOS Spotlight (.Spotlight-V100, .Spotlight-V200), QuickLook (.ql_tcache) & Trash folders
        if (options.CleanSpotlightAndTrash && SpotlightAndTrashNames.Contains(dirName))
            return true;

        // 3. macOS .apdisk
        if (options.CleanDsStore && string.Equals(dirName, ".apdisk", StringComparison.OrdinalIgnoreCase))
            return true;

        // 4. Windows Recycle Bin ($RECYCLE.BIN, RECYCLER, RECYCLED) & CHKDSK FOUND.000
        if (options.CleanWindowsRecycleAndFound &&
            (WindowsRecycleDirNames.Contains(dirName) || IsFoundChkFolder(dirName)))
            return true;

        // 5. Windows temporary folders (msdownld.tmp)
        if (options.CleanWindowsTempAndReadme && WindowsTempFolderNames.Contains(dirName))
            return true;

        // 6. Linux Trash folders (.Trash, .Trash-1000, .Trash-*)
        if (options.CleanLinuxJunk || options.CleanSpotlightAndTrash)
        {
            if (string.Equals(dirName, ".Trash", StringComparison.OrdinalIgnoreCase) ||
                dirName.StartsWith(".Trash-", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 7. Smart TV & Android folders (LOST.DIR, LG Smart TV, .android_secure, .thumbnails, .estrongs, etc.)
        if (options.CleanSmartTvJunk)
        {
            if (SmartTvAndAndroidJunkFolderNames.Contains(dirName))
                return true;

            // Carpeta "Android" o carpetas esqueleto que Android/Smart TV crea vacías en la raíz (Alarms, Ringtones, Podcasts...)
            if (string.Equals(dirName, "Android", StringComparison.OrdinalIgnoreCase) && IsEmptySkeletonFolder(dir))
                return true;

            if (isRootLevel && AndroidEmptySkeletonRootDirs.Contains(dirName) && IsEmptySkeletonFolder(dir))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Verifica si una carpeta fue generada como esqueleto vacío (sin archivos reales del usuario).
    /// </summary>
    private static bool IsEmptySkeletonFolder(DirectoryInfo dir)
    {
        try
        {
            var files = dir.GetFiles("*", new EnumerationOptions
            {
                AttributesToSkip = 0,
                IgnoreInaccessible = true,
                RecurseSubdirectories = true
            });

            return files.All(f =>
                string.Equals(f.Name, ".nomedia", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.Name, ".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.Name, "Thumbs.db", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.Name, "desktop.ini", StringComparison.OrdinalIgnoreCase) ||
                f.Name.StartsWith("._", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Convierte una ruta estándar en ruta extendida Win32 (\\?\) para poder borrar archivos con punto final
    /// como "README." o rutas mayores de 260 caracteres creadas por macOS/Linux.
    /// </summary>
    private static string ToExtendedWin32Path(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath) || fullPath.StartsWith(@"\\?\", StringComparison.Ordinal))
            return fullPath;

        if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
            return @"\\?\UNC\" + fullPath.Substring(2);

        return @"\\?\" + fullPath;
    }

    private static void TryDeleteFile(FileInfo file, CleanSummary summary, IProgress<CleanLogEntry> logProgress, bool dryRun)
    {
        string fullPath = file.FullName;
        string extPath = ToExtendedWin32Path(fullPath);

        try
        {
            long size = 0;
            try { size = file.Length; } catch { /* Ignorar */ }

            if (!dryRun)
            {
                // Quitar atributos ReadOnly / Hidden / System usando ruta extendida (soporta "README.")
                try
                {
                    File.SetAttributes(extPath, FileAttributes.Normal);
                }
                catch
                {
                    // Continuar intento de borrado
                }

                File.Delete(extPath);
            }

            summary.DeletedFilesCount++;
            summary.TotalBytesFreed += size;

            logProgress.Report(new CleanLogEntry
            {
                Kind = CleanItemKind.File,
                FullPath = fullPath,
                SizeBytes = size
            });
        }
        catch
        {
            // Ignorar si algún archivo puntual está bloqueado por otro proceso del sistema
        }
    }

    private static void TryDeleteDirectoryTree(
        DirectoryInfo dir,
        CleanSummary summary,
        IProgress<CleanLogEntry> logProgress,
        CancellationToken cancellationToken,
        bool dryRun)
    {
        string fullPath = dir.FullName;
        string extPath = ToExtendedWin32Path(fullPath);

        try
        {
            long folderBytes = NormalizeAndCalculateDirectorySize(dir, cancellationToken, dryRun);

            if (!dryRun)
            {
                Directory.Delete(extPath, recursive: true);
            }

            summary.DeletedFoldersCount++;
            summary.TotalBytesFreed += folderBytes;

            logProgress.Report(new CleanLogEntry
            {
                Kind = CleanItemKind.Folder,
                FullPath = fullPath,
                SizeBytes = folderBytes
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Ignorar si no se pudo eliminar completamente
        }
    }

    /// <summary>
    /// Calcula el tamaño total de una carpeta residual y (si no es vista previa) normaliza los atributos
    /// ReadOnly / Hidden / System de todos sus elementos internos para garantizar su eliminación.
    /// </summary>
    private static long NormalizeAndCalculateDirectorySize(DirectoryInfo dir, CancellationToken cancellationToken, bool dryRun)
    {
        long total = 0;

        if (!dryRun)
        {
            try
            {
                File.SetAttributes(ToExtendedWin32Path(dir.FullName), FileAttributes.Normal);
            }
            catch { /* Ignorar */ }
        }

        try
        {
            var files = dir.GetFiles("*", new EnumerationOptions
            {
                AttributesToSkip = 0,
                IgnoreInaccessible = true,
                RecurseSubdirectories = true
            });

            foreach (var f in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    total += f.Length;
                    if (!dryRun)
                    {
                        File.SetAttributes(ToExtendedWin32Path(f.FullName), FileAttributes.Normal);
                    }
                }
                catch { /* Ignorar */ }
            }

            if (!dryRun)
            {
                var subDirs = dir.GetDirectories("*", new EnumerationOptions
                {
                    AttributesToSkip = 0,
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true
                });

                foreach (var d in subDirs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        File.SetAttributes(ToExtendedWin32Path(d.FullName), FileAttributes.Normal);
                    }
                    catch { /* Ignorar */ }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Ignorar errores de acceso parcial
        }

        return total;
    }
}
