using System.Threading.Tasks;

namespace Scanword.Application;

public interface IPurchaseStore
{
    Task<bool> TryRecordPurchaseAsync(string productId, string purchaseToken, string? orderId);
}