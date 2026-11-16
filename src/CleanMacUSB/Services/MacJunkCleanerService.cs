using System.IO;
using CleanMacUSB.Models;

namespace CleanMacUSB.Services;

public sealed class CleanOptions
{
    // Categorías de macOS
    public bool CleanAppleDouble { get; init; } = true;
    public bool CleanDsStore { get; init; } = true;
    public bool CleanSpotlightAndTrash { get; init; } = true;

    // Categorías de Windows y archivos residuales adicionales
    public bool CleanWindowsCaches { get; init; } = true;
    public bool CleanWindowsRecycleAndFound { get; init; } = true;
    public bool CleanWindowsTempAndReadme { get; init; } = true;
}

public sealed class CleanSummary
{
    public int DeletedFilesCount { get; set; }
    public int DeletedFoldersCount { get; set; }
    public long TotalBytesFreed { get; set; }
    public bool WasAborted { get; set; }
}

/// <summary>
/// Motor de escaneo y limpieza recursiva de archivos y carpetas residuales de macOS y Windows.
/// Soporta atributos Hidden, System y ReadOnly en unidades FAT32, exFAT y NTFS.
/// </summary>
public static class MacJunkCleanerService
{
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

    public static async Task<CleanSummary> CleanDriveAsync(
        string driveRoot,
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
                var rootDir = new DirectoryInfo(driveRoot);
                if (!rootDir.Exists) return summary;

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

                // 1. Limpiar archivos directamente en la raíz
                ScanFilesInDirectory(rootDir, options, summary, logProgress, cancellationToken);
                completedSteps++;
                percentProgress.Report((completedSteps * 100.0) / totalSteps);

                // 2. Recorrer subdirectorios de la raíz
                foreach (var subDir in topDirs)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // System Volume Information: no eliminar la carpeta del sistema en sí, pero limpiar WPSettings.dat / IndexerVolumeGuid dentro si CleanWindowsCaches está activo
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

                    if (ShouldDeleteDirectory(subDir.Name, options))
                    {
                        TryDeleteDirectoryTree(subDir, summary, logProgress);
                    }
                    else if (!IsSkippedSystemFolder(subDir.Name, options))
                    {
                        ScanDirectoryRecursive(subDir, options, summary, logProgress, cancellationToken);
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

    private static bool IsSkippedSystemFolder(string dirName, CleanOptions options)
    {
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
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

            if (ShouldDeleteDirectory(subDir.Name, options))
            {
                TryDeleteDirectoryTree(subDir, summary, logProgress);
            }
            else
            {
                ScanDirectoryRecursive(subDir, options, summary, logProgress, cancellationToken);
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

    private static bool ShouldDeleteFile(string fileName, CleanOptions options)
    {
        // 1. macOS AppleDouble (._*)
        if (options.CleanAppleDouble && fileName.StartsWith("._", StringComparison.OrdinalIgnoreCase))
            return true;

        // 2. macOS .DS_Store, .VolumeIcon.icns, .apdisk
        if (options.CleanDsStore && DsStoreFileNames.Contains(fileName))
            return true;

        // 3. macOS Spotlight / Trashes / fseventsd / TemporaryItems
        if (options.CleanSpotlightAndTrash &&
            (SpotlightAndTrashNames.Contains(fileName) || fileName.StartsWith(".Trash-", StringComparison.OrdinalIgnoreCase)))
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

        return false;
    }

    private static bool ShouldDeleteDirectory(string dirName, CleanOptions options)
    {
        // 1. macOS AppleDouble folders
        if (options.CleanAppleDouble && AppleDoubleFolderNames.Contains(dirName))
            return true;

        // 2. macOS Spotlight & Trash folders
        if (options.CleanSpotlightAndTrash &&
            (SpotlightAndTrashNames.Contains(dirName) || dirName.StartsWith(".Trash-", StringComparison.OrdinalIgnoreCase)))
            return true;

        // 3. macOS .apdisk
        if (options.CleanDsStore && string.Equals(dirName, ".apdisk", StringComparison.OrdinalIgnoreCase))
            return true;

        // 4. Windows Recycle Bin ($RECYCLE.BIN, RECYCLER, RECYCLED) & CHKDSK FOUND.000
        if (options.CleanWindowsRecycleAndFound &&
            (WindowsRecycleDirNames.Contains(dirName) || IsFoundChkFolder(dirName)))
            return true;

        return false;
    }

    private static void TryDeleteFile(FileInfo file, CleanSummary summary, IProgress<CleanLogEntry> logProgress)
    {
        try
        {
            long size = 0;
            try { size = file.Length; } catch { /* Ignorar */ }

            // Quitar atributos ReadOnly / Hidden / System antes de borrar
            try
            {
                if ((file.Attributes & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)) != 0)
                {
                    File.SetAttributes(file.FullName, FileAttributes.Normal);
                }
            }
            catch
            {
                // Continuar intento de borrado
            }

            file.Delete();

            summary.DeletedFilesCount++;
            summary.TotalBytesFreed += size;

            logProgress.Report(new CleanLogEntry
            {
                Kind = CleanItemKind.File,
                FullPath = file.FullName,
                SizeBytes = size
            });
        }
        catch
        {
            // Ignorar si algún archivo puntual está bloqueado por el sistema
        }
    }

    private static void TryDeleteDirectoryTree(DirectoryInfo dir, CleanSummary summary, IProgress<CleanLogEntry> logProgress)
    {
        try
        {
            long folderBytes = NormalizeAndCalculateDirectorySize(dir);

            dir.Delete(recursive: true);

            summary.DeletedFoldersCount++;
            summary.TotalBytesFreed += folderBytes;

            logProgress.Report(new CleanLogEntry
            {
                Kind = CleanItemKind.Folder,
                FullPath = dir.FullName,
                SizeBytes = folderBytes
            });
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
    private static long NormalizeAndCalculateDirectorySize(DirectoryInfo dir)
    {
        long total = 0;

        try
        {
            dir.Attributes = FileAttributes.Normal;
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
                try
                {
                    total += f.Length;
                    f.Attributes = FileAttributes.Normal;
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
                try
                {
                    d.Attributes = FileAttributes.Normal;
                }
                catch { /* Ignorar */ }
            }
        }
        catch
        {
            // Ignorar errores de acceso parcial
        }

        return total;
    }
}
