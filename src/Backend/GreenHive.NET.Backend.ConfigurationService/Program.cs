using System.Text.Json;
using GreenHive.NET.Backend.ConfigurationService;
using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Contracts.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("configuration-service");
builder.Services.AddScoped<SettingsStore>();

var app = builder.Build();
app.UseGreenHiveDefaults();

var api = app.MapApi("/api/config/settings");
api.MapGet("/", (SettingsStore store, CancellationToken ct) => store.ListAsync(ct).ToHttp());
api.MapGet("/{key}", (string key, SettingsStore store, CancellationToken ct) => store.GetAsync(key, ct).ToHttp());
api.MapPut("/{scope}/{key}", (string scope, string key, ConfigSettingUpdate update, SettingsStore store, CancellationToken ct) =>
    store.SetAsync(scope, key, update.Value, ct).ToHttp());
api.MapDelete("/{scope}/{key}", (string scope, string key, SettingsStore store, CancellationToken ct) => store.DeleteAsync(scope, key, ct).ToHttp());

// Services read effective settings for a tenant (and optionally a user).
app.MapInternal().MapGet("/settings/{key}", (string key, Guid tenant, Guid? user, SettingsStore store, CancellationToken ct) =>
    store.Effective(tenant, user, key, ct).ToHttp());

app.Run();
