using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Scanword.Domain;

namespace Scanword.Infrastructure.Tests;

internal sealed class TestPaymentService : IPaymentService
{
    private readonly List<StorePurchase> _issued = new();
    private readonly bool _reuseToken;
    private StorePurchase? _fixed;

    internal TestPaymentService(bool reuseToken)
    {
        _reuseToken = reuseToken;
    }

    public Task<PurchaseOutcome> PurchaseAsync(string productId)
    {
        if (_reuseToken && _fixed is not null)
            return Task.FromResult(new PurchaseOutcome(PurchaseResult.Success, _fixed));

        var purchase = new StorePurchase(
            productId,
            $"test-token-{Guid.NewGuid():N}",
            null,
            DateTimeOffset.UtcNow);

        _issued.Add(purchase);
        if (_reuseToken)
            _fixed = purchase;

        return Task.FromResult(new PurchaseOutcome(PurchaseResult.Success, purchase));
    }

    public Task<IReadOnlyList<StorePurchase>> GetOwnedPurchasesAsync()
    {
        return Task.FromResult<IReadOnlyList<StorePurchase>>(_issued.ToArray());
    }
}
