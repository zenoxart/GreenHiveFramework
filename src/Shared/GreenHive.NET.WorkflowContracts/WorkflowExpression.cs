using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GreenHive.NET.Monads;

namespace GreenHive.NET.WorkflowContracts;

/// <summary>
/// A small, safe expression language for mappings and conditions.
/// <para>Reads variables (<c>vars.amount</c>), step results (<c>steps.review.decision</c>), the last step's outputs
/// (<c>last.decision</c>) and the trigger payload (<c>payload.category</c>); compares (== != &lt; &lt;= &gt; &gt;=), combines
/// (&amp;&amp; || !), does simple math (+ - * / %) and string concatenation (+); literals: numbers, 'strings', true, false, null.</para>
/// <para>Built-in functions only: len, lower, upper, trim, contains, startsWith, endsWith, coalesce, isEmpty, round, abs,
/// min, max, number, text. No method calls, no reflection, no I/O - expressions are data, not code.</para>
/// </summary>
public sealed class WorkflowExpression
{
    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "len", "lower", "upper", "trim", "contains", "startsWith", "endsWith", "coalesce", "isEmpty", "round", "abs", "min", "max", "number", "text",
    };

    private readonly Node _root;

    private WorkflowExpression(string text, Node root, IReadOnlyList<string> paths)
    {
        Text = text;
        _root = root;
        Paths = paths;
    }

    public string Text { get; }

    /// <summary>All variable paths the expression reads (e.g. "vars.amount", "payload.category").</summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>Parses and checks an expression; errors appear in the designer and the impact analysis, not at run time.</summary>
    public static Result<WorkflowExpression> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Error.Validation("workflow.expression.empty", "workflow:expression.empty");
        }

        try
        {
            var parser = new Parser(Tokenize(text));
            var root = parser.ParseExpression();
            parser.ExpectEnd();
            var paths = new List<string>();
            Collect(root, paths);
            return new WorkflowExpression(text, root, paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }
        catch (FormatException ex)
        {
            return Error.Validation("workflow.expression.invalid", ex.Message).WithMetadata("expression", text);
        }
    }

    /// <summary>Parses and evaluates in one go.</summary>
    public static Result<object?> Evaluate(string? text, JsonObject scope) => Parse(text).Bind(e => e.Evaluate(scope));

    /// <summary>Evaluates a condition; a missing condition is true.</summary>
    public static Result<bool> EvaluateCondition(string? text, JsonObject scope) =>
        string.IsNullOrWhiteSpace(text) ? true : Evaluate(text, scope).Map(IsTruthy);

    public Result<object?> Evaluate(JsonObject scope)
    {
        try
        {
            return Result<object?>.Success(Eval(_root, scope));
        }
        catch (FormatException ex)
        {
            return Error.Validation("workflow.expression.evaluation", ex.Message).WithMetadata("expression", Text);
        }
    }

    /// <summary>Builds the evaluation scope of an instance.</summary>
    public static JsonObject Scope(JsonObject? variables, JsonObject? stepOutputs = null, JsonObject? last = null, JsonObject? payload = null) => new()
    {
        ["vars"] = variables?.DeepClone() ?? new JsonObject(),
        ["steps"] = stepOutputs?.DeepClone() ?? new JsonObject(),
        ["last"] = last?.DeepClone() ?? new JsonObject(),
        ["payload"] = payload?.DeepClone() ?? new JsonObject(),
    };

    /// <summary>Converts an evaluation result to a JSON node (for writing variables and parameters).</summary>
    public static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        JsonNode node => node.DeepClone(),
        bool b => JsonValue.Create(b),
        decimal d => JsonValue.Create(d),
        string s => JsonValue.Create(s),
        _ => JsonValue.Create(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    public static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        decimal d => d != 0,
        string s => s.Length > 0 && !s.Equals("false", StringComparison.OrdinalIgnoreCase),
        JsonArray a => a.Count > 0,
        _ => true,
    };

    public override string ToString() => Text;

    // ------------------------------------------------------------------ evaluation

    private static object? Eval(Node node, JsonObject scope) => node switch
    {
        Literal l => l.Value,
        PathNode p => Resolve(scope, p.Segments),
        Unary { Op: "!" } u => !IsTruthy(Eval(u.Operand, scope)),
        Unary { Op: "-" } u => -Number(Eval(u.Operand, scope)),
        Binary { Op: "&&" } b => IsTruthy(Eval(b.Left, scope)) && IsTruthy(Eval(b.Right, scope)),
        Binary { Op: "||" } b => IsTruthy(Eval(b.Left, scope)) || IsTruthy(Eval(b.Right, scope)),
        Binary b => Apply(b.Op, Eval(b.Left, scope), Eval(b.Right, scope)),
        Call c => Invoke(c.Name, c.Arguments.Select(a => Eval(a, scope)).ToList()),
        _ => throw new FormatException("Unknown expression node."),
    };

    private static object? Resolve(JsonObject scope, IReadOnlyList<string> segments)
    {
        JsonNode? current = scope;
        foreach (var segment in segments)
        {
            current = current switch
            {
                JsonObject obj => obj.FirstOrDefault(kv => kv.Key.Equals(segment, StringComparison.OrdinalIgnoreCase)).Value,
                JsonArray arr when int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var i) && i < arr.Count => arr[i],
                _ => null,
            };
            if (current is null)
            {
                return null;
            }
        }

        return FromNode(current);
    }

    private static object? FromNode(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return node;
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => value.GetValue<decimal>(),
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => value.ToJsonString(),
        };
    }

    private static object? Apply(string op, object? left, object? right)
    {
        switch (op)
        {
            case "==":
                return AreEqual(left, right);
            case "!=":
                return !AreEqual(left, right);
            case "<":
                return Compare(left, right) < 0;
            case "<=":
                return Compare(left, right) <= 0;
            case ">":
                return Compare(left, right) > 0;
            case ">=":
                return Compare(left, right) >= 0;
            case "+" when left is string || right is string:
                return AsText(left) + AsText(right);
            case "+":
                return Number(left) + Number(right);
            case "-":
                return Number(left) - Number(right);
            case "*":
                return Number(left) * Number(right);
            case "/":
                var divisor = Number(right);
                return divisor == 0 ? throw new FormatException("Division by zero.") : Number(left) / divisor;
            case "%":
                var modulo = Number(right);
                return modulo == 0 ? throw new FormatException("Division by zero.") : Number(left) % modulo;
            default:
                throw new FormatException($"Unknown operator '{op}'.");
        }
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (TryNumber(left, out var a) && TryNumber(right, out var b))
        {
            return a == b;
        }

        if (left is bool || right is bool)
        {
            return IsTruthy(left) == IsTruthy(right);
        }

        return string.Equals(AsText(left), AsText(right), StringComparison.OrdinalIgnoreCase);
    }

    private static int Compare(object? left, object? right)
    {
        if (TryNumber(left, out var a) && TryNumber(right, out var b))
        {
            return a.CompareTo(b);
        }

        if (left is string ls && right is string rs &&
            DateTimeOffset.TryParse(ls, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ld) &&
            DateTimeOffset.TryParse(rs, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var rd))
        {
            return ld.CompareTo(rd);
        }

        return string.Compare(AsText(left), AsText(right), StringComparison.OrdinalIgnoreCase);
    }

    private static object? Invoke(string name, IReadOnlyList<object?> args)
    {
        object? Arg(int i) => i < args.Count ? args[i] : null;

        return name.ToLowerInvariant() switch
        {
            "len" => Arg(0) switch
            {
                null => 0m,
                JsonArray a => (decimal)a.Count,
                JsonObject o => (decimal)o.Count,
                var v => (decimal)AsText(v).Length,
            },
            "lower" => AsText(Arg(0)).ToLowerInvariant(),
            "upper" => AsText(Arg(0)).ToUpperInvariant(),
            "trim" => AsText(Arg(0)).Trim(),
            "contains" => Arg(0) is JsonArray array
                ? array.Any(item => AreEqual(FromNode(item), Arg(1)))
                : AsText(Arg(0)).Contains(AsText(Arg(1)), StringComparison.OrdinalIgnoreCase),
            "startswith" => AsText(Arg(0)).StartsWith(AsText(Arg(1)), StringComparison.OrdinalIgnoreCase),
            "endswith" => AsText(Arg(0)).EndsWith(AsText(Arg(1)), StringComparison.OrdinalIgnoreCase),
            "coalesce" => args.FirstOrDefault(a => a is not null && !(a is string s && s.Length == 0)),
            "isempty" => Arg(0) is null || (Arg(0) is string s2 && s2.Trim().Length == 0) || (Arg(0) is JsonArray a2 && a2.Count == 0),
            "round" => Math.Round(Number(Arg(0)), (int)(args.Count > 1 ? Number(Arg(1)) : 0), MidpointRounding.AwayFromZero),
            "abs" => Math.Abs(Number(Arg(0))),
            "min" => args.Select(Number).Min(),
            "max" => args.Select(Number).Max(),
            "number" => TryNumber(Arg(0), out var n) ? n : null,
            "text" => AsText(Arg(0)),
            _ => throw new FormatException($"Unknown function '{name}'."),
        };
    }

    private static bool TryNumber(object? value, out decimal number)
    {
        switch (value)
        {
            case decimal d:
                number = d;
                return true;
            case string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed):
                number = parsed;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static decimal Number(object? value) =>
        value is null ? 0 : TryNumber(value, out var n) ? n : throw new FormatException($"'{AsText(value)}' is not a number.");

    private static string AsText(object? value) => value switch
    {
        null => string.Empty,
        bool b => b ? "true" : "false",
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        string s => s,
        JsonNode node => node.ToJsonString(),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static void Collect(Node node, List<string> paths)
    {
        switch (node)
        {
            case PathNode p:
                paths.Add(string.Join('.', p.Segments));
                break;
            case Unary u:
                Collect(u.Operand, paths);
                break;
            case Binary b:
                Collect(b.Left, paths);
                Collect(b.Right, paths);
                break;
            case Call c:
                c.Arguments.ForEach(a => Collect(a, paths));
                break;
        }
    }

    // ------------------------------------------------------------------ tokenizer and parser

    private enum TokenKind
    {
        Number,
        String,
        Identifier,
        Operator,
        End,
    }

    private sealed record Token(TokenKind Kind, string Text, int Position);

    private abstract record Node;

    private sealed record Literal(object? Value) : Node;

    private sealed record PathNode(IReadOnlyList<string> Segments) : Node;

    private sealed record Unary(string Op, Node Operand) : Node;

    private sealed record Binary(string Op, Node Left, Node Right) : Node;

    private sealed record Call(string Name, List<Node> Arguments) : Node;

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            var start = i;
            if (char.IsDigit(c))
            {
                while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Number, text[start..i], start));
            }
            else if (c is '\'' or '"')
            {
                var sb = new StringBuilder();
                i++;
                while (i < text.Length && text[i] != c)
                {
                    if (text[i] == '\\' && i + 1 < text.Length)
                    {
                        i++;
                    }

                    sb.Append(text[i]);
                    i++;
                }

                if (i >= text.Length)
                {
                    throw new FormatException($"Unterminated string at position {start}.");
                }

                i++;
                tokens.Add(new Token(TokenKind.String, sb.ToString(), start));
            }
            else if (char.IsLetter(c) || c == '_')
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] is '_' or '.'))
                {
                    i++;
                }

                tokens.Add(new Token(TokenKind.Identifier, text[start..i].TrimEnd('.'), start));
            }
            else
            {
                var two = i + 1 < text.Length ? text.Substring(i, 2) : string.Empty;
                if (two is "==" or "!=" or "<=" or ">=" or "&&" or "||")
                {
                    tokens.Add(new Token(TokenKind.Operator, two, start));
                    i += 2;
                }
                else if ("+-*/%<>!(),".Contains(c))
                {
                    tokens.Add(new Token(TokenKind.Operator, c.ToString(), start));
                    i++;
                }
                else
                {
                    throw new FormatException($"Unexpected character '{c}' at position {start}.");
                }
            }
        }

        tokens.Add(new Token(TokenKind.End, string.Empty, text.Length));
        return tokens;
    }

    private sealed class Parser(List<Token> tokens)
    {
        private int _index;

        private Token Current => tokens[_index];

        public void ExpectEnd()
        {
            if (Current.Kind != TokenKind.End)
            {
                throw new FormatException($"Unexpected '{Current.Text}' at position {Current.Position}.");
            }
        }

        public Node ParseExpression() => ParseOr();

        private Node ParseOr() => ParseLeft(ParseAnd, "||");

        private Node ParseAnd() => ParseLeft(ParseEquality, "&&");

        private Node ParseEquality() => ParseLeft(ParseComparison, "==", "!=");

        private Node ParseComparison() => ParseLeft(ParseAdditive, "<", "<=", ">", ">=");

        private Node ParseAdditive() => ParseLeft(ParseMultiplicative, "+", "-");

        private Node ParseMultiplicative() => ParseLeft(ParseUnary, "*", "/", "%");

        private Node ParseLeft(Func<Node> next, params string[] operators)
        {
            var left = next();
            while (Current.Kind == TokenKind.Operator && operators.Contains(Current.Text))
            {
                var op = Current.Text;
                _index++;
                left = new Binary(op, left, next());
            }

            return left;
        }

        private Node ParseUnary()
        {
            if (Current is { Kind: TokenKind.Operator, Text: "!" or "-" })
            {
                var op = Current.Text;
                _index++;
                return new Unary(op, ParseUnary());
            }

            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            var token = Current;
            _index++;
            switch (token.Kind)
            {
                case TokenKind.Number:
                    return decimal.TryParse(token.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n)
                        ? new Literal(n)
                        : throw new FormatException($"Invalid number '{token.Text}' at position {token.Position}.");
                case TokenKind.String:
                    return new Literal(token.Text);
                case TokenKind.Identifier:
                    switch (token.Text.ToLowerInvariant())
                    {
                        case "true":
                            return new Literal(true);
                        case "false":
                            return new Literal(false);
                        case "null":
                            return new Literal(null);
                    }

                    if (Current is { Kind: TokenKind.Operator, Text: "(" })
                    {
                        if (!Functions.Contains(token.Text))
                        {
                            throw new FormatException($"Unknown function '{token.Text}' at position {token.Position}.");
                        }

                        _index++;
                        var args = new List<Node>();
                        if (Current is not { Kind: TokenKind.Operator, Text: ")" })
                        {
                            args.Add(ParseExpression());
                            while (Current is { Kind: TokenKind.Operator, Text: "," })
                            {
                                _index++;
                                args.Add(ParseExpression());
                            }
                        }

                        Expect(")");
                        return new Call(token.Text, args);
                    }

                    var segments = token.Text.Split('.');
                    if (segments[0].ToLowerInvariant() is not ("vars" or "steps" or "last" or "payload"))
                    {
                        throw new FormatException(
                            $"Unknown name '{token.Text}' at position {token.Position}; use vars., steps., last. or payload.");
                    }

                    return new PathNode(segments);
                case TokenKind.Operator when token.Text == "(":
                    var inner = ParseExpression();
                    Expect(")");
                    return inner;
                default:
                    throw new FormatException(token.Kind == TokenKind.End
                        ? "Unexpected end of expression."
                        : $"Unexpected '{token.Text}' at position {token.Position}.");
            }
        }

        private void Expect(string text)
        {
            if (Current.Kind != TokenKind.Operator || Current.Text != text)
            {
                throw new FormatException($"Expected '{text}' at position {Current.Position}.");
            }

            _index++;
        }
    }
}
