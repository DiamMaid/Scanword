using System;

namespace Scanword.Domain;

public readonly record struct StorePurchase(
    string ProductId,
    string PurchaseToken,
    string? OrderId,
    DateTimeOffset PurchasedAt);
