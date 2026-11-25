namespace Scanword.Domain;

public readonly record struct PurchaseOutcome(PurchaseResult Result, StorePurchase? Purchase);
