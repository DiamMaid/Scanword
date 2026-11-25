using System.Threading.Tasks;
using Scanword.Domain;

namespace Scanword.Application;

public sealed class RewardedAdService
{
    private readonly IAdService _adService;
    private readonly HintWalletService _walletService;
    private readonly int _coinsPerAd;

    public RewardedAdService(IAdService adService, HintWalletService walletService, int coinsPerAd)
    {
        _adService = adService;
        _walletService = walletService;
        _coinsPerAd = coinsPerAd;
    }

    public async Task<(bool AdWatched, int NewBalance)> ShowRewardedAdAsync()
    {
        bool watched = await _adService.ShowRewardedAdAsync();

        if (!watched)
            return (false, await _walletService.GetBalanceAsync());

        int newBalance = await _walletService.CreditAsync(_coinsPerAd);
        return (true, newBalance);
    }
}