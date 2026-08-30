using Kelvinvale.Core.Abstractions;

namespace Kelvinvale.Api.Contracts;

public static class RequestParsing
{
    /// <summary>Parses a caller-supplied enum value (case-insensitive), raising a 400 problem with <paramref name="code"/> on a bad value.</summary>
    public static TEnum ParseEnum<TEnum>(string value, string code, string noun)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        var allowed = string.Join(", ", Enum.GetNames<TEnum>());
        throw new DomainException(
            new DomainError(code, $"'{value}' is not a valid {noun}. Expected one of: {allowed}."),
            statusCode: 400);
    }
}
