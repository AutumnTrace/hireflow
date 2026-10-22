using HireFlow.Domain.Common;

namespace HireFlow.Domain.Jobs;

/// <summary>Gross yearly salary band, in whole currency units.</summary>
public sealed class SalaryRange
{
    private SalaryRange()
    {
        Currency = null!;
    }

    public SalaryRange(int min, int max, string currency)
    {
        if (min < 0)
        {
            throw new DomainValidationException(nameof(min), "Salary cannot be negative.");
        }

        if (max < min)
        {
            throw new DomainValidationException(nameof(max), "Maximum salary must be greater than or equal to the minimum.");
        }

        var code = Guard.NotBlank(currency, nameof(currency), 3).ToUpperInvariant();
        if (code.Length != 3)
        {
            throw new DomainValidationException(nameof(currency), "Currency must be a 3-letter ISO 4217 code.");
        }

        Min = min;
        Max = max;
        Currency = code;
    }

    public int Min { get; private set; }

    public int Max { get; private set; }

    public string Currency { get; private set; }
}
