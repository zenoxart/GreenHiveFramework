using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Backend.ModuleDeliveryService;
using GreenHive.NET.Monads;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("module-delivery");
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 200 * 1024 * 1024);
builder.Services.AddSingleton<PackageStorage>();
builder.Services.AddSingleton<PackageSigning>();
builder.Services.AddSingleton<PackageCatalog>();
builder.Services.AddHostedService<PackageScanner>();

var app = builder.Build();
app.UseGreenHiveDefaults();

var api = app.MapApi("/api/packages");
api.MapGet("/", (PackageCatalog catalog, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "modules.view").BindAsync(_ => catalog.ListAsync(ct)).ToHttp());
api.MapGet("/signing-key", (PackageSigning signing) => Results.Text(signing.PublicKeyPem, "application/x-pem-file"));
api.MapGet("/{moduleId}/{version}/info", (string moduleId, string version, PackageCatalog catalog, CancellationToken ct) =>
    catalog.FindAsync(moduleId, version, ct).ToHttp());
api.MapGet("/{moduleId}/{version}", async (string moduleId, string version, PackageCatalog catalog, PackageStorage storage, CancellationToken ct) =>
{
    var info = await catalog.FindAsync(moduleId, version, ct);
    return info.Bind(i => storage.FindFile(moduleId, version) is { } file
            ? Result.Success((Info: i, File: file))
            : Result.Failure<(GreenHive.NET.ModuleContracts.Api.PackageInfo Info, string File)>(Error.NotFound("packages.fileMissing", "modules:package.notFound")))
        .Match(x => Results.File(x.File, "application/zip", x.Info.FileName, enableRangeProcessing: true), ResultHttpMapper.Problem);
});
api.MapPut("/{moduleId}/{version}", async (string moduleId, string version, string? fileName, HttpRequest request,
    PackageCatalog catalog, PackageStorage storage, RequestContext context, CancellationToken ct) =>
{
    var allowed = Steps.Authorize(context.User, "modules.publish");
    if (allowed.IsFailure)
    {
        return ResultHttpMapper.Problem(allowed.Error);
    }

    if (storage.Folder(moduleId, version) is not { } folder)
    {
        return ResultHttpMapper.Problem(Error.Validation("packages.path", "modules:package.invalidVersion"));
    }

    Directory.CreateDirectory(folder);
    foreach (var old in Directory.GetFiles(folder, "*.zip"))
    {
        File.Delete(old);
    }

    var file = Path.Combine(folder, Path.GetFileName(fileName ?? $"{moduleId}-{version}.zip"));
    await using (var target = File.Create(file))
    {
        await request.Body.CopyToAsync(target, ct);
    }

    return (await catalog.RegisterAsync(moduleId, version, file, ct)).ToHttp();
});

app.Run();
