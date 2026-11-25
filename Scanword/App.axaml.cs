using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Scanword.Application;
using Scanword.Infrastructure.Persistence;
using Scanword.Infrastructure.Services;
using Scanword.ViewModels;
using Scanword.Views;

namespace Scanword;

public partial class App : Avalonia.Application
{
    private const int DefaultWalletBalance = 30;
    private const int CoinsPerAd = 10;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        MainViewModel viewModel = CreateMainViewModel();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime singleViewFactoryApplicationLifetime)
        {
            singleViewFactoryApplicationLifetime.MainViewFactory = () => new MainView { DataContext = viewModel };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = viewModel
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static MainViewModel CreateMainViewModel()
    {
        string dataDirectory = AppServices.GetDataDirectory();
        string levelsPath = AppServices.EnsureLevelsDatabase(dataDirectory);
        string progressPath = Path.Combine(dataDirectory, "progress.db");

        var levelRepository = new SqliteLevelRepository(levelsPath);
        var progressStore = new SqliteProgressStore(progressPath);
        var levelLoading = new LevelLoadingService(levelRepository, progressStore);
        var wallet = new HintWalletService(progressStore, DefaultWalletBalance);
        var rewardedAds = new RewardedAdService(new StubAdService(), wallet, CoinsPerAd);

        CoinPack[] coinPacks =
        [
            new CoinPack("coins_50", "50 монет", 50),
            new CoinPack("coins_150", "150 монет", 150),
            new CoinPack("coins_400", "400 монет", 400)
        ];
        var purchases = new CoinPurchaseService(new StubPaymentService(), progressStore, wallet, coinPacks);

        var viewModel = new MainViewModel(
            levelLoading, levelRepository, progressStore, wallet, rewardedAds, purchases, coinPacks);
        _ = viewModel.InitializeAsync();
        return viewModel;
    }
}
