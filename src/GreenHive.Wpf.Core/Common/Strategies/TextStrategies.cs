using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GreenHive.NET.Core.Common.Strategies;

#region Basic Text Strategies

/// <summary>
/// Strategy that converts text to uppercase.
/// Handles any non-empty string input.
/// </summary>
public class UpperCaseStrategy : IStrategy<string, string>
{
    public string Name => "UpperCase";
    public string Description => "Konvertiert Text in Grossbuchstaben";
    public int Priority => 10;

    public bool CanHandle(string input) => !string.IsNullOrEmpty(input);

    public string Execute(string input) => input.ToUpperInvariant();
}

/// <summary>
/// Strategy that converts text to lowercase.
/// Handles any non-empty string input.
/// </summary>
public class LowerCaseStrategy : IStrategy<string, string>
{
    public string Name => "LowerCase";
    public string Description => "Konvertiert Text in Kleinbuchstaben";
    public int Priority => 10;

    public bool CanHandle(string input) => !string.IsNullOrEmpty(input);

    public string Execute(string input) => input.ToLowerInvariant();
}

/// <summary>
/// Strategy that reverses text.
/// </summary>
public class ReverseStrategy : IStrategy<string, string>
{
    public string Name => "Reverse";
    public string Description => "Kehrt den Text um";
    public int Priority => 20;

    public bool CanHandle(string input) => !string.IsNullOrEmpty(input);

    public string Execute(string input)
    {
        var charArray = input.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }
}

#endregion

#region Data Format Strategies

/// <summary>
/// Strategy that formats numbers in text with thousand separators.
/// </summary>
public class NumberFormatterStrategy : IStrategy<string, string>
{
    public string Name => "NumberFormatter";
    public string Description => "Formatiert Zahlen mit Tausendertrennzeichen";
    public int Priority => 100;

    public bool CanHandle(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;
        
        return input.Any(char.IsDigit);
    }

    public string Execute(string input)
    {
        var result = new StringBuilder();
        var currentNumber = new StringBuilder();
        
        foreach (var c in input)
        {
            if (char.IsDigit(c))
            {
                currentNumber.Append(c);
            }
            else
            {
                if (currentNumber.Length > 0)
                {
                    if (long.TryParse(currentNumber.ToString(), out var number))
                    {
                        result.Append(number.ToString("N0", CultureInfo.CurrentCulture));
                    }
                    else
                    {
                        result.Append(currentNumber);
                    }
                    currentNumber.Clear();
                }
                result.Append(c);
            }
        }
        
        if (currentNumber.Length > 0)
        {
            if (long.TryParse(currentNumber.ToString(), out var number))
            {
                result.Append(number.ToString("N0", CultureInfo.CurrentCulture));
            }
            else
            {
                result.Append(currentNumber);
            }
        }
        
        return result.ToString();
    }
}

/// <summary>
/// Strategy that parses and formats email addresses.
/// </summary>
public class EmailFormatterStrategy : IStrategy<string, string>
{
    public string Name => "EmailFormatter";
    public string Description => "Analysiert und formatiert E-Mail-Adressen";
    public int Priority => 150;

    public bool CanHandle(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;
        
        return input.Contains('@') && input.Contains('.');
    }

    public string Execute(string input)
    {
        var parts = input.Split('@');
        if (parts.Length == 2)
        {
            var domainParts = parts[1].Split('.');
            var tld = domainParts.Length > 1 ? domainParts[^1].ToUpperInvariant() : "?";
            return $"[E-Mail] Benutzer: {parts[0]} | Domain: {parts[1]} | TLD: {tld}";
        }
        return input;
    }
}

/// <summary>
/// Strategy that parses and validates JSON input.
/// </summary>
public class JsonParserStrategy : IStrategy<string, string>
{
    public string Name => "JsonParser";
    public string Description => "Validiert und formatiert JSON-Daten";
    public int Priority => 200;

    public bool CanHandle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        var trimmed = input.Trim();
        return (trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
               (trimmed.StartsWith('[') && trimmed.EndsWith(']'));
    }

