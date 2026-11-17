using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace CleanMacUSB.Services;

public sealed class UpdateCheckResult
{
    public bool CheckSucceeded { get; init; }
    public bool IsUpdateAvailable { get; init; }
    public string CurrentVersion { get; init; } = UpdateService.CurrentVersionString;
    public string LatestVersion { get; init; } = UpdateService.CurrentVersionString;
    public string DownloadUrl { get; init; } = string.Empty;
    public string ReleaseNotes { get; init; } = string.Empty;
}

/// <summary>
/// Servicio de comprobación y descarga de actualizaciones conectado al repositorio oficial de GitHub (josansancheez/cleanmacusb).
/// </summary>
public static class UpdateService
{
    public const string CurrentVersionString = "1.0.0";
    private const string VersionJsonUrl = "https://raw.githubusercontent.com/josansancheez/cleanmacusb/main/version.json";
    private const string GitHubReleasesApiUrl = "https://api.github.com/repos/josansancheez/cleanmacusb/releases/latest";
    private const string GitHubRepoReleasesPageUrl = "https://github.com/josansancheez/cleanmacusb/releases";
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, StringBuilder? packageFullName);

    /// <summary>
    /// Detecta automáticamente si la aplicación se ha instalado y se ejecuta desde la Microsoft Store (paquete MSIX/AppX).
    /// Si devuelve true, se oculta el actualizador propio porque la Microsoft Store gestiona las actualizaciones.
    /// </summary>
    public static bool IsRunningAsMicrosoftStorePackage()
    {
        try
        {
            uint length = 0;
            int result = GetCurrentPackageFullName(ref length, null);
            if (result != APPMODEL_ERROR_NO_PACKAGE && length > 0)
            {
                return true;
            }

            string baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir) && baseDir.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch
        {
            // En versiones sin soporte de AppModel, es ejecución Win32 estándar
        }

        return false;
    }

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CleanMacUSB-Updater/1.0");
        return client;
    }

    public static async Task<UpdateCheckResult> CheckForUpdatesAsync(bool isSpanish)
    {
        var currentVer = new Version(CurrentVersionString);

        // 1. Consultar primero version.json en la rama main del repositorio GitHub
        try
        {
            using var response = await Http.GetAsync(VersionJsonUrl);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string remoteVerStr = root.TryGetProperty("version", out var vProp)
                    ? (vProp.GetString() ?? CurrentVersionString).TrimStart('v', 'V')
                    : CurrentVersionString;

                string downloadUrl = root.TryGetProperty("downloadUrl", out var dProp)
                    ? (dProp.GetString() ?? GitHubRepoReleasesPageUrl)
                    : GitHubRepoReleasesPageUrl;

                string notesKey = isSpanish ? "releaseNotesEs" : "releaseNotesEn";
                string notes = root.TryGetProperty(notesKey, out var nProp)
                    ? (nProp.GetString() ?? string.Empty)
                    : string.Empty;

                if (Version.TryParse(remoteVerStr, out var remoteVer))
                {
                    return new UpdateCheckResult
                    {
                        CheckSucceeded = true,
                        IsUpdateAvailable = remoteVer > currentVer,
                        CurrentVersion = CurrentVersionString,
                        LatestVersion = remoteVerStr,
                        DownloadUrl = downloadUrl,
                        ReleaseNotes = notes
                    };
                }
            }
        }
        catch
        {
            // Continuar con fallback a GitHub Releases API
        }

        // 2. Fallback: Consultar GitHub Releases API oficial
        try
        {
            using var response = await Http.GetAsync(GitHubReleasesApiUrl);
            if (response.IsSuccessStatusCode)
            {
                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tProp)
                    ? (tProp.GetString() ?? CurrentVersionString).TrimStart('v', 'V')
                    : CurrentVersionString;

                string body = root.TryGetProperty("body", out var bProp)
                    ? (bProp.GetString() ?? string.Empty)
                    : string.Empty;

                string downloadUrl = GitHubRepoReleasesPageUrl;
                if (root.TryGetProperty("assets", out var assets) &&
                    assets.ValueKind == JsonValueKind.Array &&
                    assets.GetArrayLength() > 0)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        if (asset.TryGetProperty("browser_download_url", out var urlProp))
                        {
                            string? candidate = urlProp.GetString();
                            if (!string.IsNullOrEmpty(candidate) && candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = candidate;
                                break;
                            }
                        }
                    }
                }

                if (Version.TryParse(tagName, out var remoteVer))
                {
                    return new UpdateCheckResult
                    {
                        CheckSucceeded = true,
                        IsUpdateAvailable = remoteVer > currentVer,
                        CurrentVersion = CurrentVersionString,
                        LatestVersion = tagName,
                        DownloadUrl = downloadUrl,
                        ReleaseNotes = body
                    };
                }
            }
            else if ((int)response.StatusCode == 404)
            {
                // El repositorio está conectado pero aún no tiene una versión superior publicada
                return new UpdateCheckResult
                {
                    CheckSucceeded = true,
                    IsUpdateAvailable = false,
                    CurrentVersion = CurrentVersionString,
                    LatestVersion = CurrentVersionString
                };
            }
        }
        catch
        {
            // Sin conexión a Internet o servidor inaccesible
        }

        return new UpdateCheckResult
        {
            CheckSucceeded = false,
            IsUpdateAvailable = false,
            CurrentVersion = CurrentVersionString,
            LatestVersion = CurrentVersionString
        };
    }

    public static async Task<bool> DownloadAndLaunchInstallerAsync(string downloadUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(downloadUrl))
                return false;

            // Si la URL es un archivo .exe directo, descargarlo a %TEMP% y ejecutarlo
            if (downloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                string tempInstallerPath = Path.Combine(Path.GetTempPath(), $"CleanMacUSB_Update_{Guid.NewGuid():N}.exe");
                using var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    await using (var fs = new FileStream(tempInstallerPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tempInstallerPath,
                        UseShellExecute = true
                    });
                    return true;
                }
            }

            // En caso contrario, abrir la página de descarga en el navegador predeterminado
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadUrl,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
