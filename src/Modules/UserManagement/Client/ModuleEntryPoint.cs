using GreenHive.NET.Modules.UserManagement.Client.ViewModels;
using GreenHive.NET.Modules.UserManagement.Client.Views;
using GreenHive.NET.Modules.UserManagement.Contracts;
using GreenHive.NET.CommandContracts;
using GreenHive.NET.ModuleContracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;

namespace GreenHive.NET.Modules.UserManagement.Client;

/// <summary>Entry point of the user-management module.</summary>
public sealed class UserManagementModule : ModuleEntryPoint
{
    public const string Users = "users.list";
    public const string Organization = "users.organization";

    protected override void OnInitialize()
    {
        Texts("en", UserTexts.English);
        Texts("de", UserTexts.German);
        Icon("home", "E716");
        Icon("organization", "E821");
        Icon("new", "E8FA");

        View<UserListViewModel>(Users, "users:view.users", "users.home", vm => new UserListView(vm), home: true, order: 0, permission: UserPermissions.View);
        View<OrganizationViewModel>(Organization, "users:view.organization", "users.organization", vm => new OrganizationView(vm),
            new ViewDescriptor { Role = ViewRole.ToolWindow, DefaultDock = DockSide.Right, MinWidth = 280 }, order: 1, permission: UserPermissions.View);

        MenuGroup("users:menu", order: 40);
        NavigationGroup("users:module", order: 40);
        Command("users.open", "users:cmd.open", "users.home", _ => Go(Users), permission: UserPermissions.View, placements: new CommandPlacement[] { InMenu(0, 0) });
        Command("users.organization.open", "users:cmd.organization", "users.organization", _ => Go(Organization), permission: UserPermissions.View, placements: new CommandPlacement[] { InMenu(0, 1) });
        Command("users.create", "users:cmd.create", "users.new", _ => Go(Users, UserListViewModel.CreateParameter), permission: UserPermissions.Edit,
            placements: new CommandPlacement[] { InMenu(1, 0) }, workflowCapable: true);
    }

    private Task Go(string key, string? parameter = null)
    {
        Module.NavigateTo(key, parameter);
        return Task.CompletedTask;
    }
}
