using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Monads;
using GreenHive.NET.WorkflowContracts;

namespace GreenHive.NET.Backend.AiAgentService;

/// <summary>A tool the model may call (a headless module step type).</summary>
public sealed record AiTool(string Id, string Description, InputSchema? Input);

public sealed record AiToolCall(string ToolId, JsonObject Inputs);

/// <summary>What the model gets: goal, step, allowed tools and the data inside the policy's data scope.</summary>
public sealed record AiPrompt(string Goal, WorkflowStep Step, JsonObject Context, IReadOnlyList<AiTool> Tools)
{
    /// <summary>Results of tool calls made so far in this step.</summary>
    public JsonObject ToolResults { get; init; } = [];
}

/// <summary>Structured answer of the model: outputs for the step (or a tool call), reasoning summary and confidence.</summary>
public sealed record AiCompletion(JsonObject Outputs, string Reasoning, double Confidence, int Tokens)
{
    public string? Decision { get; init; }

    public AiToolCall? ToolCall { get; init; }
}

/// <summary>Abstraction over the AI model provider (structured output and tool calling only - no free-text parsing).</summary>
public interface IAiProvider
{
    string Name { get; }

    string Model { get; }

    Task<Result<AiCompletion>> CompleteAsync(AiPrompt prompt, CancellationToken ct);
}

/// <summary>
/// Deterministic offline provider: fills fields from the workflow data by name, uses defaults, calls the step's first
/// tool when data is missing and lowers its confidence for unusual cases (so the fallback hands them to a person).
/// </summary>
public sealed class RulesAiProvider(IConfiguration configuration) : IAiProvider
{
    public string Name => "rules";

    public string Model => "greenhive-rules-1";

    private decimal ReviewAbove => decimal.TryParse(configuration["GreenHive:Ai:Rules:ReviewAbove"], CultureInfo.InvariantCulture, out var v) ? v : 20000m;

    public Task<Result<AiCompletion>> CompleteAsync(AiPrompt prompt, CancellationToken ct)
    {
        var data = Flatten(prompt.Context, prompt.ToolResults);
        var step = prompt.Step;

        // Required data missing and a tool is allowed: look it up first.
        var missingRequired = step.Input?.Fields.Any(f => f.Required && !f.Sensitive && f.Type != InputFieldType.Choice && Find(data, f.Name) is null) ?? false;
        if (prompt.Tools.Count > 0 && prompt.ToolResults.Count == 0 && missingRequired)
        {
            var tool = prompt.Tools[0];
            var inputs = new JsonObject();
            foreach (var field in tool.Input?.Fields ?? [])
            {
                if (Find(data, field.Name) is { } value)
                {
                    inputs[field.Name] = value.DeepClone();
                }
            }

            return Task.FromResult(Result.Success(new AiCompletion([], $"Looking up data with {tool.Id}.", 1, 120) { ToolCall = new AiToolCall(tool.Id, inputs) }));
        }

        var outputs = new JsonObject();
        var missing = new List<string>();
        var notes = new List<string>();
        double confidence = 0.9;
        string? decision = null;

        switch (step.Kind)
        {
            case WorkflowStepKind.Input or WorkflowStepKind.AiTask when step.Input is { } schema:
                foreach (var field in schema.Fields)
                {
                    if (field.Sensitive)
                    {
                        continue;
                    }

                    var value = Find(data, field.Name);
                    if (field.Type == InputFieldType.Choice && value is null)
                    {
                        var chosen = Recommend(field, data, notes, ref confidence);
                        if (chosen is not null)
                        {
                            outputs[field.Name] = chosen;
                        }

                        continue;
                    }

                    if (value is not null)
                    {
                        outputs[field.Name] = value.DeepClone();
                    }
                    else if (field.Default is not null)
                    {
                        outputs[field.Name] = field.Default;
                    }
                    else if (field.Required)
                    {
                        missing.Add(field.Name);
                    }
                }

                if (schema.Find("note") is not null || schema.Find("comment") is not null)
                {
                    outputs[schema.Find("comment") is not null ? "comment" : "note"] = $"Pre-checked by the AI agent: {string.Join("; ", notes.DefaultIfEmpty("no findings"))}.";
                }

                break;

            case WorkflowStepKind.AiTask:
                foreach (var (output, _) in step.OutputMapping)
                {
                    outputs[output] = Find(data, output)?.DeepClone() ?? Summary(step, data);
                }

                if (outputs.Count == 0)
                {
                    outputs["summary"] = Summary(step, data);
                }

                confidence = 0.8;
                break;

            case WorkflowStepKind.Choice:
                var choice = step.Options.FirstOrDefault(o => data.Values.Any(v => v is JsonValue jv && string.Equals(jv.ToString(), o.Value, StringComparison.OrdinalIgnoreCase)))
                    ?? step.Options.FirstOrDefault();
                decision = choice?.Value;
                confidence = choice is null ? 0.2 : 0.75;
                break;

            case WorkflowStepKind.Review:
                decision = "confirm";
                confidence = 0.85;
                break;
        }

        if (missing.Count > 0)
        {
            confidence = Math.Min(confidence, 0.3);
            notes.Add($"missing values: {string.Join(", ", missing)}");
        }

        var reasoning = notes.Count == 0 ? "All values were taken from the workflow data." : string.Join("; ", notes);
        return Task.FromResult(Result.Success(new AiCompletion(outputs, reasoning, confidence, 200 + (outputs.ToJsonString().Length / 4)) { Decision = decision }));
    }