    public string Execute(string input)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var formatted = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            
            var elementCount = CountElements(doc.RootElement);
            return $"[JSON Valid] {elementCount} Elemente gefunden\n\n{formatted}";
        }
        catch (JsonException ex)
        {
            return $"[JSON Invalid] Fehler: {ex.Message}";
        }
    }

    private static int CountElements(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().Count(),
            JsonValueKind.Array => element.EnumerateArray().Count(),
            _ => 1
        };
    }
}

/// <summary>
/// Strategy that parses and formats URL strings.
/// </summary>
public class UrlParserStrategy : IStrategy<string, string>
{
    public string Name => "UrlParser";
    public string Description => "Analysiert URLs und extrahiert Komponenten";
    public int Priority => 180;

    public bool CanHandle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        return input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               input.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               input.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase);
    }

    public string Execute(string input)
    {
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[URL Analyse]");
            sb.AppendLine($"  Protokoll: {uri.Scheme}");
            sb.AppendLine($"  Host: {uri.Host}");
            sb.AppendLine($"  Port: {(uri.IsDefaultPort ? "Standard" : uri.Port.ToString())}");
            
            if (!string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/")
                sb.AppendLine($"  Pfad: {uri.AbsolutePath}");
            
            if (!string.IsNullOrEmpty(uri.Query))
            {
                sb.AppendLine($"  Query-Parameter:");
                var queryParams = uri.Query.TrimStart('?').Split('&');
                foreach (var param in queryParams)
                {
                    var parts = param.Split('=');
                    if (parts.Length == 2)
                        sb.AppendLine($"    - {parts[0]}: {Uri.UnescapeDataString(parts[1])}");
                }
            }
            
            if (!string.IsNullOrEmpty(uri.Fragment))
                sb.AppendLine($"  Fragment: {uri.Fragment}");
            
            return sb.ToString().TrimEnd();
        }
        
        return $"[URL Invalid] Keine gueltige URL: {input}";
    }
}

/// <summary>
/// Strategy that parses date/time strings and formats them.
/// </summary>
public class DateTimeParserStrategy : IStrategy<string, string>
{
    public string Name => "DateTimeParser";
    public string Description => "Erkennt und formatiert Datum/Zeit-Angaben";
    public int Priority => 160;

    private static readonly string[] DateFormats =
    [
        "dd.MM.yyyy", "dd/MM/yyyy", "yyyy-MM-dd",
        "dd.MM.yyyy HH:mm", "dd.MM.yyyy HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ",
        "dd-MM-yyyy", "MM/dd/yyyy"
    ];

    public bool CanHandle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        return DateTime.TryParse(input, out _) ||
               DateFormats.Any(f => DateTime.TryParseExact(input.Trim(), f, 
                   CultureInfo.InvariantCulture, DateTimeStyles.None, out _));
    }

    public string Execute(string input)
    {
        if (DateTime.TryParse(input.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Datum/Zeit Analyse]");
            sb.AppendLine($"  Datum: {date:dddd, dd. MMMM yyyy}");
            sb.AppendLine($"  Zeit: {date:HH:mm:ss}");
            sb.AppendLine($"  Wochentag: {date:dddd}");
            sb.AppendLine($"  Kalenderwoche: {GetIso8601WeekOfYear(date)}");
            sb.AppendLine($"  Tag im Jahr: {date.DayOfYear}");
            sb.AppendLine($"  ISO 8601: {date:yyyy-MM-ddTHH:mm:ss}");
            sb.AppendLine($"  Unix Timestamp: {((DateTimeOffset)date).ToUnixTimeSeconds()}");
            
            var diff = date - DateTime.Now;
            if (diff.TotalDays > 0)
                sb.AppendLine($"  Zeitdifferenz: In {Math.Abs(diff.Days)} Tagen");
            else if (diff.TotalDays < 0)
                sb.AppendLine($"  Zeitdifferenz: Vor {Math.Abs(diff.Days)} Tagen");
            
            return sb.ToString().TrimEnd();
        }
        
        return $"[Datum Invalid] Konnte nicht geparst werden: {input}";
    }

    private static int GetIso8601WeekOfYear(DateTime date)
    {
        var day = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            date = date.AddDays(3);
        return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }
}

#endregion

#region Security & Encoding Strategies

/// <summary>
/// Strategy that encodes text to Base64.
/// </summary>
public class Base64EncodeStrategy : IStrategy<string, string>
{
    public string Name => "Base64Encode";
    public string Description => "Kodiert Text in Base64-Format";
    public int Priority => 30;

