using System.Globalization;
using GreenHive.NET.Modules.UserManagement.Client.ViewModels;
using GreenHive.NET.Modules.UserManagement.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using System.Windows;
using GreenHive.Wpf.Framework.Theming;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GreenHive.NET.Modules.UserManagement.Client.Views;

/// <summary>User directory (left) and the selected user (right).</summary>
public sealed class UserListView : UserControl
{
    private static readonly double[] Columns = [2, 2.4, 1.6, 1.6, 0.8];

    private readonly UserListViewModel _vm;
    private readonly DataList<UserDto> _list;
    private readonly SpacedPanel _detail = new() { Spacing = 12, Padding = new Thickness(16) };
    private readonly ComboBox _department = new() { Width = 200 };
    private readonly TextBlock _count = UiKit.Caption(string.Empty);
    private bool _fillingDepartments;

    public UserListView(UserListViewModel vm)
    {
        _vm = vm;
        var header = DataList<UserDto>.Cells(Columns, UiKit.Label(vm.T("field.displayName")), UiKit.Label(vm.T("field.email")),
            UiKit.Label(vm.T("field.department")), UiKit.Label(vm.T("field.jobTitle")), UiKit.Label(vm.T("field.status")));
        _list = new DataList<UserDto>(Row, u => u.Id, vm.T("empty"), header);
        _list.SelectionChanged += (_, u) =>
        {
            if (u?.Id != vm.Selected?.Id)
            {
                vm.Selected = u;
            }
        };

        var search = new SearchBox { PlaceholderText = vm.T("search"), Width = 240 };
        search.QuerySubmitted += async (_, e) =>
        {
            vm.Search = e.QueryText;
            await vm.LoadAsync();
        };
        _department.SelectionChanged += async (_, _) =>
        {
            if (_fillingDepartments)
            {
                return;
            }

            vm.DepartmentId = (_department.SelectedItem as ComboBoxItem)?.Tag as Guid?;
            await vm.LoadAsync();
        };
        var inactive = new CheckBox { Content = vm.T("inactive") };
        inactive.Click += async (_, _) =>
        {
            vm.IncludeInactive = inactive.IsChecked == true;
            await vm.LoadAsync();
        };
        var create = UiKit.Button(vm.T("new"), async (_, _) => await CreateAsync(), accent: true, glyph: "\uE8FA");
        create.IsEnabled = vm.CanEdit;
        _count.VerticalAlignment = VerticalAlignment.Center;
        var toolbar = UiKit.Row(search, _department, inactive, UiKit.Button(vm.T("refresh"), async (_, _) => await vm.LoadAsync(), glyph: "\uE72C"), create, _count, UiKit.Busy(vm));
        toolbar.Margin = new Thickness(12, 8, 12, 0);

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star), MinWidth = 360 });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 280 });
        var detail = new ScrollViewer { Content = _detail };
        Grid.SetColumn(detail, 1);
        layout.Children.Add(_list.View);
        layout.Children.Add(detail);

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var messages = UiKit.Messages(vm);
        messages.Margin = new Thickness(12, 4, 12, 0);
        Grid.SetRow(messages, 1);
        Grid.SetRow(layout, 2);
        root.Children.Add(toolbar);
        root.Children.Add(messages);
        root.Children.Add(layout);
        Content = root;

        UiKit.Watch(vm, nameof(UserListViewModel.Departments), FillDepartments);
        UiKit.Watch(vm, nameof(UserListViewModel.DepartmentId), FillDepartments);
        UiKit.Watch(vm, nameof(UserListViewModel.Items), () =>
        {
            _list.SetItems(vm.Items);
            _count.Text = vm.T("users", ("count", vm.Total));
        });
        UiKit.Watch(vm, nameof(UserListViewModel.Selected), () =>
        {
            if (vm.Selected is { } s && _list.SelectedItem?.Id != s.Id)
            {
                _list.Select(u => u.Id == s.Id);
            }

            ShowDetail();
        });
        UiKit.Watch(vm, nameof(UserListViewModel.CreateRequested), () =>
        {
            if (vm.CreateRequested)
            {
                vm.CreateRequested = false;
                Dispatcher.BeginInvoke(new Action(async () => await CreateAsync()));
            }
        });
    }

    private void FillDepartments()
    {
        _fillingDepartments = true;
        _department.Items.Clear();
        _department.Items.Add(new ComboBoxItem { Content = _vm.T("all"), Tag = null });
        foreach (var d in _vm.Departments)
        {
            _department.Items.Add(new ComboBoxItem { Content = d.Name, Tag = d.Id });
        }

        _department.SelectedIndex = _vm.DepartmentId is { } id ? Math.Max(0, _vm.Departments.ToList().FindIndex(d => d.Id == id) + 1) : 0;
        _fillingDepartments = false;
    }

    private FrameworkElement Row(UserDto u)
    {
        var status = UiKit.Text(_vm.T(u.IsActive ? "status.active" : "status.inactive"));
        if (!u.IsActive)
        {
            status.Opacity = 0.6;
        }

        return DataList<UserDto>.Cells(Columns, UiKit.Text(u.DisplayName), UiKit.Text(u.Email), UiKit.Text(u.DepartmentName ?? string.Empty), UiKit.Text(u.JobTitle), status);
    }

    private void ShowDetail()
    {
        _detail.Children.Clear();
        if (_vm.Selected is not { } u)
        {
            _detail.Children.Add(UiKit.Caption(_vm.T("select")));
            return;
        }

        var initials = string.Concat(u.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0]));
        var picture = new PersonPicture { Initials = initials, DisplayName = u.DisplayName, Width = 56, Height = 56 };
        _detail.Children.Add(UiKit.Row(picture, UiKit.Column(UiKit.Title(u.DisplayName), UiKit.Caption($"{u.UserName} · {u.JobTitle}"))));
        _ = LoadPictureAsync(picture, u.Id);

        var facts = new SpacedGrid { ColumnSpacing = 16, RowSpacing = 6 };
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        facts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        void Fact(string key, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var row = facts.RowDefinitions.Count;
            facts.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = UiKit.Caption(_vm.T(key));
            var v = UiKit.Text(value, wrap: true);
            Grid.SetRow(l, row);
            Grid.SetRow(v, row);
            Grid.SetColumn(v, 1);
            facts.Children.Add(l);
            facts.Children.Add(v);
        }

        Fact("field.email", u.Email);
        Fact("field.department", u.DepartmentName);
        Fact("field.manager", u.ManagerName);
        Fact("field.phone", u.Phone);
        Fact("field.location", u.Location);
        Fact("field.hiredOn", u.HiredOn?.ToString("d", CultureInfo.CurrentCulture));
        Fact("field.roles", string.Join(", ", u.Roles));
        Fact("field.approvalLimit", UiKit.Money(u.ApprovalLimit, "EUR"));
        Fact("field.lastLogin", u.LastLoginAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
        Fact("field.status", _vm.T(u.IsActive ? "status.active" : "status.inactive"));
        _detail.Children.Add(UiKit.Card(facts, 12));

        var edit = UiKit.Button(_vm.T("edit"), async (_, _) => await EditAsync(u), glyph: "\uE70F");
        var roles = UiKit.Button(_vm.T("roles"), async (_, _) => await RolesAsync(u), glyph: "\uE8D7");
        var reset = UiKit.Button(_vm.T("resetPassword"), async (_, _) =>
        {
            if (await UiKit.ConfirmAsync(this, _vm.T("resetPassword"), _vm.T("confirmReset", ("name", u.DisplayName)), _vm.T("resetPassword"), _vm.T("cancel")))
            {
                await _vm.ResetPasswordAsync(u);
            }
        }, glyph: "\uE72E");
        edit.IsEnabled = reset.IsEnabled = _vm.CanEdit;
        roles.IsEnabled = _vm.CanEditRoles;
        _detail.Children.Add(UiKit.Row(edit, roles, reset));
    }

    private async Task LoadPictureAsync(PersonPicture picture, Guid userId)
    {
        if (await _vm.AvatarAsync(userId) is not { Length: > 0 } bytes || _vm.Selected?.Id != userId)
        {
            return;
        }

        // Unreadable pictures keep the initials.
        picture.SetPicture(bytes);
    }

    private ComboBox DepartmentBox(Guid? selected)
    {
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        box.Items.Add(new ComboBoxItem { Content = "-", Tag = null });
        foreach (var d in _vm.Departments)
        {
            box.Items.Add(new ComboBoxItem { Content = $"{d.Name} ({d.CostCenter})", Tag = d.Id });
        }

        box.SelectedIndex = selected is { } id ? Math.Max(0, _vm.Departments.ToList().FindIndex(d => d.Id == id) + 1) : 0;
        return box;
    }

    private static Guid? Selected(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag as Guid?;

    private static NumberBox Limit(decimal value) => new()
    {
        Value = (double)value, Minimum = 0, SmallChange = 1000, LargeChange = 10000,
    };

    private async Task CreateAsync()
    {
        var userName = new TextBox();
        var displayName = new TextBox();
        var email = new TextBox { InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.EmailSmtpAddress) } } };
        var department = DepartmentBox(_vm.DepartmentId);
        var jobTitle = new TextBox();
        var limit = Limit(0);
        var password = new PasswordBox();
        var roles = RoleChecks(new string[] { "Viewer" });
        var errors = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Theme.Error };
        var form = UiKit.Column(
            UiKit.Field(_vm.T("field.userName"), userName), UiKit.Field(_vm.T("field.displayName"), displayName), UiKit.Field(_vm.T("field.email"), email),
            UiKit.Field(_vm.T("field.department"), department), UiKit.Field(_vm.T("field.jobTitle"), jobTitle), UiKit.Field(_vm.T("field.approvalLimit"), limit),
            UiKit.Field(_vm.T("field.roles"), roles), UiKit.Field(_vm.T("field.password"), password), errors);
        form.MinWidth = 440;
        await UiKit.DialogAsync(this, _vm.T("cmd.create.label").TrimEnd('.'), form, _vm.T("save"), _vm.T("cancel"), async () =>
        {
            var missing = new[] { (userName.Text, "field.userName"), (displayName.Text, "field.displayName"), (email.Text, "field.email") }
                .Where(x => string.IsNullOrWhiteSpace(x.Item1)).Select(x => _vm.T("error.required", ("field", _vm.T(x.Item2)))).ToList();
            if (missing.Count > 0)
            {
                errors.Text = string.Join("\n", missing);
                return false;
            }

            return await _vm.CreateAsync(new CreateUserRequest(userName.Text.Trim(), displayName.Text.Trim(), email.Text.Trim())
            {
                DepartmentId = Selected(department),
                JobTitle = jobTitle.Text.Trim(),
                ApprovalLimit = double.IsNaN(limit.Value) ? 0 : (decimal)limit.Value,
                Roles = CheckedRoles(roles),
                Password = string.IsNullOrEmpty(password.Password) ? null : password.Password,
            });
        });
    }

    private async Task EditAsync(UserDto u)
    {
        var displayName = new TextBox { Text = u.DisplayName };
        var email = new TextBox { Text = u.Email };
        var department = DepartmentBox(u.DepartmentId);
        var jobTitle = new TextBox { Text = u.JobTitle };
        var phone = new TextBox { Text = u.Phone };
        var location = new TextBox { Text = u.Location };
        var limit = Limit(u.ApprovalLimit);
        var active = new ToggleSwitch { IsOn = u.IsActive };
        var form = UiKit.Column(
            UiKit.Field(_vm.T("field.displayName"), displayName), UiKit.Field(_vm.T("field.email"), email), UiKit.Field(_vm.T("field.department"), department),
            UiKit.Field(_vm.T("field.jobTitle"), jobTitle), UiKit.Field(_vm.T("field.phone"), phone), UiKit.Field(_vm.T("field.location"), location),
            UiKit.Field(_vm.T("field.approvalLimit"), limit), UiKit.Field(_vm.T("field.active"), active));
        form.MinWidth = 440;
        await UiKit.DialogAsync(this, u.DisplayName, form, _vm.T("save"), _vm.T("cancel"), () =>
            string.IsNullOrWhiteSpace(displayName.Text) || string.IsNullOrWhiteSpace(email.Text)
                ? Task.FromResult(false)
                : _vm.UpdateAsync(u, new UpdateUserRequest(displayName.Text.Trim(), email.Text.Trim(), active.IsOn, u.RowVersion)
                {
                    DepartmentId = Selected(department),
                    JobTitle = jobTitle.Text.Trim(),
                    Phone = phone.Text.Trim(),
                    Location = location.Text.Trim(),
                    ManagerId = u.ManagerId,
                    ApprovalLimit = double.IsNaN(limit.Value) ? 0 : (decimal)limit.Value,
                }));
    }

    private SpacedPanel RoleChecks(IReadOnlyList<string> selected)
    {
        var panel = new SpacedPanel { Spacing = 2 };
        foreach (var role in _vm.Roles)
        {
            panel.Children.Add(new CheckBox
            {
                Content = $"{role.Name} - {role.Description}",
                Tag = role.Name,
                IsChecked = selected.Contains(role.Name, StringComparer.OrdinalIgnoreCase),
            });
        }

        return panel;
    }

    private static IReadOnlyList<string> CheckedRoles(SpacedPanel panel) =>
        panel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();

    private async Task RolesAsync(UserDto u)
    {
        var roles = RoleChecks(u.Roles);
        await UiKit.DialogAsync(this, $"{_vm.T("field.roles")} - {u.DisplayName}", roles, _vm.T("save"), _vm.T("cancel"), () => _vm.SetRolesAsync(u, CheckedRoles(roles)));
    }
}
