using Scanword.Application;
using Scanword.Domain;
using Scanword.Infrastructure.Persistence;

namespace Scanword.Infrastructure.Tests;

public sealed class CoinPurchaseServiceTests : IDisposable
{
    private static readonly CoinPack[] Catalog =
    [
        new CoinPack("pack_100", "100 монет", 100),
        new CoinPack("pack_50", "50 монет", 50)
    ];

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
        var progressStore = new SqliteProgressStore(_dbPath);
        var paymentService = new TestPaymentService(reuseToken: true);
        var walletService = new HintWalletService(progressStore, 0);
        var purchaseService = new CoinPurchaseService(paymentService, progressStore, walletService, Catalog);

        var (result1, balance1) = await purchaseService.PurchaseHintsAsync("pack_100");
        Assert.Equal(PurchaseResult.Success, result1);
        Assert.Equal(100, balance1);

        var (result2, balance2) = await purchaseService.PurchaseHintsAsync("pack_100");
        Assert.Equal(PurchaseResult.AlreadyOwned, result2);
        Assert.Equal(100, balance2);
    }

    [Fact]
    public async Task Different_tokens_each_credit_coins()
    {
        var progressStore = new SqliteProgressStore(_dbPath);
        var paymentService = new TestPaymentService(reuseToken: false);
        var walletService = new HintWalletService(progressStore, 0);
        var purchaseService = new CoinPurchaseService(paymentService, progressStore, walletService, Catalog);

        var (result1, balance1) = await purchaseService.PurchaseHintsAsync("pack_100");
        Assert.Equal(PurchaseResult.Success, result1);
        Assert.Equal(100, balance1);

        var (result2, balance2) = await purchaseService.PurchaseHintsAsync("pack_100");
        Assert.Equal(PurchaseResult.Success, result2);
        Assert.Equal(200, balance2);
    }

    [Fact]
    public async Task Unknown_product_fails_without_charging()
    {
        var progressStore = new SqliteProgressStore(_dbPath);
        var paymentService = new TestPaymentService(reuseToken: false);
        var walletService = new HintWalletService(progressStore, 10);
        var purchaseService = new CoinPurchaseService(paymentService, progressStore, walletService, Catalog);

        var (result, balance) = await purchaseService.PurchaseHintsAsync("pack_999");
        Assert.Equal(PurchaseResult.Failed, result);
        Assert.Equal(10, balance);
    }

    [Fact]
    public async Task Restore_credits_purchases_not_recorded_locally()
    {
        var progressStore = new SqliteProgressStore(_dbPath);
        var paymentService = new TestPaymentService(reuseToken: false);
        paymentService.SeedOwned("pack_100", "restore-token-1");
        paymentService.SeedOwned("pack_50", "restore-token-2");
        var walletService = new HintWalletService(progressStore, 0);
        var purchaseService = new CoinPurchaseService(paymentService, progressStore, walletService, Catalog);

        var (restored, balance) = await purchaseService.RestorePurchasesAsync();
        Assert.Equal(2, restored);
        Assert.Equal(150, balance);

        var (restoredAgain, balanceAgain) = await purchaseService.RestorePurchasesAsync();
        Assert.Equal(0, restoredAgain);
        Assert.Equal(150, balanceAgain);
    }

    [Fact]
    public async Task Restore_after_local_purchase_does_not_double_credit()
    {
        var progressStore = new SqliteProgressStore(_dbPath);
        var paymentService = new TestPaymentService(reuseToken: true);
        var walletService = new HintWalletService(progressStore, 0);
        var purchaseService = new CoinPurchaseService(paymentService, progressStore, walletService, Catalog);

        var (result, balance) = await purchaseService.PurchaseHintsAsync("pack_100");
        Assert.Equal(PurchaseResult.Success, result);
        Assert.Equal(100, balance);

        var (restored, balanceAfterRestore) = await purchaseService.RestorePurchasesAsync();
        Assert.Equal(0, restored);
        Assert.Equal(100, balanceAfterRestore);
    }

    [Fact]
    public async Task Wallet_balance_persists_across_service_instances()
    {
        var progressStore1 = new SqliteProgressStore(_dbPath);
        var wallet1 = new HintWalletService(progressStore1, 0);
        var purchase1 = new CoinPurchaseService(
            new TestPaymentService(reuseToken: false), progressStore1, wallet1, Catalog);
        await purchase1.PurchaseHintsAsync("pack_50");

        var progressStore2 = new SqliteProgressStore(_dbPath);
        var wallet2 = new HintWalletService(progressStore2, 0);
        int balance = await wallet2.GetBalanceAsync();
        Assert.Equal(50, balance);
    }
}
