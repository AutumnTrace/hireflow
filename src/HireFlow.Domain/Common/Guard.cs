namespace HireFlow.Domain.Common;

internal static class Guard
{
    public static string NotBlank(string? value, string field, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException(field, $"'{field}' is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new DomainValidationException(field, $"'{field}' must be at most {maxLength} characters.");
        }

        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength = 200)
        => string.IsNullOrWhiteSpace(value) ? null : NotBlank(value, field, maxLength);

    public static List<string> Tags(IEnumerable<string>? values)
        => values?
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
}
