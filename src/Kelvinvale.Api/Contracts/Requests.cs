namespace Kelvinvale.Api.Contracts;

// Non-nullable string parameters are implicitly required: [ApiController] returns a 400
// ValidationProblemDetails when they are missing from the request body.

public sealed record CreateCustomerRequest(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Email,
    string Address);

public sealed record UpdateCustomerRequest(
    string FirstName,
    string LastName,
    string Email,
    string Address);

public sealed record OpenProductRequest(string Type);

public sealed record PlaceInstructionRequest(
    string Type,
    long AmountPence,
    string FundCode,
    string ClientReference);
