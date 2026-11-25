using System.Threading.Tasks;
using Scanword.Domain;

namespace Scanword.Application;

public sealed class CoinPurchaseService
{
    private readonly IPaymentService _paymentService;
    private readonly IPurchaseStore _purchaseStore;
    private readonly HintWalletService _walletService;
    private readonly int _coinsPerPurchase;

    public CoinPurchaseService(
        IPaymentService paymentService,
        IPurchaseStore purchaseStore,
        HintWalletService walletService,
        int coinsPerPurchase)
    {
        _paymentService = paymentService;
        _purchaseStore = purchaseStore;
        _walletService = walletService;
        _coinsPerPurchase = coinsPerPurchase;
    }

    public async Task<(PurchaseResult Result, int NewBalance)> PurchaseHintsAsync(string productId)
    {
        PurchaseOutcome outcome = await _paymentService.PurchaseAsync(productId);

        if (outcome.Result != PurchaseResult.Success || outcome.Purchase is null)
            return (outcome.Result, await _walletService.GetBalanceAsync());

        StorePurchase purchase = outcome.Purchase.Value;
        bool isNewPurchase = await _purchaseStore.TryRecordPurchaseAsync(
            purchase.ProductId, purchase.PurchaseToken, purchase.OrderId);

        if (!isNewPurchase)
            return (PurchaseResult.AlreadyOwned, await _walletService.GetBalanceAsync());

        int newBalance = await _walletService.CreditAsync(_coinsPerPurchase);
        return (PurchaseResult.Success, newBalance);
    }
}