    private string? Recommend(InputField field, Dictionary<string, JsonNode?> data, List<string> notes, ref double confidence)
    {
        var options = field.Options.Select(o => o.Value).ToList();
        if (options.Contains("approve", StringComparer.OrdinalIgnoreCase) && Find(data, "amount") is JsonValue amountNode &&
            decimal.TryParse(amountNode.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            if (amount > ReviewAbove)
            {
                notes.Add($"amount {amount:N2} is above {ReviewAbove:N0} - a person should decide");
                confidence = 0.55;
                return options.FirstOrDefault(o => o.Equals("clarify", StringComparison.OrdinalIgnoreCase)) ?? options[0];
            }

            notes.Add($"amount {amount:N2} is within the usual range");
            return options.First(o => o.Equals("approve", StringComparison.OrdinalIgnoreCase));
        }

        confidence = Math.Min(confidence, 0.6);
        return options.FirstOrDefault();
    }

    private static string Summary(WorkflowStep step, Dictionary<string, JsonNode?> data) =>
        $"{step.Instruction ?? step.Title}: " + string.Join(", ", data.Where(kv => kv.Value is JsonValue).Take(8).Select(kv => $"{kv.Key}={kv.Value}"));

    private static JsonNode? Find(Dictionary<string, JsonNode?> data, string name) => data.TryGetValue(name, out var value) ? value : null;

    /// <summary>Variables, payload and step outputs flattened by field name (later sources win).</summary>
    private static Dictionary<string, JsonNode?> Flatten(JsonObject context, JsonObject toolResults)
    {
        var data = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
        void Add(JsonObject? source)
        {
            foreach (var (key, value) in source ?? [])
            {
                if (value is JsonValue)
                {
                    data[key] = value;
                }
            }
        }

        Add(context["payload"] as JsonObject);
        foreach (var (_, stepOutputs) in context["steps"] as JsonObject ?? [])
        {
            Add(stepOutputs as JsonObject);
        }

        Add(context["variables"] as JsonObject);
        foreach (var (_, result) in toolResults)
        {
            Add(result as JsonObject);
        }

        return data;
    }
}

/// <summary>
/// Any OpenAI-compatible chat completions endpoint (GreenHive__Ai__Endpoint, __ApiKey, __Model): structured output via
/// a JSON schema built from the step's InputSchema, and function calling for the allowed tools.
/// </summary>
public sealed class OpenAiCompatibleProvider(IHttpClientFactory http, IConfiguration configuration) : IAiProvider
{
    public string Name => "openai";

    public string Model => configuration["GreenHive:Ai:Model"] ?? "gpt-4o-mini";

