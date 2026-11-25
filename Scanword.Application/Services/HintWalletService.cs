using System;
using System.Threading.Tasks;

namespace Scanword.Application;

public sealed class HintWalletService
{
    private readonly IWalletStore _walletStore;
    private readonly int _defaultBalance;

    public HintWalletService(IWalletStore walletStore, int defaultBalance)
    {
        _walletStore = walletStore;
        _defaultBalance = defaultBalance;
    }

    public Task<int> GetBalanceAsync()
    {
        return _walletStore.GetBalanceAsync(_defaultBalance);
    }

    public async Task<(int NewBalance, int CoinsSpent)> SpendAsync(int coinsToSpend)
    {
        int currentBalance = await _walletStore.GetBalanceAsync(_defaultBalance);

        int actualSpend = Math.Min(coinsToSpend, currentBalance);
        if (actualSpend <= 0)
            return (currentBalance, 0);

        int newBalance = currentBalance - actualSpend;
        await _walletStore.SaveBalanceAsync(newBalance);
        return (newBalance, actualSpend);
    }

    public async Task<int> CreditAsync(int coins)
    {
        if (coins < 0)
            throw new ArgumentOutOfRangeException(nameof(coins));

        int currentBalance = await _walletStore.GetBalanceAsync(_defaultBalance);
        int newBalance = currentBalance + coins;
        await _walletStore.SaveBalanceAsync(newBalance);
        return newBalance;
    }
}