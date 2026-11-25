namespace Scanword.Domain;

public sealed record InvariantViolation(int InvariantNumber, string Message);