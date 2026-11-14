using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using GreenHive.NET.Backend.Core.Data;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts.Api;

namespace GreenHive.NET.Backend.AiAgentService;

/// <summary>Traceability of every AI action: provider, prompt hash, proposal, policy decision, confidence, tokens.</summary>
public sealed class AiAuditLog(Db db, TimeProvider time)
{
    public Task<Result<int>> WriteAsync(Guid tenantId, Guid instanceId, Guid workflowId, string stepId, IAiProvider provider, string modelProfile,
        string promptText, JsonObject? proposal, string? reasoning, double? confidence, string decision, string? policyCode, int tokens, CancellationToken ct) =>
        db.ExecuteAsync(
            """
            insert into ai.audit (id, tenant_id, instance_id, workflow_id, step_id, at, provider, model, model_profile, prompt_hash, proposal, reasoning,
                                  confidence, decision, policy_code, tokens)
            values (@id, @tenantId, @instanceId, @workflowId, @stepId, @at, @provider, @model, @modelProfile, @promptHash, @proposal::jsonb, @reasoning,
                    @confidence, @decision, @policyCode, @tokens)
            """,
            new
            {
                id = Guid.NewGuid(),
                tenantId,
                instanceId,
                workflowId,
                stepId,
                at = time.GetUtcNow(),
                provider = provider.Name,
                model = provider.Model,
                modelProfile,
                promptHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(promptText)))[..16],
                proposal = proposal?.ToJsonString(),
                reasoning,
                confidence,
                decision,
                policyCode,
                tokens,
            }, ct);

    public Task<Result<IReadOnlyList<AiAuditEntry>>> ForInstanceAsync(Guid instanceId, CancellationToken ct) =>
        db.QueryAsync("select * from ai.audit where instance_id = @instanceId order by at", Map, new { instanceId }, ct);

    public Task<Result<IReadOnlyList<AiAuditEntry>>> RecentAsync(Guid tenantId, CancellationToken ct) =>
        db.QueryAsync("select * from ai.audit where tenant_id = @tenantId order by at desc limit 100", Map, new { tenantId }, ct);

    /// <summary>Usage counters for the limits: runs of a workflow today, entries and tokens of an instance, entries of a step.</summary>
    public Task<Result<(int RunsToday, int InstanceEntries, int InstanceTokens, int StepEntries)>> UsageAsync(Guid workflowId, Guid instanceId, string stepId, CancellationToken ct) =>
        db.QuerySingleAsync(
                """
                select
                  (select count(distinct instance_id)::int from ai.audit where workflow_id = @workflowId and at >= date_trunc('day', now())) as runs_today,
                  (select count(*)::int from ai.audit where instance_id = @instanceId and decision <> 'tool') as instance_entries,
                  (select coalesce(sum(tokens), 0)::int from ai.audit where instance_id = @instanceId) as instance_tokens,
                  (select count(*)::int from ai.audit where instance_id = @instanceId and step_id = @stepId and decision not in ('tool', 'approved')) as step_entries
                """,
                r => (r.Get<int>("runs_today"), r.Get<int>("instance_entries"), r.Get<int>("instance_tokens"), r.Get<int>("step_entries")),
                new { workflowId, instanceId, stepId }, ct)
            .Map(o => o.GetValueOrDefault((0, 0, 0, 0)));

    public Task<Result<(int Runs, int Tokens)>> TodayAsync(Guid tenantId, CancellationToken ct) =>
        db.QuerySingleAsync(
                "select count(distinct instance_id)::int as runs, coalesce(sum(tokens), 0)::int as tokens from ai.audit where tenant_id = @tenantId and at >= date_trunc('day', now())",
                r => (r.Get<int>("runs"), r.Get<int>("tokens")), new { tenantId }, ct)
            .Map(o => o.GetValueOrDefault((0, 0)));

    private static AiAuditEntry Map(System.Data.Common.DbDataReader r) =>
        new(r.Get<Guid>("id"), r.Get<Guid>("instance_id"), r.Get<string>("step_id"), r.GetTime("at"), r.Get<string>("decision"))
        {
            Provider = r.Get<string>("provider"),
            Model = r.Get<string>("model"),
            Reasoning = r.GetOrNull<string>("reasoning"),
            Confidence = r.GetNullable<double>("confidence"),
            PolicyCode = r.GetOrNull<string>("policy_code"),
            Proposal = r.GetOrNull<string>("proposal") is { } p ? JsonNode.Parse(p) as JsonObject : null,
            Tokens = r.Get<int>("tokens"),
        };
}
