using GreenHive.NET.Modules.DocumentManagement.Contracts;
using GreenHive.NET.Contracts.Api;
using GreenHive.Wpf.Framework.ModuleLoader.Sdk;
using GreenHive.NET.Monads;

namespace GreenHive.NET.Modules.DocumentManagement.Client.ViewModels;

/// <summary>Invoices: filter by status, details, create, submit (starts the approval workflow), approve/reject, pay.</summary>
public sealed class InvoiceListViewModel : ModuleViewModel
{
    public const string CreateParameter = "new";

    private IReadOnlyList<InvoiceDto> _items = [];
    private InvoiceDto? _selected;
    private bool _createRequested;
    private Guid? _pendingSelection;

    public string Search { get; set; } = string.Empty;

    /// <summary>Status filter; empty = all.</summary>
    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<InvoiceDto> Items
    {
        get => _items;
        private set => SetProperty(ref _items, value);
    }

    public InvoiceDto? Selected
    {
        get => _selected;
        set
        {
            if (SetProperty(ref _selected, value))
            {
                OnPropertyChanged(nameof(CanSubmit));
                OnPropertyChanged(nameof(CanDecide));
                OnPropertyChanged(nameof(CanPay));
            }
        }
    }

    public bool CreateRequested
    {
        get => _createRequested;
        set => SetProperty(ref _createRequested, value);
    }

    public bool CanEdit => Has(DocumentPermissions.Edit);

    public bool CanSubmit => Selected?.Status is InvoiceStatus.Draft or InvoiceStatus.Rejected && CanEdit;

    public bool CanDecide => Selected?.Status == InvoiceStatus.Submitted && Has(DocumentPermissions.Approve);

    public bool CanPay => Selected?.Status == InvoiceStatus.Approved && CanEdit;

    protected override void OnCreated()
    {
        Title = T("view.invoices.label");
        Module.Subscribe("documents.invoice.*", e => AppContext.Dispatcher?.Run(() => _ = LoadAsync()));
    }

    public override void OnNavigatedTo(object? parameter)
    {
        if (parameter as string == CreateParameter)
        {
            CreateRequested = true;
        }
        else if (parameter is string id && Guid.TryParse(id, out var invoiceId))
        {
            _pendingSelection = invoiceId;
        }

        _ = LoadAsync();
    }

    public Task<bool> LoadAsync()
    {
        var query = $"invoices?status={Uri.EscapeDataString(Status)}&search={Uri.EscapeDataString(Search)}";
        return RunAsync(ct => Api.GetAsync<IReadOnlyList<InvoiceDto>>(query, ct), list =>
        {
            Items = list;
            var id = _pendingSelection ?? Selected?.Id;
            _pendingSelection = null;
            Selected = id is null ? null : list.FirstOrDefault(i => i.Id == id);
        });
    }

    public Task<Result<IReadOnlyList<LookupItem>>> CostCentersAsync() => Api.GetAsync<IReadOnlyList<LookupItem>>("lookups/cost-centers", Closed);

    public Task<bool> CreateAsync(CreateInvoiceRequest request) =>
        RunAsync(ct => Api.PostAsync<InvoiceDto>("invoices", request, ct), i =>
        {
            _pendingSelection = i.Id;
            _ = LoadAsync();
        }, T("invoices.created", ("number", request.Number)));

    public Task<bool> SubmitAsync() => Act("submit", null, "invoices.submitted");

    public Task<bool> ApproveAsync(string? comment) => Act("approve", new InvoiceDecisionRequest(comment), "invoices.approved");

    public Task<bool> RejectAsync(string? comment) => Act("reject", new InvoiceDecisionRequest(comment), "invoices.rejected");

    public Task<bool> PayAsync() => Act("pay", null, "invoices.paid");

    /// <summary>Client-side validation of the create form (the server validates again).</summary>
    public Validation<CreateInvoiceRequest> Validate(string number, string supplier, double amount, string currency, DateTimeOffset issued, DateTimeOffset due, string? costCenter)
    {
        var errors = new List<Error>();
        if (string.IsNullOrWhiteSpace(number))
        {
            errors.Add(Error.Validation("number", T("error.required", ("field", T("field.number")))));
        }

        if (string.IsNullOrWhiteSpace(supplier))
        {
            errors.Add(Error.Validation("supplier", T("error.required", ("field", T("field.supplier")))));
        }

        if (double.IsNaN(amount) || amount <= 0)
        {
            errors.Add(Error.Validation("amount", T("error.amount")));
        }

        if (string.IsNullOrWhiteSpace(costCenter))
        {
            errors.Add(Error.Validation("costCenter", T("error.required", ("field", T("field.costCenter")))));
        }

        return errors.Count > 0
            ? Validation<CreateInvoiceRequest>.Invalid(errors)
            : Validation<CreateInvoiceRequest>.Valid(new CreateInvoiceRequest(number.Trim(), supplier.Trim(), (decimal)amount, currency,
                DateOnly.FromDateTime(issued.Date), DateOnly.FromDateTime(due.Date), costCenter!));
    }

    private Task<bool> Act(string action, object? body, string info)
    {
        if (Selected is not { } invoice)
        {
            return Task.FromResult(false);
        }

        return RunAsync(ct => Api.PostAsync<InvoiceDto>($"invoices/{invoice.Id}/{action}", body, ct), updated =>
        {
            Items = Items.Select(i => i.Id == updated.Id ? updated : i).ToList();
            Selected = updated;
        }, T(info));
    }
}
