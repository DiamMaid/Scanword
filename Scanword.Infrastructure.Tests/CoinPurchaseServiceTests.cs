using Scanword.Application;
using Scanword.Domain;
using Scanword.Infrastructure.Persistence;

namespace Scanword.Infrastructure.Tests;

public sealed class CoinPurchaseServiceTests : IDisposable
{
    private readonly string _dbPath;

    public CoinPurchaseServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"scanword_purchase_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task Two_redemptions_of_same_token_credit_coins_only_once()
    {
        const int defaultBalance = 0;
        const int coinsPerPurchase = 100;

        var progressStore = new SqliteProgressStore(_dbPath);
        var paymentService = new TestPaymentService(reuseToken: true);
        var walletService = new HintWalletService(progressStore, defaultBalance);
        var purchaseService = new CoinPurchaseService(paymentService, progressStore, walletService, coinsPerPurchase);

        var (result1, balance1) = await purchaseService.PurchaseHintsAsync("pack_100");
        Assert.Equal(PurchaseResult.Success, result1);
        Assert.Equal(coinsPerPurchase, balance1);

        var (result2, balance2) = await purchaseService.PurchaseHintsAsync("pack_100");
        Assert.Equal(PurchaseResult.AlreadyOwned, result2);
        Assert.Equal(coinsPerPurchase, balance2);
    }

    [Fact]
    public async Task Different_tokens_each_credit_coins()
    {
        const int defaultBalance = 0;
        const int coinsPerPurchase = 100;

        var progressStore = new SqliteProgressStore(_dbPath);
        var paymentService = new TestPaymentService(reuseToken: false);
        var walletService = new HintWalletService(progressStore, defaultBalance);
        var purchaseService = new CoinPurchaseService(paymentService, progressStore, walletService, coinsPerPurchase);

        var (result1, balance1) = await purchaseService.PurchaseHintsAsync("pack_100");
        Assert.Equal(PurchaseResult.Success, result1);
        Assert.Equal(100, balance1);

        var (result2, balance2) = await purchaseService.PurchaseHintsAsync("pack_100");
        Assert.Equal(PurchaseResult.Success, result2);
        Assert.Equal(200, balance2);
    }

    [Fact]
    public async Task Wallet_balance_persists_across_service_instances()
    {
        const int defaultBalance = 0;
        const int coinsPerPurchase = 50;

        var progressStore1 = new SqliteProgressStore(_dbPath);
        var wallet1 = new HintWalletService(progressStore1, defaultBalance);
        var purchase1 = new CoinPurchaseService(
            new TestPaymentService(reuseToken: false), progressStore1, wallet1, coinsPerPurchase);
        await purchase1.PurchaseHintsAsync("pack_50");

        var progressStore2 = new SqliteProgressStore(_dbPath);
        var wallet2 = new HintWalletService(progressStore2, defaultBalance);
        int balance = await wallet2.GetBalanceAsync();
        Assert.Equal(50, balance);
    }
}
