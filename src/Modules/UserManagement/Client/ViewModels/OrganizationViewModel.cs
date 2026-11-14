using GreenHive.NET.Modules.UserManagement.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;

namespace GreenHive.NET.Modules.UserManagement.Client.ViewModels;

/// <summary>Departments (with cost centres and head count) and roles.</summary>
public sealed class OrganizationViewModel : ModuleViewModel
{
    private IReadOnlyList<DepartmentDto> _departments = [];
    private IReadOnlyList<RoleSummary> _roles = [];

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

    protected override void OnCreated() => Title = T("view.organization.label");

    public override void OnNavigatedTo(object? parameter) => _ = LoadAsync();

    public async Task LoadAsync()
    {
        await RunAsync(ct => Api.GetAsync<IReadOnlyList<DepartmentDto>>("departments", ct), d => Departments = d);
        await RunAsync(ct => Api.GetAsync<IReadOnlyList<RoleSummary>>("roles", ct), r => Roles = r);
    }

    public void ShowDepartment(DepartmentDto department) => Module.NavigateTo(UserManagementModule.Users, department.Id.ToString());
}
