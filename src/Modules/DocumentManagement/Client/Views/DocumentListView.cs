using System.Globalization;
using GreenHive.NET.Modules.DocumentManagement.Client.ViewModels;
using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using System.Windows;
using GreenHive.Wpf.Framework.Theming;
using System.Windows.Controls;

namespace GreenHive.NET.Modules.DocumentManagement.Client.Views;

/// <summary>Document list (search, category) with the selected document's details and versions.</summary>
public sealed class DocumentListView : UserControl
{
    private static readonly double[] Columns = [3, 1.2, 1, 1.4, 1.2];

    private readonly DocumentListViewModel _vm;
    private readonly DataList<DocumentDto> _list;
    private readonly SpacedPanel _detail = new() { Spacing = 12, Padding = new Thickness(16) };
    private readonly TextBlock _count = UiKit.Caption(string.Empty);

    public DocumentListView(DocumentListViewModel vm)
    {
        _vm = vm;
        var header = DataList<DocumentDto>.Cells(Columns,
            UiKit.Label(vm.T("field.title")), UiKit.Label(vm.T("field.category")), UiKit.Label(vm.T("field.status")),
            UiKit.Label(vm.T("field.owner")), UiKit.Label(vm.T("field.updated")));
        _list = new DataList<DocumentDto>(Row, d => d.Id, vm.T("documents.empty"), header);
        _list.SelectionChanged += async (_, d) =>
        {
            if (d?.Id != vm.Selected?.Id)
            {
                await vm.SelectAsync(d);
            }
        };

        var search = new SearchBox { PlaceholderText = vm.T("search"), Width = 260 };
        search.QuerySubmitted += async (_, e) =>
        {
            vm.Search = e.QueryText;
            await vm.LoadAsync();
        };
        var category = new ComboBox { Width = 160 };
        category.Items.Add(new ComboBoxItem { Content = vm.T("all"), Tag = string.Empty });
        foreach (var c in DocumentCategories.All)
        {
            category.Items.Add(new ComboBoxItem { Content = vm.T("category." + c), Tag = c });
        }

        category.SelectedIndex = 0;
        category.SelectionChanged += async (_, _) =>
        {
            vm.Category = (category.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
            await vm.LoadAsync();
        };

        var create = UiKit.Button(vm.T("new"), async (_, _) => await CreateAsync(), accent: true, glyph: "\uE710");
        create.IsEnabled = vm.CanEdit;
        var toolbar = UiKit.Row(search, category, UiKit.Button(vm.T("refresh"), async (_, _) => await vm.LoadAsync(), glyph: "\uE72C"), create, _count, UiKit.Busy(vm));
        toolbar.Margin = new Thickness(12, 8, 12, 0);
        _count.VerticalAlignment = VerticalAlignment.Center;

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star), MinWidth = 320 });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 260 });
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

        UiKit.Watch(vm, nameof(DocumentListViewModel.Items), () =>
        {
            _list.SetItems(vm.Items);
            _count.Text = $"{vm.Items.Count} / {vm.Total}";
        });
        UiKit.Watch(vm, nameof(DocumentListViewModel.Selected), ShowDetail);
        UiKit.Watch(vm, nameof(DocumentListViewModel.Versions), ShowDetail);
        UiKit.Watch(vm, nameof(DocumentListViewModel.CreateRequested), () =>
        {
            if (vm.CreateRequested)
            {
                vm.CreateRequested = false;
                Dispatcher.BeginInvoke(new Action(async () => await CreateAsync()));
            }
        });
    }

    private FrameworkElement Row(DocumentDto d) => DataList<DocumentDto>.Cells(Columns,
        UiKit.Text(d.Title),
        UiKit.Text(_vm.T("category." + d.Category)),
        UiKit.Text(_vm.T("status." + d.Status)),
        UiKit.Text(d.OwnerName),
        UiKit.Text(d.UpdatedAt.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)));

    private void ShowDetail()
    {
        _detail.Children.Clear();
        if (_vm.Selected is not { } d)
        {
            _detail.Children.Add(UiKit.Caption(_vm.T("documents.select")));
            return;
        }

        _detail.Children.Add(UiKit.Title(d.Title));
        _detail.Children.Add(UiKit.Caption($"{_vm.T("category." + d.Category)} · {_vm.T("status." + d.Status)} · {d.OwnerName} · v{d.CurrentVersion}"));
        _detail.Children.Add(UiKit.Caption($"{d.FileName} · {d.ContentType} · {d.SizeBytes / 1024.0:N0} KB"));
        if (d.Tags.Count > 0)
        {
            _detail.Children.Add(UiKit.Caption(string.Join("  ", d.Tags.Select(t => "#" + t))));
        }

        var archived = d.Status == "archived";
        var edit = UiKit.Button(_vm.T("documents.edit"), async (_, _) => await EditAsync(d), glyph: "\uE70F");
        var version = UiKit.Button(_vm.T("documents.addVersion"), async (_, _) => await AddVersionAsync(d), glyph: "\uE898");
        var archive = UiKit.Button(_vm.T("documents.archive"), async (_, _) =>
        {
            if (await UiKit.ConfirmAsync(this, _vm.T("documents.archive"), d.Title, _vm.T("documents.archive"), _vm.T("cancel")))
            {
                await _vm.ArchiveAsync(d);
            }
        }, glyph: "\uE7B8");
        edit.IsEnabled = version.IsEnabled = archive.IsEnabled = _vm.CanEdit && !archived;
        _detail.Children.Add(UiKit.Row(edit, version, archive));

        var versions = UiKit.Column(UiKit.Label(_vm.T("documents.versions")));
        foreach (var v in _vm.Versions.OrderByDescending(v => v.Version))
        {
            versions.Children.Add(UiKit.Caption(
                $"v{v.Version}  ·  {v.FileName}  ·  {v.SizeBytes / 1024.0:N0} KB  ·  {v.CreatedBy}  ·  {v.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}" +
                (string.IsNullOrEmpty(v.Comment) ? string.Empty : $"  ·  {v.Comment}")));
        }

        _detail.Children.Add(UiKit.Card(versions, 12));
    }

    private ComboBox CategoryBox(string selected)
    {
        var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var c in DocumentCategories.All)
        {
            box.Items.Add(new ComboBoxItem { Content = _vm.T("category." + c), Tag = c });
        }

        box.SelectedIndex = Math.Max(0, Array.IndexOf(DocumentCategories.All, selected));
        return box;
    }

    private static string SelectedCategory(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "other";

    private async Task CreateAsync()
    {
        var title = new TextBox();
        var category = CategoryBox("other");
        var file = new HintTextBox { PlaceholderText = "contract.pdf" };
        var size = new NumberBox { Value = 0, Minimum = 0 };
        var tags = new TextBox();
        var error = UiKit.Caption(string.Empty);
        var form = UiKit.Column(
            UiKit.Field(_vm.T("field.title"), title), UiKit.Field(_vm.T("field.category"), category), UiKit.Field(_vm.T("field.fileName"), file),
            UiKit.Field(_vm.T("field.size"), size), UiKit.Field(_vm.T("field.tags"), tags), error);
        form.MinWidth = 420;
        await UiKit.DialogAsync(this, _vm.T("cmd.create.label").TrimEnd('.'), form, _vm.T("save"), _vm.T("cancel"), async () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(file.Text))
            {
                error.Text = _vm.T("error.required", ("field", string.IsNullOrWhiteSpace(title.Text) ? _vm.T("field.title") : _vm.T("field.fileName")));
                return false;
            }

            return await _vm.CreateAsync(new CreateDocumentRequest(title.Text.Trim(), SelectedCategory(category), file.Text.Trim())
            {
                SizeBytes = double.IsNaN(size.Value) ? 0 : (long)size.Value,
                Tags = DocumentListViewModel.ParseTags(tags.Text),
            });
        });
    }

    private async Task EditAsync(DocumentDto d)
    {
        var title = new TextBox { Text = d.Title };
        var category = CategoryBox(d.Category);
        var tags = new TextBox { Text = string.Join(", ", d.Tags) };
        var form = UiKit.Column(UiKit.Field(_vm.T("field.title"), title), UiKit.Field(_vm.T("field.category"), category), UiKit.Field(_vm.T("field.tags"), tags));
        form.MinWidth = 420;
        await UiKit.DialogAsync(this, d.Title, form, _vm.T("save"), _vm.T("cancel"), () =>
            string.IsNullOrWhiteSpace(title.Text) ? Task.FromResult(false) : _vm.UpdateAsync(d, title.Text.Trim(), SelectedCategory(category), DocumentListViewModel.ParseTags(tags.Text)));
    }

    private async Task AddVersionAsync(DocumentDto d)
    {
        var file = new TextBox { Text = d.FileName };
        var size = new NumberBox { Value = d.SizeBytes, Minimum = 0 };
        var comment = new TextBox();
        var form = UiKit.Column(UiKit.Field(_vm.T("field.fileName"), file), UiKit.Field(_vm.T("field.size"), size), UiKit.Field(_vm.T("field.comment"), comment));
        form.MinWidth = 420;
        await UiKit.DialogAsync(this, _vm.T("documents.addVersion"), form, _vm.T("save"), _vm.T("cancel"), () =>
            string.IsNullOrWhiteSpace(file.Text) ? Task.FromResult(false)
                : _vm.AddVersionAsync(d, file.Text.Trim(), double.IsNaN(size.Value) ? 0 : (long)size.Value, string.IsNullOrWhiteSpace(comment.Text) ? null : comment.Text.Trim()));
    }
}
