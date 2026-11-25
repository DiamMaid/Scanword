using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Scanword.Domain;

namespace Scanword.Infrastructure.Services;

public sealed class StubPaymentService : IPaymentService
{
    private readonly List<StorePurchase> _issued = new();

    public Task<PurchaseOutcome> PurchaseAsync(string productId)
    {
        var purchase = new StorePurchase(
            productId,
            $"stub-token-{Guid.NewGuid():N}",
            null,
            DateTimeOffset.UtcNow);

        _issued.Add(purchase);
        return Task.FromResult(new PurchaseOutcome(PurchaseResult.Success, purchase));
    }

    public Task<IReadOnlyList<StorePurchase>> GetOwnedPurchasesAsync()
    {
        return Task.FromResult<IReadOnlyList<StorePurchase>>(_issued.ToArray());
    }
}
