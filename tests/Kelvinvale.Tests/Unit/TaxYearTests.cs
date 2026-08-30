using Kelvinvale.Core.Domain;

namespace Kelvinvale.Tests.Unit;

public sealed class TaxYearTests
{
    [Theory]
    [InlineData("2026-04-05", 2025)] // last day of 2025/26
    [InlineData("2026-04-06", 2026)] // first day of 2026/27
    [InlineData("2026-08-30", 2026)]
    [InlineData("2026-12-31", 2026)]
    [InlineData("2027-01-01", 2026)]
    [InlineData("2027-04-05", 2026)]
    [InlineData("2027-04-06", 2027)]
    public void Containing_places_a_date_in_the_right_tax_year(string date, int expectedStartYear)
    {
        var taxYear = TaxYear.Containing(DateOnly.Parse(date));

        Assert.Equal(expectedStartYear, taxYear.StartYear);
    }

    [Theory]
    [InlineData(2026, "2026/27")]
    [InlineData(2009, "2009/10")]
    [InlineData(1999, "1999/00")]
    public void Label_is_the_hmrc_style_span(int startYear, string expected)
    {
        Assert.Equal(expected, TaxYear.FromStartYear(startYear).Label);
    }

    [Fact]
    public void Start_and_end_are_6_april_to_5_april()
    {
        var taxYear = TaxYear.FromStartYear(2026);

        Assert.Equal(new DateOnly(2026, 4, 6), taxYear.Start);
        Assert.Equal(new DateOnly(2027, 4, 5), taxYear.End);
    }

    [Fact]
    public void The_annual_allowance_is_twenty_thousand_pounds()
    {
        Assert.Equal(2_000_000, TaxYear.FromStartYear(2026).AllowancePence);
    }

    [Fact]
    public void Parse_round_trips_a_label()
    {
        Assert.Equal(2026, TaxYear.Parse("2026/27").StartYear);
    }

    [Fact]
    public void Two_tax_years_with_the_same_start_year_are_equal()
    {
        Assert.Equal(TaxYear.FromStartYear(2026), TaxYear.Containing(new DateOnly(2026, 9, 1)));
    }
}
