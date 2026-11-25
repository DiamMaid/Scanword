using System;
using System.Collections.Generic;

namespace Scanword.Domain;

public sealed record LevelValidationResult(bool IsValid, IReadOnlyList<InvariantViolation> Violations)
{
    public static LevelValidationResult Success { get; } = new(true, Array.Empty<InvariantViolation>());
}