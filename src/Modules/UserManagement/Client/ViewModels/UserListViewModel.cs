using GreenHive.NET.Modules.UserManagement.Contracts;
using GreenHive.NET.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.Wpf.Framework.Security;

namespace GreenHive.NET.Modules.UserManagement.Client.ViewModels;

/// <summary>User directory: search, department filter, details, create, edit, roles, password reset.</summary>
public sealed class UserListViewModel : ModuleViewModel
{
    public const string CreateParameter = "new";

    private IReadOnlyList<UserDto> _items = [];
    private IReadOnlyList<DepartmentDto> _departments = [];
    private IReadOnlyList<RoleSummary> _roles = [];
    private UserDto? _selected;
    private bool _createRequested;
    private int _total;

    public string Search { get; set; } = string.Empty;

    public Guid? DepartmentId { get; set; }

    public bool IncludeInactive { get; set; }

    public IReadOnlyList<UserDto> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public int Total
    {
        get => _total;
        private set => SetProperty(ref _total, value);
    }

    public IReadOnlyList<DepartmentDto> Departments
    {
        get => _departments;
        private set => SetProperty(ref _departments, value);
    }

    public IReadOnlyList<RoleSummary> Roles
    {
        get => _roles;
        private set => SetProperty(ref _roles, value);
    }

    public UserDto? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    public bool CreateRequested
    {
        get => _createRequested;
        set => SetProperty(ref _createRequested, value);
    }

    public bool CanEdit => Has(UserPermissions.Edit);

    public bool CanEditRoles => Has(UserPermissions.Roles);

    protected override void OnCreated()
    {
        Title = T("view.users.label");
        Module.Subscribe("users.*", e => AppContext.Dispatcher?.Run(() => _ = LoadAsync()));
    }

    public override void OnNavigatedTo(object? parameter)
    {
        if (parameter is string id && Guid.TryParse(id, out var departmentId))
        {
            DepartmentId = departmentId;
            OnPropertyChanged(nameof(DepartmentId));
        }

        _ = InitializeAsync();
        if (parameter as string == CreateParameter)
        {
            CreateRequested = true;
        }
    }

    private async Task InitializeAsync()
    {
        if (Departments.Count == 0)
        {
            await RunAsync(ct => Api.GetAsync<IReadOnlyList<DepartmentDto>>("departments", ct), d => Departments = d);
            await RunAsync(ct => Api.GetAsync<IReadOnlyList<RoleSummary>>("roles", ct), r => Roles = r);
        }

        await LoadAsync();
    }

    public Task<bool> LoadAsync()
    {
        var query = $"?take=500&includeInactive={(IncludeInactive ? "true" : "false")}&search={Uri.EscapeDataString(Search)}" +
                    (DepartmentId is { } d ? $"&departmentId={d}" : string.Empty);
        return RunAsync(ct => Api.GetAsync<PagedResult<UserDto>>(query, ct), page =>
        {
            Items = page.Items;
            Total = page.Total;
            Selected = Selected is { } s ? page.Items.FirstOrDefault(u => u.Id == s.Id) : null;
        });
    }

    public Task<bool> CreateAsync(CreateUserRequest request) =>
        RunAsync(ct => Api.PostAsync<CreateUserResponse>(string.Empty, request, ct), r =>
        {
            InfoText = r.TemporaryPassword is { } p ? T("createdTemp", ("name", r.User.DisplayName), ("password", p)) : T("created", ("name", r.User.DisplayName));
            Selected = r.User;
            _ = LoadAsync();
        });

    public Task<bool> UpdateAsync(UserDto user, UpdateUserRequest request) =>
        RunAsync(ct => Api.PutAsync<UserDto>(user.Id.ToString(), request, ct), Replace, T("saved"));

    public Task<bool> SetRolesAsync(UserDto user, IReadOnlyList<string> roles) =>
        RunAsync(ct => Api.PutAsync<UserDto>($"{user.Id}/roles", new SetRolesRequest(roles), ct), Replace, T("rolesSaved"));

    public Task<bool> ResetPasswordAsync(UserDto user) =>
        RunAsync(ct => Api.PostAsync<ResetPasswordResponse>($"{user.Id}/reset-password", null, ct),
            r => InfoText = T("passwordReset", ("name", user.DisplayName), ("password", r.TemporaryPassword)));

    /// <summary>Profile picture of a user (null = none; the view shows the initials).</summary>
    public Task<byte[]?> AvatarAsync(Guid userId) => Context<ISecurityContext>().Profile.AvatarOfAsync(userId);

    private void Replace(UserDto user)
    {
        Items = Items.Select(u => u.Id == user.Id ? user : u).ToList();
        Selected = user;
    }
}
