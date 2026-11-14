using System.Text.Json.Nodes;
using GreenHive.NET.Modules.UserManagement.Backend;
using GreenHive.NET.Modules.UserManagement.Contracts;
using GreenHive.NET.Backend.Core.Hosting;
using GreenHive.NET.Backend.Core.Http;
using GreenHive.NET.Backend.Core.Pipeline;
using GreenHive.NET.Contracts.Api;
using GreenHive.NET.Monads;

var builder = WebApplication.CreateBuilder(args);
builder.AddGreenHiveDefaults("user-module");
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddScoped<CreateUser>();
builder.Services.AddScoped<UpdateUser>();
builder.Services.AddScoped<SetUserRoles>();
builder.Services.AddScoped<ResetPassword>();

var app = builder.Build();
app.UseGreenHiveDefaults();

var api = app.MapApi("/api/users");
api.MapGet("/", (string? search, Guid? departmentId, bool? includeInactive, int? skip, int? take, UserRepository repository, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, UserPermissions.View)
        .BindAsync(_ => repository.SearchAsync(context.User.TenantId, search, departmentId, includeInactive == true, Math.Max(0, skip ?? 0), Math.Clamp(take ?? 100, 1, 500), ct))
        .ToHttp());
api.MapGet("/departments", (UserRepository repository, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, UserPermissions.View).BindAsync(_ => repository.DepartmentsAsync(context.User.TenantId, ct)).ToHttp());
api.MapGet("/roles", (UserRepository repository, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, UserPermissions.View).BindAsync(_ => repository.RolesAsync(context.User.TenantId, ct)).ToHttp());
api.MapGet("/{id:guid}", (Guid id, UserRepository repository, RequestContext context, CancellationToken ct) =>
    Steps.Authorize(context.User, UserPermissions.View).BindAsync(_ => repository.FindAsync(id, ct)).ToHttp());
api.MapPost("/", (CreateUserRequest request, CreateUser useCase, CancellationToken ct) =>
    useCase.ExecuteAsync(request, ct).ToCreated(r => $"/api/users/{r.User.Id}"));
api.MapPut("/{id:guid}", (Guid id, UpdateUserRequest request, UpdateUser useCase, CancellationToken ct) => useCase.ExecuteAsync(id, request, ct).ToHttp());
api.MapPut("/{id:guid}/roles", (Guid id, SetRolesRequest request, SetUserRoles useCase, CancellationToken ct) => useCase.ExecuteAsync(id, request, ct).ToHttp());
api.MapPost("/{id:guid}/reset-password", (Guid id, ResetPassword useCase, CancellationToken ct) => useCase.ExecuteAsync(id, ct).ToHttp());

// ---- lookups (EntityRef fields)
api.MapGet("/lookups/departments", (UserRepository repository, RequestContext context, CancellationToken ct) =>
    repository.DepartmentsAsync(context.User.TenantId, ct).Map(list => list.Select(d => new LookupItem(d.Id.ToString(), $"{d.Name} ({d.CostCenter})"))).ToHttp());
api.MapGet("/lookups/users", (string? permission, UserRepository repository, RequestContext context, CancellationToken ct) =>
    repository.WithPermissionAsync(context.User.TenantId, permission, ct).Map(list => list.Select(u => new LookupItem(u.Id.ToString(), u.Name))).ToHttp());

// ---- headless workflow step users.createUser (onboarding)
api.MapPost("/workflow-steps/{stepType}", (string stepType, JsonObject inputs, CreateUser useCase, CancellationToken ct) =>
    (stepType == "users.createUser"
        ? useCase.ExecuteAsync(new CreateUserRequest(inputs["userName"]?.ToString() ?? string.Empty, inputs["displayName"]?.ToString() ?? string.Empty,
                inputs["email"]?.ToString() ?? string.Empty)
            {
                DepartmentId = Guid.TryParse(inputs["departmentId"]?.ToString(), out var department) ? department : null,
                JobTitle = inputs["jobTitle"]?.ToString(),
                Roles = [inputs["role"]?.ToString() is { Length: > 0 } role ? role : "Viewer"],
            }, ct)
            .Map(r => new JsonObject { ["userId"] = r.User.Id.ToString(), ["userName"] = r.User.UserName, ["temporaryPassword"] = r.TemporaryPassword })
        : Task.FromResult(Result<JsonObject>.Failure(Error.NotFound("users.stepType.unknown", "workflow:stepType.unknown"))))
    .ToHttp());

app.Run();
