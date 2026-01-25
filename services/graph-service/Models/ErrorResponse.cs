namespace GraphService.Models;

public sealed record ErrorDetails(string Code, string Message);

public sealed record ErrorResponse(ErrorDetails Error);
