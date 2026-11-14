using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.NET.Contracts;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;

namespace GreenHive.NET.Modules.DocumentManagement.Client.ViewModels;

/// <summary>Document list with search and category filter; details, versions, create, edit, new version, archive.</summary>
public sealed class DocumentListViewModel : ModuleViewModel
{
    public const string CreateParameter = "new";

    private IReadOnlyList<DocumentDto> _items = [];
    private DocumentDto? _selected;
    private IReadOnlyList<DocumentVersionDto> _versions = [];
    private int _total;
    private bool _createRequested;

    public string Search { get; set; } = string.Empty;

    /// <summary>Category filter; empty = all.</summary>
    public string Category { get; set; } = string.Empty;

    public IReadOnlyList<DocumentDto> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public int Total
    {
        get => _total;
        private set => SetProperty(ref _total, value);
    }

    public DocumentDto? Selected
    {
        get => _selected;
        private set => SetProperty(ref _selected, value);
    }

    public IReadOnlyList<DocumentVersionDto> Versions
    {
        get => _versions;
        private set => SetProperty(ref _versions, value);
    }

    /// <summary>Set when the view was opened by the "new document" command; the view shows the dialog and resets it.</summary>
    public bool CreateRequested
    {
        get => _createRequested;
        set => SetProperty(ref _createRequested, value);
    }

    public bool CanEdit => Has(DocumentPermissions.Edit);

    protected override void OnCreated()
    {
        Title = T("view.documents.label");
        Module.Subscribe("documents.*", e =>
        {
            if (e.Type is "documents.created" or "documents.updated")
            {
                AppContext.Dispatcher?.Run(() => _ = LoadAsync());
            }
        });
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _ = LoadAsync();
        if (parameter as string == CreateParameter)
        {
            CreateRequested = true;
        }
    }

    public Task<bool> LoadAsync()
    {
        var query = $"?take=200&search={Uri.EscapeDataString(Search)}&category={Uri.EscapeDataString(Category)}";
        return RunAsync(ct => Api.GetAsync<PagedResult<DocumentDto>>(query, ct), page =>
        {
            Items = page.Items;
            Total = page.Total;
            if (Selected is { } current && page.Items.FirstOrDefault(d => d.Id == current.Id) is { } fresh)
            {
                Selected = fresh;
            }
        });
    }

    public async Task SelectAsync(DocumentDto? document)
    {
        Selected = document;
        Versions = [];
        if (document is not null)
        {
            await RunAsync(ct => Api.GetAsync<IReadOnlyList<DocumentVersionDto>>($"{document.Id}/versions", ct), v => Versions = v);
        }
    }

    public Task<bool> CreateAsync(CreateDocumentRequest request) =>
        RunAsync(ct => Api.PostAsync<DocumentDto>(string.Empty, request, ct), async d =>
        {
            await LoadAsync();
            await SelectAsync(Items.FirstOrDefault(i => i.Id == d.Id) ?? d);
        }, T("documents.created", ("title", request.Title)));

    public Task<bool> UpdateAsync(DocumentDto document, string title, string category, IReadOnlyList<string> tags) =>
        RunAsync(ct => Api.PutAsync<DocumentDto>(document.Id.ToString(), new UpdateDocumentRequest(title, category, document.RowVersion) { Tags = tags }, ct),
            d => _ = AfterChangeAsync(d), T("documents.saved"));

    public Task<bool> AddVersionAsync(DocumentDto document, string fileName, long size, string? comment) =>
        RunAsync(ct => Api.PostAsync<DocumentDto>($"{document.Id}/versions", new AddVersionRequest(fileName, size, comment), ct),
            d => _ = AfterChangeAsync(d), T("documents.versionAdded", ("version", document.CurrentVersion + 1)));

    public Task<bool> ArchiveAsync(DocumentDto document) =>
        RunAsync(ct => Api.PostAsync<DocumentDto>($"{document.Id}/archive", null, ct), d => _ = AfterChangeAsync(d), T("documents.archived"));

    public static IReadOnlyList<string> ParseTags(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private async Task AfterChangeAsync(DocumentDto document)
    {
        await LoadAsync();
        await SelectAsync(Items.FirstOrDefault(i => i.Id == document.Id) ?? document);
    }
}
