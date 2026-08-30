namespace Kelvinvale.Core.Domain;

/// <summary>
/// A UK tax year, which runs from 6 April to 5 April. Identified by its start calendar year
/// (e.g. <c>StartYear = 2026</c> is the "2026/27" tax year: 6 Apr 2026 to 5 Apr 2027).
/// Persisted as the start year (an <see cref="int"/>) via an EF value converter.
/// </summary>
public readonly record struct TaxYear : IComparable<TaxYear>
{
    /// <summary>UK ISA annual subscription allowance in pence, by tax year start year.</summary>
    private static readonly IReadOnlyDictionary<int, long> AllowancesPence = new Dictionary<int, long>
    {
        [2022] = 2_000_000, // £20,000
        [2023] = 2_000_000,
        [2024] = 2_000_000,
        [2025] = 2_000_000,
        [2026] = 2_000_000,
    };

    /// <summary>Allowance used when a year is not in the table. Kept explicit so a missing year fails safe, not open.</summary>
    private const long DefaultAllowancePence = 2_000_000;

    public int StartYear { get; }

    private TaxYear(int startYear) => StartYear = startYear;

    public static TaxYear FromStartYear(int startYear) => new(startYear);

    /// <summary>The tax year that contains <paramref name="date"/>. 6 April opens a new tax year.</summary>
    public static TaxYear Containing(DateOnly date)
    {
        var firstDayOfNewYear = new DateOnly(date.Year, 4, 6);
        var startYear = date >= firstDayOfNewYear ? date.Year : date.Year - 1;
        return new TaxYear(startYear);
    }

    /// <summary>Parses a "2026/27" style label.</summary>
    public static TaxYear Parse(string label)
    {
        var slash = label.IndexOf('/');
        if (slash <= 0 || !int.TryParse(label[..slash], out var startYear))
        {
            throw new FormatException($"'{label}' is not a valid tax year label (expected e.g. '2026/27').");
        }

        return new TaxYear(startYear);
    }

    public DateOnly Start => new(StartYear, 4, 6);

    public DateOnly End => new(StartYear + 1, 4, 5);

    public long AllowancePence =>
        AllowancesPence.TryGetValue(StartYear, out var pence) ? pence : DefaultAllowancePence;

    public string Label => $"{StartYear}/{(StartYear + 1) % 100:D2}";

    public int CompareTo(TaxYear other) => StartYear.CompareTo(other.StartYear);

    public override string ToString() => Label;
}
