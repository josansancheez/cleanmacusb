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

    // Categorías de Linux y Smart TV
    public bool CleanLinuxJunk { get; init; } = true;
    public bool CleanSmartTvJunk { get; init; } = true;
}

public sealed class CleanSummary
{
    public int DeletedFilesCount { get; set; }
    public int DeletedFoldersCount { get; set; }
    public long TotalBytesFreed { get; set; }
    public bool WasAborted { get; set; }
}

/// <summary>
/// Motor de escaneo y limpieza recursiva de archivos y carpetas residuales de macOS, Windows, Linux y Smart TVs.
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
        ".Trashes",
        ".fseventsd",
        ".TemporaryItems",
        ".DocumentRevisions-V100",
        ".PKInstallSandboxManager",
        ".PKInstallSandboxManager-SystemSoftware"
    };

    private static readonly HashSet<string> WindowsCacheFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Thumbs.db",
        "ehthumbs.db",
        "ehthumbs_vista.db",
        "desktop.ini",
        "WPSettings.dat",
        "IndexerVolumeGuid"
    };

    private static readonly HashSet<string> WindowsRecycleDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "$RECYCLE.BIN",
        "RECYCLER",
        "RECYCLED"
    };

    private static readonly HashSet<string> ReadmeResidualFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "README.",
        "README",
        "README.txt"
    };

    private static readonly HashSet<string> LinuxJunkFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".directory",
        ".hidden"
    };

    private static readonly HashSet<string> SmartTvJunkFolderNames = new(StringComparer.OrdinalIgnoreCase)
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
        ".pvr"
    };

    private static readonly HashSet<string> SmartTvJunkFileNames = new(StringComparer.OrdinalIgnoreCase)
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
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var summary = new CleanSummary();
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

                // 1. Limpiar archivos directamente en la carpeta o raíz seleccionada
                ScanFilesInDirectory(rootDir, options, summary, logProgress, cancellationToken);
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

                    // System Volume Information: limpiar WPSettings.dat / IndexerVolumeGuid dentro si CleanWindowsCaches está activo
                    if (string.Equals(subDir.Name, "System Volume Information", StringComparison.OrdinalIgnoreCase))
                    {
                        if (options.CleanWindowsCaches)
                        {
                            ScanFilesInDirectory(subDir, options, summary, logProgress, cancellationToken);
                        }
                        completedSteps++;
                        percentProgress.Report((completedSteps * 100.0) / totalSteps);
                        continue;
                    }

                    if (ShouldDeleteDirectory(subDir, options))
                    {
                        TryDeleteDirectoryTree(subDir, summary, logProgress, cancellationToken);
                    }
                    else if (!IsSkippedSystemFolder(subDir, options))
                    {
                        ScanDirectoryRecursive(subDir, options, summary, logProgress, cancellationToken, currentDepth: 1);
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
        int currentDepth)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Límite de seguridad de profundidad recursiva (evita desbordamiento de pila en sistemas de archivos corruptos)
        if (currentDepth > 64)
            return;

        // Primero escanear archivos del directorio actual
        ScanFilesInDirectory(currentDir, options, summary, logProgress, cancellationToken);

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

            if (ShouldDeleteDirectory(subDir, options))
            {
                TryDeleteDirectoryTree(subDir, summary, logProgress, cancellationToken);
            }
            else if (!IsSkippedSystemFolder(subDir, options))
            {
                ScanDirectoryRecursive(subDir, options, summary, logProgress, cancellationToken, currentDepth + 1);
            }
        }
    }

    private static void ScanFilesInDirectory(
        DirectoryInfo dir,
        CleanOptions options,
        CleanSummary summary,
        IProgress<CleanLogEntry> logProgress,
        CancellationToken cancellationToken)
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
                TryDeleteFile(file, summary, logProgress);
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

        // 3. macOS Spotlight / Trashes / fseventsd / TemporaryItems
        if (options.CleanSpotlightAndTrash && SpotlightAndTrashNames.Contains(fileName))
            return true;

        // 4. Windows Caches (Thumbs.db, ehthumbs.db, desktop.ini, WPSettings.dat, IndexerVolumeGuid)
        if (options.CleanWindowsCaches && WindowsCacheFileNames.Contains(fileName))
            return true;

        // 5. Windows CHKDSK fragments (*.chk)
        if (options.CleanWindowsRecycleAndFound && fileName.EndsWith(".chk", StringComparison.OrdinalIgnoreCase))
            return true;

        // 6. Residual README., Office locks (~$*), and temp/backup files (*.tmp, *.bak)
        if (options.CleanWindowsTempAndReadme)
        {
            if (ReadmeResidualFileNames.Contains(fileName))
                return true;

            if (fileName.StartsWith("~$", StringComparison.OrdinalIgnoreCase))
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

        // 8. Smart TV / Android TV Junk Files (.database_uuid, .thumbdata*, albumart.pamp, device_info.xml, .rec_info)
        if (options.CleanSmartTvJunk)
        {
            if (SmartTvJunkFileNames.Contains(fileName) ||
                fileName.StartsWith(".thumbdata", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool ShouldDeleteDirectory(DirectoryInfo dir, CleanOptions options)
    {
        string dirName = dir.Name;

        // 1. macOS AppleDouble folders
        if (options.CleanAppleDouble && AppleDoubleFolderNames.Contains(dirName))
            return true;

        // 2. macOS Spotlight & Trash folders
        if (options.CleanSpotlightAndTrash && SpotlightAndTrashNames.Contains(dirName))
            return true;

        // 3. macOS .apdisk
        if (options.CleanDsStore && string.Equals(dirName, ".apdisk", StringComparison.OrdinalIgnoreCase))
            return true;

        // 4. Windows Recycle Bin ($RECYCLE.BIN, RECYCLER, RECYCLED) & CHKDSK FOUND.000
        if (options.CleanWindowsRecycleAndFound &&
            (WindowsRecycleDirNames.Contains(dirName) || IsFoundChkFolder(dirName)))
            return true;

        // 5. Linux Trash folders (.Trash, .Trash-1000, .Trash-*)
        if (options.CleanLinuxJunk || options.CleanSpotlightAndTrash)
        {
            if (string.Equals(dirName, ".Trash", StringComparison.OrdinalIgnoreCase) ||
                dirName.StartsWith(".Trash-", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 6. Smart TV & Android TV folders (LOST.DIR, LG Smart TV, .android_secure, .thumbnails, Android vacía)
        if (options.CleanSmartTvJunk)
        {
            if (SmartTvJunkFolderNames.Contains(dirName))
                return true;

            // La carpeta "Android" creada automáticamente por Smart TVs Android TV (solo si está vacía o solo tiene estructura vacía data/media/obb)
            if (string.Equals(dirName, "Android", StringComparison.OrdinalIgnoreCase) && IsEmptySmartTvAndroidFolder(dir))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Verifica si una carpeta "Android" fue generada por una Smart TV como esqueleto vacío (sin archivos de usuario).
    /// </summary>
    private static bool IsEmptySmartTvAndroidFolder(DirectoryInfo androidDir)
    {
        try
        {
            var files = androidDir.GetFiles("*", new EnumerationOptions
            {
                AttributesToSkip = 0,
                IgnoreInaccessible = true,
                RecurseSubdirectories = true
            });

            return files.All(f =>
                string.Equals(f.Name, ".nomedia", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.Name, ".DS_Store", StringComparison.OrdinalIgnoreCase) ||
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

    private static void TryDeleteFile(FileInfo file, CleanSummary summary, IProgress<CleanLogEntry> logProgress)
    {
        string fullPath = file.FullName;
        string extPath = ToExtendedWin32Path(fullPath);

        try
        {
            long size = 0;
            try { size = file.Length; } catch { /* Ignorar */ }

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
        CancellationToken cancellationToken)
    {
        string fullPath = dir.FullName;
        string extPath = ToExtendedWin32Path(fullPath);

        try
        {
            long folderBytes = NormalizeAndCalculateDirectorySize(dir, cancellationToken);

            Directory.Delete(extPath, recursive: true);

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
    /// Calcula el tamaño total de una carpeta residual y normaliza los atributos
    /// ReadOnly / Hidden / System de todos sus elementos internos para garantizar su eliminación.
    /// </summary>
    private static long NormalizeAndCalculateDirectorySize(DirectoryInfo dir, CancellationToken cancellationToken)
    {
        long total = 0;

        try
        {
            File.SetAttributes(ToExtendedWin32Path(dir.FullName), FileAttributes.Normal);
        }
        catch { /* Ignorar */ }

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
                    File.SetAttributes(ToExtendedWin32Path(f.FullName), FileAttributes.Normal);
                }
                catch { /* Ignorar */ }
            }

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