    public bool CanHandle(string input) => !string.IsNullOrEmpty(input);

    public string Execute(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var base64 = Convert.ToBase64String(bytes);
        return $"[Base64 Encoded]\n{base64}\n\nLaenge: {base64.Length} Zeichen";
    }
}

/// <summary>
/// Strategy that decodes Base64 strings.
/// </summary>
public class Base64DecodeStrategy : IStrategy<string, string>
{
    public string Name => "Base64Decode";
    public string Description => "Dekodiert Base64-Format zu Text";
    public int Priority => 170;

    public bool CanHandle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        var trimmed = input.Trim();
        if (trimmed.Length % 4 != 0)
            return false;
        
        return Regex.IsMatch(trimmed, @"^[A-Za-z0-9+/]*={0,2}$");
    }

    public string Execute(string input)
    {
        try
        {
            var bytes = Convert.FromBase64String(input.Trim());
            var decoded = Encoding.UTF8.GetString(bytes);
            return $"[Base64 Decoded]\n{decoded}";
        }
        catch (FormatException)
        {
            return "[Base64 Error] Ungültiges Base64-Format";
        }
    }
}

/// <summary>
/// Strategy that calculates hash values of input text.
/// </summary>
public class HashCalculatorStrategy : IStrategy<string, string>
{
    public string Name => "HashCalculator";
    public string Description => "Berechnet verschiedene Hash-Werte (MD5, SHA256, SHA512)";
    public int Priority => 25;

    public bool CanHandle(string input) => !string.IsNullOrEmpty(input);

    public string Execute(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        
        var sb = new StringBuilder();
        sb.AppendLine("[Hash-Werte]");
        sb.AppendLine($"  Input-Laenge: {input.Length} Zeichen / {bytes.Length} Bytes");
        sb.AppendLine();
        
        // MD5
        var md5Hash = MD5.HashData(bytes);
        sb.AppendLine($"  MD5:    {Convert.ToHexString(md5Hash).ToLowerInvariant()}");
        
        // SHA256
        var sha256Hash = SHA256.HashData(bytes);
        sb.AppendLine($"  SHA256: {Convert.ToHexString(sha256Hash).ToLowerInvariant()}");
        
        // SHA512
        var sha512Hash = SHA512.HashData(bytes);
        sb.AppendLine($"  SHA512: {Convert.ToHexString(sha512Hash).ToLowerInvariant()}");
        
        return sb.ToString().TrimEnd();
    }
}

#endregion

#region Text Analysis Strategies

/// <summary>
/// Strategy that analyzes text statistics.
/// </summary>
public class TextStatisticsStrategy : IStrategy<string, string>
{
    public string Name => "TextStatistics";
    public string Description => "Analysiert Textstatistiken (Woerter, Zeichen, Saetze)";
    public int Priority => 15;

    public bool CanHandle(string input) => !string.IsNullOrEmpty(input);

