namespace Scanword.Domain;

public readonly record struct HintOutcome(HintResult Result, int CoinsSpent);