using System.IO.Compression;
using System.Text.Json;
using GreenHive.NET.Contracts;
using GreenHive.NET.ModuleContracts;
using GreenHive.NET.ModuleContracts.Api;
using GreenHive.NET.Monads;
using GreenHive.Wpf.Framework.Mvvm;
using GreenHive.Wpf.Framework.Security;

namespace GreenHive.Wpf.Framework.ModuleLoader;

/// <summary>Downloads a module package, validates it and extracts it into the module store.</summary>
public sealed class ModulePackageDownloader : AppObject
{
    private ModuleStore? _store;
    private ModulePackageValidator? _validator;

    private ModuleStore Store => _store ??= AppContext.Produce<ModuleStore>();

    private ModulePackageValidator Validator => _validator ??= AppContext.Produce<ModulePackageValidator>();

    private BackendConnection Backend => Context<ISecurityContext>().Backend;

    public async Task<Result<LocalModule>> DownloadAsync(ModuleManifest manifest, CancellationToken ct)
    {
        var info = await Backend.GetAsync<PackageInfo>($"/api/packages/{manifest.ModuleId}/{manifest.Version}/info", ct);
        if (!info.TryGetValue(out var package, out var infoError))
        {
            return infoError;
        }

        var temporary = Path.Combine(Path.GetTempPath(), $"greenhive-{manifest.ModuleId}-{manifest.Version}-{Guid.NewGuid():n}.zip");
        try
        {
            using (var request = Backend.CreateRequest(HttpMethod.Get, package.Url))
            using (var response = await Backend.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                if (!response.IsSuccessStatusCode)
                {
                    return Error.Unavailable("modules.package.download", "modules:package.downloadFailed").WithMetadata("status", (int)response.StatusCode);
                }

                await using var file = File.Create(temporary);
                await response.Content.CopyToAsync(file, ct);
            }

            var valid = await Validator.ValidateAsync(temporary, package, ct);
            if (valid.IsFailure)
            {
                return valid.Error;
            }

            var folder = Store.InstalledFolder(manifest.ModuleId, manifest.Version);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            ZipFile.ExtractToDirectory(temporary, folder);

            // The registry's manifest is authoritative (it was validated and published).
            await File.WriteAllTextAsync(Path.Combine(folder, "manifest.json"), JsonSerializer.Serialize(manifest, GreenHiveJson.Options), ct);
            AppContext.Log.Write($"Module {manifest.ModuleId} {manifest.Version} downloaded and verified", LogEntryType.Information, nameof(ModulePackageDownloader));
            return new LocalModule(manifest, folder, ModuleSource.Installed);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException)
        {
            return Try.Map(ex, "modules.package");
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (IOException)
            {
            }
        }
    }
}
