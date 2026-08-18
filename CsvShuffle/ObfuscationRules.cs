using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CsvShuffle;

public static partial class ObfuscationRules
{
    static readonly string[] CardinalDirections = ["north", "south", "east", "west"];
    static readonly string[] CardinalDirectionAbbreviations = ["n", "e", "s", "w"];
    static readonly string[] AddressUnitTerms = ["apt", "apartment", "suite", "ste", "unit", "floor", "fl"];
    static readonly string[] SinglePartTlds = ["com", "net", "org", "dev", "io", "ai", "app"];
    static readonly string[] MultiPartTlds = ["co.uk", "co.br", "com.au", "co.nz", "co.jp"];

    static readonly string[] RelationshipTerms =
    [
        "spouse", "husband", "wife", "partner", "parent/guardian", "parent", "guardian",
        "child", "son", "daughter", "mother", "father", "sibling", "brother", "sister", "aunt", "uncle",
        "cousin", "grandparent", "grandmother", "grandfather", "niece", "nephew", "relative"
    ];
    static readonly HashSet<string> CardinalDirectionSet = new(CardinalDirections, StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> CardinalDirectionAbbreviationSet = new(CardinalDirectionAbbreviations, StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> AddressUnitTermSet = new(AddressUnitTerms, StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> RelationshipTermSet = new(RelationshipTerms, StringComparer.OrdinalIgnoreCase);

    public static string Transform(
        string value,
        ObfuscationMode mode,
        Dictionary<string, string> consistentValues,
        Dictionary<string, string> rowTokens
    )
    {
        if (mode == ObfuscationMode.Clear || string.IsNullOrEmpty(value))
            return value;

        if (mode is not (
            ObfuscationMode.Phone
            or ObfuscationMode.Ssn
            or ObfuscationMode.Email
            or ObfuscationMode.Relationship)
           )
            return mode switch
            {
                ObfuscationMode.Date => TransformDate(value),
                ObfuscationMode.Address => TransformAddress(value, rowTokens),
                _ => TransformText(value, mode, rowTokens)
            };

        string key = $"{mode}|{value}";

        if (consistentValues.TryGetValue(key, out string? prior))
            return prior;

        string transformed = mode switch
        {
            ObfuscationMode.Phone => TransformPhone(value),
            ObfuscationMode.Ssn => TransformDigits(value),
            ObfuscationMode.Email => TransformEmail(value, rowTokens),
            _ => TransformRelationship(value)
        };

        consistentValues[key] = transformed;
        return transformed;
    }

    static string TransformText(string value, ObfuscationMode mode, Dictionary<string, string> rowTokens)
    {
        var result = new StringBuilder(value.Length);
        bool preserveVowelClass =
            mode is ObfuscationMode.Name or ObfuscationMode.Address or ObfuscationMode.BracketPreserving;
        int bracketDepth = 0;
        bool hasAddressDigits = false;

        for (int index = 0; index < value.Length;)
        {
            char character = value[index];
            if (mode == ObfuscationMode.BracketPreserving)
            {
                switch (character)
                {
                    case '(' or '[' or '{':
                        bracketDepth++;
                        break;
                    case ')' or ']' or '}' when bracketDepth > 0:
                        bracketDepth--;
                        break;
                }

                if (bracketDepth > 0 || character is ')' or ']' or '}')
                {
                    result.Append(character);
                    index++;
                    continue;
                }
            }

            if (char.IsLetter(character))
            {
                int end = index + 1;
                while (end < value.Length && char.IsLetter(value[end]))
                    end++;

                string token = value[index..end];
                string key = $"{mode}|{token}";
                if (!rowTokens.TryGetValue(key, out string? replacement))
                {
                    replacement =
                        new string(token.Select(letter => RandomLetter(letter, preserveVowelClass)).ToArray());
                    rowTokens[key] = replacement;
                }

                result.Append(replacement);
                index = end;
                continue;
            }

            if (char.IsDigit(character))
            {
                bool firstAddressDigit = mode == ObfuscationMode.Address && !hasAddressDigits;
                result.Append(firstAddressDigit && character != '0'
                    ? (char)('1' + Random.Shared.Next(9))
                    : (char)('0' + Random.Shared.Next(10)));
                hasAddressDigits = hasAddressDigits || mode == ObfuscationMode.Address;
                index++;
                continue;
            }

            result.Append(character);
            index++;
        }

        return result.ToString();
    }

    static string TransformDigits(string value) =>
        new(value.Select(character => char.IsDigit(character)
            ? (char)('0' + Random.Shared.Next(10))
            : character).ToArray());

    static string TransformPhone(string value)
    {
        List<int> digitIndexes =
        [
            .. value
                .Select((character, index) => (character, index))
                .Where(item => char.IsDigit(item.character))
                .Select(item => item.index)
        ];

        int areaCodeStart = digitIndexes.Count == 11 ? 1 : 0;
        int exchangeStart = areaCodeStart + 3;
        bool isNanpLength = digitIndexes.Count is 10 or 11;
        char[] result = value.ToCharArray();

        for (int digitIndex = 0; digitIndex < digitIndexes.Count; digitIndex++)
        {
            char source = value[digitIndexes[digitIndex]];

            result[digitIndexes[digitIndex]] = isNanpLength switch
            {
                true when digitIndexes.Count == 11 && digitIndex == 0 => source,
                true when digitIndex == areaCodeStart || digitIndex == exchangeStart =>
                    source is >= '2' and <= '9' ? (char)('2' + Random.Shared.Next(8)) : source,
                _ => (char)('0' + Random.Shared.Next(10))
            };
        }

        return new string(result);
    }

    static string TransformAddress(string value, Dictionary<string, string> rowTokens)
    {
        var result = new StringBuilder(value.Length);
        bool hasAddressDigits = false;

        for (int index = 0; index < value.Length;)
        {
            char character = value[index];
            if (char.IsLetter(character))
            {
                int end = index + 1;
                while (end < value.Length && char.IsLetter(value[end]))
                    end++;

                string token = value[index..end];
                if (AddressUnitTermSet.Contains(token))
                {
                    result.Append(token);
                }
                else if (CardinalDirectionSet.Contains(token))
                {
                    result.Append(PreserveCasing(token, RandomOther(CardinalDirections, token)));
                }
                else if (CardinalDirectionAbbreviationSet.Contains(token))
                {
                    result.Append(PreserveCasing(token, RandomOther(CardinalDirectionAbbreviations, token)));
                }
                else
                {
                    result.Append(TransformAddressLetters(token, rowTokens));
                }

                index = end;
                continue;
            }

            if (char.IsDigit(character))
            {
                result.Append(!hasAddressDigits && character != '0'
                    ? (char)('1' + Random.Shared.Next(9))
                    : (char)('0' + Random.Shared.Next(10)));
                hasAddressDigits = true;
                index++;
                continue;
            }

            result.Append(character);
            index++;
        }

        return result.ToString();
    }

    static string TransformAddressLetters(string token, Dictionary<string, string> rowTokens)
    {
        string key = $"{ObfuscationMode.Address}|{token}";

        if (rowTokens.TryGetValue(key, out string? replacement))
            return replacement;

        replacement = new string(token.Select(letter => RandomLetter(letter, preserveVowelClass: true)).ToArray());
        rowTokens[key] = replacement;

        return replacement;
    }

    static string TransformEmail(string value, Dictionary<string, string> rowTokens)
    {
        int atIndex = value.LastIndexOf('@');
        if (atIndex <= 0 || atIndex == value.Length - 1)
            return TransformText(value, ObfuscationMode.Generic, rowTokens);

        string localPart = TransformText(value[..atIndex], ObfuscationMode.Email, rowTokens);
        string[] labels = value[(atIndex + 1)..].Split('.');
        if (labels.Any(label => label.Length == 0))
            return TransformText(value, ObfuscationMode.Generic, rowTokens);

        string twoPartSuffix = labels.Length >= 2 ? string.Join('.', labels[^2..]) : string.Empty;
        int tldLabelCount = MultiPartTlds.Contains(twoPartSuffix, StringComparer.OrdinalIgnoreCase) ? 2 : 1;
        string sourceTld = string.Join('.', labels[^tldLabelCount..]).ToLowerInvariant();
        string replacementTld = tldLabelCount == 2
            ? RandomOther(MultiPartTlds, sourceTld)
            : RandomOther(SinglePartTlds, sourceTld);

        string[] transformedLabels =
        [
            .. labels[..^tldLabelCount]
                .Select(label => TransformText(label, ObfuscationMode.Email, rowTokens)),
            PreserveCasing(string.Join('.', labels[^tldLabelCount..]), replacementTld)
        ];

        return $"{localPart}@{string.Join('.', transformedLabels)}";
    }

    static string TransformRelationship(string value) =>
        RelationshipRegex().Replace(
            value,
            match => RelationshipTermSet.Contains(match.Value)
                ? PreserveCasing(match.Value, RandomOther(RelationshipTerms, match.Value))
                : match.Value
        );

    static string RandomOther(string[] choices, string current)
    {
        string[] alternatives =
            [.. choices.Where(choice => !choice.Equals(current, StringComparison.OrdinalIgnoreCase))];
        if (alternatives.Length == 0)
            throw new InvalidOperationException("Obfuscation requires at least one replacement value.");

        return alternatives[Random.Shared.Next(alternatives.Length)];
    }

    static string PreserveCasing(string source, string replacement)
    {
        char[] letters = [.. source.Where(char.IsLetter)];
        return letters.Length > 0 && letters.All(char.IsUpper)
            ? replacement.ToUpperInvariant()
            : char.IsUpper(source[0])
                ? char.ToUpperInvariant(replacement[0]) + replacement[1..]
                : replacement;
    }

    static char RandomLetter(
        char source,
        bool preserveVowelClass
    )
    {
        const string vowels = "aeiouy";
        const string consonants = "bcdfghjklmnpqrstvwxz";

        string pool = preserveVowelClass && vowels.Contains(char.ToLowerInvariant(source))
            ? vowels
            : preserveVowelClass
                ? consonants
                : "abcdefghijklmnopqrstuvwxyz";

        char result = pool[Random.Shared.Next(pool.Length)];

        return char.IsUpper(source)
            ? char.ToUpperInvariant(result)
            : result;
    }

    static string TransformDate(string value)
    {
        if (!DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var date)
            && !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date))
            return TransformText(
                value: value,
                mode: ObfuscationMode.Generic,
                rowTokens: []
            );

        return date
            .AddYears(Random.Shared.Next(-5, 6))
            .AddMonths(Random.Shared.Next(-2, 3))
            .AddDays(Random.Shared.Next(-10, 11))
            .ToString("M/d/yyyy", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(
        @"(?<!\p{L})([\p{L}/]+)(?!\p{L})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex RelationshipRegex();
}