    public async Task<Result<AiCompletion>> CompleteAsync(AiPrompt prompt, CancellationToken ct)
    {
        var endpoint = (configuration["GreenHive:Ai:Endpoint"] ?? "https://api.openai.com/v1").TrimEnd('/');
        var apiKey = configuration["GreenHive:Ai:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return Error.Unavailable("ai.provider.notConfigured", "ai:provider.notConfigured");
        }

        var body = new JsonObject
        {
            ["model"] = Model,
            ["temperature"] = 0.1,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = SystemPrompt },
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonObject
                    {
                        ["goal"] = prompt.Goal,
                        ["step"] = new JsonObject { ["id"] = prompt.Step.Id, ["kind"] = prompt.Step.Kind.ToString(), ["title"] = prompt.Step.Title, ["instruction"] = prompt.Step.Instruction },
                        ["options"] = new JsonArray(prompt.Step.Options.Select(o => (JsonNode?)JsonValue.Create(o.Value)).ToArray()),
                        ["data"] = prompt.Context.DeepClone(),
                        ["toolResults"] = prompt.ToolResults.DeepClone(),
                    }.ToJsonString(),
                },
            },
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject { ["name"] = "step_result", ["strict"] = false, ["schema"] = OutputSchema(prompt.Step) },
            },
        };
        if (prompt.Tools.Count > 0 && prompt.ToolResults.Count == 0)
        {
            body["tools"] = new JsonArray(prompt.Tools.Select(t => (JsonNode?)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = t.Id.Replace('.', '_'),
                    ["description"] = t.Description,
                    ["parameters"] = SchemaOf(t.Input),
                },
            }).ToArray());
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return await Try.RunAsync(async () =>
        {
            using var response = await http.CreateClient("greenhive").SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                return Result.Failure<AiCompletion>(Error.Unavailable("ai.provider.http", $"AI provider returned {(int)response.StatusCode}."));
            }

            var json = JsonNode.Parse(text)!;
            var message = json["choices"]?[0]?["message"];
            var tokens = json["usage"]?["total_tokens"]?.GetValue<int>() ?? 0;
            if (message?["tool_calls"]?[0]?["function"] is { } function)
            {
                var toolId = prompt.Tools.FirstOrDefault(t => t.Id.Replace('.', '_') == function["name"]?.GetValue<string>())?.Id;
                var arguments = JsonNode.Parse(function["arguments"]?.GetValue<string>() ?? "{}") as JsonObject ?? [];
                return toolId is null
                    ? Result.Failure<AiCompletion>(Error.Validation("ai.tool.unknown", "ai:tool.unknown"))
                    : Result.Success(new AiCompletion([], "Calling a tool.", 1, tokens) { ToolCall = new AiToolCall(toolId, arguments) });
            }

            var content = JsonNode.Parse(message?["content"]?.GetValue<string>() ?? "{}") as JsonObject ?? [];
            return Result.Success(new AiCompletion(content["outputs"] as JsonObject ?? [], content["reasoning"]?.GetValue<string>() ?? string.Empty,
                content["confidence"]?.GetValue<double>() ?? 0, tokens)
            {
                Decision = content["decision"]?.GetValue<string>(),
            });
        }, "ai.provider");
    }

    private const string SystemPrompt =
        "You execute one step of a business workflow. Use only the provided data and tools. Answer with JSON: outputs (the step's fields), " +
        "decision (for choice/review steps), reasoning (one or two sentences, no personal data) and confidence (0..1). " +
        "If information is missing or the case is unusual, lower the confidence instead of guessing.";

    private static JsonObject OutputSchema(WorkflowStep step) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["outputs"] = SchemaOf(step.Input),
            ["decision"] = new JsonObject { ["type"] = "string" },
            ["reasoning"] = new JsonObject { ["type"] = "string" },
            ["confidence"] = new JsonObject { ["type"] = "number" },
        },
        ["required"] = new JsonArray("outputs", "reasoning", "confidence"),
    };

    private static JsonObject SchemaOf(InputSchema? schema)
    {
        var properties = new JsonObject();
        foreach (var field in schema?.Fields.Where(f => !f.Sensitive) ?? [])
        {
            var property = new JsonObject
            {
                ["type"] = field.Type switch
                {
                    InputFieldType.Number => "number",
                    InputFieldType.Boolean => "boolean",
                    _ => "string",
                },
                ["description"] = field.Label,
            };
            if (field.Options.Count > 0)
            {
                property["enum"] = new JsonArray(field.Options.Select(o => (JsonNode?)JsonValue.Create(o.Value)).ToArray());
            }

            properties[field.Name] = property;
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new JsonArray(schema?.Fields.Where(f => f.Required && !f.Sensitive).Select(f => (JsonNode?)JsonValue.Create(f.Name)).ToArray() ?? []),
        };
    }
}
