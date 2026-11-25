using System.Collections.Generic;
using System.Threading.Tasks;
using Scanword.Domain;

namespace Scanword.Application;

public sealed class CoinPurchaseService
{
    private readonly IPaymentService _paymentService;
    private readonly IPurchaseStore _purchaseStore;
    private readonly HintWalletService _walletService;
    private readonly IReadOnlyList<CoinPack> _catalog;

    public CoinPurchaseService(
        IPaymentService paymentService,
        IPurchaseStore purchaseStore,
        HintWalletService walletService,
        IReadOnlyList<CoinPack> catalog)
    {
        _paymentService = paymentService;
        _purchaseStore = purchaseStore;
        _walletService = walletService;
        _catalog = catalog;
    }

    public async Task<(PurchaseResult Result, int NewBalance)> PurchaseHintsAsync(string productId)
    {
        CoinPack? pack = FindPack(productId);
        if (pack is null)
            return (PurchaseResult.Failed, await _walletService.GetBalanceAsync());

        PurchaseOutcome outcome = await _paymentService.PurchaseAsync(productId);

        if (outcome.Result != PurchaseResult.Success || outcome.Purchase is null)
            return (outcome.Result, await _walletService.GetBalanceAsync());

        StorePurchase purchase = outcome.Purchase.Value;
        bool isNewPurchase = await _purchaseStore.TryRecordPurchaseAsync(
            purchase.ProductId, purchase.PurchaseToken, purchase.OrderId);

        if (!isNewPurchase)
            return (PurchaseResult.AlreadyOwned, await _walletService.GetBalanceAsync());

        int newBalance = await _walletService.CreditAsync(pack.Coins);
        return (PurchaseResult.Success, newBalance);
    }

    public async Task<(int RestoredPurchases, int NewBalance)> RestorePurchasesAsync()
    {
        IReadOnlyList<StorePurchase> owned = await _paymentService.GetOwnedPurchasesAsync();

        int restored = 0;
        foreach (StorePurchase purchase in owned)
        {
            CoinPack? pack = FindPack(purchase.ProductId);
            if (pack is null)
                continue;

            bool isNewPurchase = await _purchaseStore.TryRecordPurchaseAsync(
                purchase.ProductId, purchase.PurchaseToken, purchase.OrderId);

            if (!isNewPurchase)
                continue;

            restored++;
            await _walletService.CreditAsync(pack.Coins);
        }

        return (restored, await _walletService.GetBalanceAsync());
    }

    private CoinPack? FindPack(string productId)
    {
        foreach (CoinPack pack in _catalog)
        {
            if (pack.ProductId == productId)
                return pack;
        }

        return null;
    }
}
