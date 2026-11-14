using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Backend.IdentityService;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("identity-service");
builder.Services.AddSingleton<IdentityRepository>();
builder.Services.AddScoped<Login>();
builder.Services.AddScoped<GetCurrentUser>();
builder.Services.AddScoped<RefreshToken>();
builder.Services.AddScoped<DelegateToken>();
builder.Services.AddScoped<SetRolePermissions>();
builder.Services.AddScoped<SetAvatar>();
builder.Services.AddScoped<ChangePassword>();

var app = builder.Build();
app.UseGreenHiveDefaults();

app.MapPost("/api/identity/login", (LoginRequest request, Login useCase, CancellationToken ct) => useCase.ExecuteAsync(request, ct).ToHttp())
    .AllowAnonymous();

var api = app.MapApi("/api/identity");
api.MapGet("/me", (GetCurrentUser useCase, CancellationToken ct) => useCase.ExecuteAsync(ct).ToHttp());
api.MapGet("/me/avatar", (IdentityRepository repository, RequestContext context, CancellationToken ct) =>
    repository.AvatarAsync(context.User.Id, ct).Bind(a => a.ToResult(Error.NotFound("identity.avatar.none", "identity:avatar.none"))).ToHttp());
api.MapPut("/me/avatar", (AvatarInfo request, SetAvatar useCase, CancellationToken ct) => useCase.ExecuteAsync(request, ct).ToHttp());
api.MapDelete("/me/avatar", (IdentityRepository repository, RequestContext context, CancellationToken ct) =>
    repository.DeleteAvatarAsync(context.User.Id, ct).ToHttp());
api.MapPut("/me/password", (ChangePasswordRequest request, ChangePassword useCase, CancellationToken ct) => useCase.ExecuteAsync(request, ct).ToHttp());
// Pictures of other users (e.g. inbox, user lists) - any signed-in user of the same tenant.
api.MapGet("/users/{id:guid}/avatar", (Guid id, IdentityRepository repository, CancellationToken ct) =>
    repository.AvatarAsync(id, ct).Bind(a => a.ToResult(Error.NotFound("identity.avatar.none", "identity:avatar.none"))).ToHttp());
api.MapPost("/refresh", (RefreshToken useCase, CancellationToken ct) => useCase.ExecuteAsync(ct).ToHttp());
api.MapGet("/permissions", (IdentityRepository repository, CancellationToken ct) => repository.PermissionsAsync(ct).ToHttp());
api.MapGet("/roles", (IdentityRepository repository, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, "users.view").BindAsync(_ => repository.RolesAsync(context.User.TenantId, ct)).ToHttp());
api.MapPut("/roles/{roleId:guid}/permissions", (Guid roleId, string[] permissions, SetRolePermissions useCase, CancellationToken ct) =>
    useCase.ExecuteAsync(roleId, permissions, ct).ToHttp());

var internalApi = app.MapInternal();
internalApi.MapPost("/tokens/delegate", (DelegateTokenRequest request, DelegateToken useCase, CancellationToken ct) => useCase.ExecuteAsync(request, ct).ToHttp());
internalApi.MapGet("/users/{id:guid}", (Guid id, IdentityRepository repository, CancellationToken ct) =>
    repository.FindByIdAsync(id, ct).Bind(a => a.ToResult(Steps.NotFound("identity.user", id))).Map(a => a.Info).ToHttp());
internalApi.MapPost("/permissions", (RegisterPermissionsRequest request, IdentityRepository repository, CancellationToken ct) =>
    repository.RegisterPermissionsAsync(request, ct).ToHttp());

app.Run();
