using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scanword.Domain;

public interface IPaymentService
{
    Task<PurchaseOutcome> PurchaseAsync(string productId);

    Task<IReadOnlyList<StorePurchase>> GetOwnedPurchasesAsync();
}