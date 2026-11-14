using GreenHive.NET.Modules.UserManagement.Client.ViewModels;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using System.Windows;
using GreenHive.Wpf.Framework.Theming;
using System.Windows.Controls;

namespace GreenHive.NET.Modules.UserManagement.Client.Views;

/// <summary>Tool window: departments (click filters the user list) and roles with their member counts.</summary>
public sealed class OrganizationView : UserControl
{
    public OrganizationView(OrganizationViewModel vm)
    {
        var departments = new SpacedPanel { Spacing = 4 };
        var roles = new SpacedPanel { Spacing = 4 };
        var root = new SpacedPanel { Spacing = 12, Padding = new Thickness(12) };
        root.Children.Add(UiKit.Row(UiKit.Button(vm.T("refresh"), async (_, _) => await vm.LoadAsync(), glyph: "\uE72C"), UiKit.Busy(vm)));
        root.Children.Add(UiKit.Messages(vm));
        root.Children.Add(UiKit.Label(vm.T("departments")));
        root.Children.Add(departments);
        root.Children.Add(UiKit.Label(vm.T("roleList")));
        root.Children.Add(roles);
        Content = new ScrollViewer { Content = root };

        UiKit.Watch(vm, nameof(OrganizationViewModel.Departments), () =>
        {
            departments.Children.Clear();
            foreach (var d in vm.Departments.OrderBy(d => d.Name))
            {
                var link = new HyperlinkButton { Content = $"{d.Name}  ·  {d.CostCenter}  ·  {vm.T("users", ("count", d.UserCount))}" };
                link.Click += (_, _) => vm.ShowDepartment(d);
                departments.Children.Add(link);
            }
        });
        UiKit.Watch(vm, nameof(OrganizationViewModel.Roles), () =>
        {
            roles.Children.Clear();
            foreach (var r in vm.Roles)
            {
                roles.Children.Add(UiKit.Card(UiKit.Column(UiKit.Label($"{r.Name} ({r.UserCount})"), UiKit.Caption(r.Description)), 10));
            }
        });
    }
}