    public string Execute(string input)
    {
        var charCount = input.Length;
        var charCountNoSpaces = input.Count(c => !char.IsWhiteSpace(c));
        var wordCount = input.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;
        var sentenceCount = input.Count(c => c == '.' || c == '!' || c == '?');
        var lineCount = input.Split('\n').Length;
        var paragraphCount = Regex.Matches(input, @"\n\s*\n").Count + 1;
        
        var letterCount = input.Count(char.IsLetter);
        var digitCount = input.Count(char.IsDigit);
        var upperCount = input.Count(char.IsUpper);
        var lowerCount = input.Count(char.IsLower);
        
        var avgWordLength = wordCount > 0 
            ? (double)charCountNoSpaces / wordCount 
            : 0;

        var sb = new StringBuilder();
        sb.AppendLine("[Textstatistik]");
        sb.AppendLine($"  Zeichen (gesamt): {charCount:N0}");
        sb.AppendLine($"  Zeichen (ohne Leerzeichen): {charCountNoSpaces:N0}");
        sb.AppendLine($"  Woerter: {wordCount:N0}");
        sb.AppendLine($"  Saetze: {sentenceCount:N0}");
        sb.AppendLine($"  Zeilen: {lineCount:N0}");
        sb.AppendLine($"  Absaetze: {paragraphCount:N0}");
        sb.AppendLine();
        sb.AppendLine($"  Buchstaben: {letterCount:N0}");
        sb.AppendLine($"  Ziffern: {digitCount:N0}");
        sb.AppendLine($"  Grossbuchstaben: {upperCount:N0}");
        sb.AppendLine($"  Kleinbuchstaben: {lowerCount:N0}");
        sb.AppendLine();
        sb.AppendLine($"  Durchschn. Wortlaenge: {avgWordLength:F1} Zeichen");
        
        if (wordCount > 0 && sentenceCount > 0)
        {
            var readingTimeMinutes = wordCount / 200.0;
            sb.AppendLine($"  Geschaetzte Lesezeit: {readingTimeMinutes:F1} Minuten");
        }
        
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Strategy that extracts and counts unique words.
/// </summary>
public class WordFrequencyStrategy : IStrategy<string, string>
{
    public string Name => "WordFrequency";
    public string Description => "Zaehlt Worthaeufigkeiten und zeigt Top-Woerter";
    public int Priority => 18;

    public bool CanHandle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        var words = input.Split([' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':'], 
            StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 3;
    }

    public string Execute(string input)
    {
        var words = Regex.Matches(input.ToLowerInvariant(), @"\b[\w]+\b")
            .Select(m => m.Value)
            .Where(w => w.Length > 1)
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(15)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("[Worthaeufigkeit - Top 15]");
        sb.AppendLine();
        
        var maxCount = words.FirstOrDefault()?.Count() ?? 1;
        foreach (var group in words)
        {
            var barLength = (int)((double)group.Count() / maxCount * 20);
            var bar = new string('#', barLength);
            sb.AppendLine($"  {group.Key,-15} {group.Count(),4}x  {bar}");
        }
        
        sb.AppendLine();
        sb.AppendLine($"  Eindeutige Woerter: {words.Count}");
        
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Strategy that validates and analyzes IP addresses.
/// </summary>
public class IpAddressStrategy : IStrategy<string, string>
{
    public string Name => "IpAddressAnalyzer";
    public string Description => "Analysiert IPv4/IPv6-Adressen und CIDR-Notation";
    public int Priority => 190;

    public bool CanHandle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        var trimmed = input.Trim().Split('/')[0];
        return System.Net.IPAddress.TryParse(trimmed, out _);
    }

    public string Execute(string input)
    {
        var parts = input.Trim().Split('/');
        var ipPart = parts[0];
        
        if (!System.Net.IPAddress.TryParse(ipPart, out var ip))
            return "[IP Invalid] Keine gueltige IP-Adresse";

        var sb = new StringBuilder();
        sb.AppendLine("[IP-Adresse Analyse]");
        sb.AppendLine($"  Adresse: {ip}");
        sb.AppendLine($"  Version: IPv{(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? "4" : "6")}");
        
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            sb.AppendLine($"  Bytes: {string.Join(".", bytes)}");
            sb.AppendLine($"  Binaer: {string.Join(".", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')))}");
            
            // Determine class
            var firstOctet = bytes[0];
            var ipClass = firstOctet switch
            {
                < 128 => "A (1.0.0.0 - 126.255.255.255)",
                < 192 => "B (128.0.0.0 - 191.255.255.255)",
                < 224 => "C (192.0.0.0 - 223.255.255.255)",
                < 240 => "D Multicast (224.0.0.0 - 239.255.255.255)",
                _ => "E Reserved (240.0.0.0 - 255.255.255.255)"
            };
            sb.AppendLine($"  Klasse: {ipClass}");
            
            // Check for special addresses
            if (bytes[0] == 10 || 
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168))
            {
                sb.AppendLine($"  Typ: Private Adresse (RFC 1918)");
            }
            else if (bytes[0] == 127)
            {
                sb.AppendLine($"  Typ: Loopback-Adresse");
            }
            
            // CIDR notation
            if (parts.Length == 2 && int.TryParse(parts[1], out var cidr) && cidr >= 0 && cidr <= 32)
            {
                var hostBits = 32 - cidr;
                var hostCount = (long)Math.Pow(2, hostBits) - 2;
                sb.AppendLine();
                sb.AppendLine($"  CIDR: /{cidr}");
                sb.AppendLine($"  Hosts: {(hostCount > 0 ? hostCount : 0):N0}");
            }
        }
        
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Strategy that validates and formats credit card numbers (with masking).
/// </summary>
public class CreditCardValidatorStrategy : IStrategy<string, string>
{
    public string Name => "CreditCardValidator";
    public string Description => "Validiert Kreditkartennummern (Luhn-Algorithmus)";
    public int Priority => 195;

    public bool CanHandle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        var digitsOnly = new string(input.Where(char.IsDigit).ToArray());
        return digitsOnly.Length >= 13 && digitsOnly.Length <= 19;
    }

    public string Execute(string input)
    {
        var digitsOnly = new string(input.Where(char.IsDigit).ToArray());
        
        var sb = new StringBuilder();
        sb.AppendLine("[Kreditkarten-Analyse]");
        
        // Mask the number for security
        var masked = digitsOnly.Length > 4 
            ? new string('*', digitsOnly.Length - 4) + digitsOnly[^4..] 
            : digitsOnly;
        sb.AppendLine($"  Nummer (maskiert): {masked}");
        sb.AppendLine($"  Laenge: {digitsOnly.Length} Ziffern");
        
        // Detect card type
        var cardType = DetectCardType(digitsOnly);
        sb.AppendLine($"  Kartentyp: {cardType}");
        
        // Luhn validation
        var isValid = ValidateLuhn(digitsOnly);
        sb.AppendLine($"  Luhn-Pruefung: {(isValid ? "Gueltig" : "Ungueltig")}");
        
        return sb.ToString().TrimEnd();
    }

    private static string DetectCardType(string number)
    {
        if (number.StartsWith("4"))
            return "Visa";
        if (number.StartsWith("51") || number.StartsWith("52") || 
            number.StartsWith("53") || number.StartsWith("54") || number.StartsWith("55"))
            return "MasterCard";
        if (number.StartsWith("34") || number.StartsWith("37"))
            return "American Express";
        if (number.StartsWith("6011") || number.StartsWith("65"))
            return "Discover";
        if (number.StartsWith("35"))
            return "JCB";
        
        return "Unbekannt";
    }

    private static bool ValidateLuhn(string number)
    {
        var sum = 0;
        var alternate = false;
        
        for (var i = number.Length - 1; i >= 0; i--)
        {
            var digit = number[i] - '0';
            
            if (alternate)
            {
                digit *= 2;
                if (digit > 9)
                    digit -= 9;
            }
            
            sum += digit;
            alternate = !alternate;
        }
        
        return sum % 10 == 0;
    }
}

#endregion

#region Code & Development Strategies

/// <summary>
/// Strategy that formats SQL queries.
/// </summary>
public class SqlFormatterStrategy : IStrategy<string, string>
{
    public string Name => "SqlFormatter";
    public string Description => "Formatiert und analysiert SQL-Queries";
    public int Priority => 185;

    private static readonly string[] SqlKeywords = 
    [
        "SELECT", "FROM", "WHERE", "JOIN", "LEFT", "RIGHT", "INNER", "OUTER",
        "ON", "AND", "OR", "ORDER", "BY", "GROUP", "HAVING", "INSERT", "INTO",
        "VALUES", "UPDATE", "SET", "DELETE", "CREATE", "ALTER", "DROP", "TABLE",
        "INDEX", "VIEW", "AS", "DISTINCT", "TOP", "LIMIT", "OFFSET", "UNION",
        "BETWEEN", "LIKE", "IN", "IS", "NULL", "NOT", "EXISTS", "CASE", "WHEN",
        "THEN", "ELSE", "END", "COUNT", "SUM", "AVG", "MIN", "MAX"
    ];

    public bool CanHandle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        var upper = input.ToUpperInvariant();
        var keywordCount = SqlKeywords.Count(k => upper.Contains(k));
        return keywordCount >= 2;
    }

    public string Execute(string input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[SQL Analyse]");
        
        var upper = input.ToUpperInvariant();
        
        // Detect query type
        var queryType = "Unbekannt";
        if (upper.TrimStart().StartsWith("SELECT")) queryType = "SELECT (Abfrage)";
        else if (upper.TrimStart().StartsWith("INSERT")) queryType = "INSERT (Einfuegen)";
        else if (upper.TrimStart().StartsWith("UPDATE")) queryType = "UPDATE (Aktualisieren)";
        else if (upper.TrimStart().StartsWith("DELETE")) queryType = "DELETE (Loeschen)";
        else if (upper.TrimStart().StartsWith("CREATE")) queryType = "CREATE (Erstellen)";
        else if (upper.TrimStart().StartsWith("ALTER")) queryType = "ALTER (Aendern)";
        else if (upper.TrimStart().StartsWith("DROP")) queryType = "DROP (Loeschen)";
        
        sb.AppendLine($"  Query-Typ: {queryType}");
        
        // Count keywords
        var foundKeywords = SqlKeywords.Where(k => upper.Contains(k)).ToList();
        sb.AppendLine($"  Keywords gefunden: {foundKeywords.Count}");
        sb.AppendLine($"  Keywords: {string.Join(", ", foundKeywords.Take(10))}");
        
        // Basic formatting
        sb.AppendLine();
        sb.AppendLine("[Formatiert]");
        
        var formatted = input;
        foreach (var keyword in new[] { "SELECT", "FROM", "WHERE", "JOIN", "LEFT JOIN", 
            "RIGHT JOIN", "INNER JOIN", "ORDER BY", "GROUP BY", "HAVING", "AND", "OR" })
        {
            formatted = Regex.Replace(formatted, $@"\b{keyword}\b", 
                $"\n{keyword}", RegexOptions.IgnoreCase);
        }
        
        sb.AppendLine(formatted.Trim());
        
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Strategy that converts text between different case formats.
/// </summary>
public class CaseConverterStrategy : IStrategy<string, string>
{
    public string Name => "CaseConverter";
    public string Description => "Konvertiert zwischen camelCase, PascalCase, snake_case, kebab-case";
    public int Priority => 35;

    public bool CanHandle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        // Check if it looks like an identifier
        return Regex.IsMatch(input.Trim(), @"^[a-zA-Z_][a-zA-Z0-9_\-]*$") ||
               input.Contains('_') || input.Contains('-') ||
               Regex.IsMatch(input, @"[a-z][A-Z]");
    }

    public string Execute(string input)
    {
        var words = SplitIntoWords(input.Trim());
        
        var sb = new StringBuilder();
        sb.AppendLine("[Case-Konvertierung]");
        sb.AppendLine($"  Original: {input}");
        sb.AppendLine($"  Woerter erkannt: {string.Join(" | ", words)}");
        sb.AppendLine();
        sb.AppendLine($"  camelCase:  {ToCamelCase(words)}");
        sb.AppendLine($"  PascalCase: {ToPascalCase(words)}");
        sb.AppendLine($"  snake_case: {ToSnakeCase(words)}");
        sb.AppendLine($"  kebab-case: {ToKebabCase(words)}");
        sb.AppendLine($"  SCREAMING:  {ToScreamingSnakeCase(words)}");
        
        return sb.ToString().TrimEnd();
    }

    private static List<string> SplitIntoWords(string input)
    {
        // Split by common separators and camelCase boundaries
        var words = new List<string>();
        
        if (input.Contains('_'))
            words.AddRange(input.Split('_', StringSplitOptions.RemoveEmptyEntries));
        else if (input.Contains('-'))
            words.AddRange(input.Split('-', StringSplitOptions.RemoveEmptyEntries));
        else
        {
            // Split camelCase/PascalCase
            var current = new StringBuilder();
            foreach (var c in input)
            {
                if (char.IsUpper(c) && current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
                current.Append(c);
            }
            if (current.Length > 0)
                words.Add(current.ToString());
        }
        
        return words.Select(w => w.ToLowerInvariant()).ToList();
    }

    private static string ToCamelCase(List<string> words)
    {
        if (words.Count == 0) return "";
        return words[0] + string.Concat(words.Skip(1).Select(w => char.ToUpper(w[0]) + w[1..]));
    }

    private static string ToPascalCase(List<string> words)
    {
        return string.Concat(words.Select(w => char.ToUpper(w[0]) + w[1..]));
    }

    private static string ToSnakeCase(List<string> words)
    {
        return string.Join("_", words);
    }

    private static string ToKebabCase(List<string> words)
    {
        return string.Join("-", words);
    }

    private static string ToScreamingSnakeCase(List<string> words)
    {
        return string.Join("_", words.Select(w => w.ToUpperInvariant()));
    }
}

#endregion
