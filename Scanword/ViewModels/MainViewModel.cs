using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scanword.Application;
using Scanword.Domain;

namespace Scanword.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const int HintLetterCost = 1;
    private const int HintWordCost = 3;

    private readonly LevelLoadingService _levelLoading;
    private readonly ILevelRepository _levelRepository;
    private readonly IProgressStore _progressStore;
    private readonly HintWalletService _wallet;
    private readonly RewardedAdService _rewardedAds;
    private readonly CoinPurchaseService _purchases;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private IReadOnlyList<LevelSummary> _levels = Array.Empty<LevelSummary>();
    private int _levelIndex;
    private int _lastTappedCell = -1;

    public MainViewModel(
        LevelLoadingService levelLoading,
        ILevelRepository levelRepository,
        IProgressStore progressStore,
        HintWalletService wallet,
        RewardedAdService rewardedAds,
        CoinPurchaseService purchases,
        IReadOnlyList<CoinPack> coinPacks)
    {
        _levelLoading = levelLoading;
        _levelRepository = levelRepository;
        _progressStore = progressStore;
        _wallet = wallet;
        _rewardedAds = rewardedAds;
        _purchases = purchases;
        CoinPacks = coinPacks;
    }

    public IReadOnlyList<char> KeyboardLetters { get; } =
        "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ".ToCharArray();

    public IReadOnlyList<CoinPack> CoinPacks { get; }

    public ObservableCollection<LevelItemViewModel> LevelItems { get; } = new();

    [ObservableProperty]
    private ScanwordEngine? gameEngine;

    [ObservableProperty]
    private int revision;

    [ObservableProperty]
    private string levelTitle = string.Empty;

    [ObservableProperty]
    private string levelPosition = string.Empty;

    [ObservableProperty]
    private int coins;

    [ObservableProperty]
    private string statusText = "Загрузка…";

    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private bool isMenuOpen;

    [ObservableProperty]
    private bool isStoreOpen;

    [ObservableProperty]
    private bool isLevelSelectOpen;

    [ObservableProperty]
    private double zoom = 1.0;

    public async Task InitializeAsync()
    {
        try
        {
            _levels = await _levelRepository.GetSummariesAsync();
            Coins = await _wallet.GetBalanceAsync();

            if (_levels.Count == 0)
            {
                StatusText = "Уровни не найдены в levels.db";
                return;
            }

            StatusText = "Добро пожаловать!";
            IsMenuOpen = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка запуска: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenMenu()
    {
        IsMenuOpen = true;
    }

    [RelayCommand]
    private async Task StartGameAsync()
    {
        await LoadLevelAsync(await FindResumeIndexAsync());
        IsMenuOpen = false;
    }

    private async Task<int> FindResumeIndexAsync()
    {
        var statuses = new LevelStatus?[_levels.Count];
        for (int i = 0; i < _levels.Count; i++)
        {
            LevelProgress? progress = await _progressStore.LoadAsync(_levels[i].Id);
            statuses[i] = progress?.Status;
        }

        int inProgress = Array.IndexOf(statuses, LevelStatus.InProgress);
        if (inProgress >= 0)
            return inProgress;

        for (int i = 0; i < statuses.Length; i++)
        {
            if (statuses[i] is null or LevelStatus.NotStarted)
                return i;
        }

        return 0;
    }

    [RelayCommand]
    private void CellTapped(int cellIndex)
    {
        if (GameEngine is null)
            return;

        _lastTappedCell = cellIndex;
        GameEngine.SelectWord(cellIndex);
        AfterMove();
    }

    [RelayCommand]
    private void InputLetter(char letter)
    {
        if (GameEngine is null)
            return;

        int target = ResolveTargetCell();
        if (target < 0)
            return;

        GameEngine.SubmitLetter(target, char.ToUpperInvariant(letter));
        AfterMove();
    }

    [RelayCommand]
    private void Erase()
    {
        if (GameEngine is null)
            return;

        GameEngine.EraseAtCursor();
        AfterMove();
    }

    [RelayCommand]
    private async Task OpenLetterHintAsync()
    {
        if (GameEngine is null)
            return;

        int target = ResolveTargetCell();
        if (target < 0)
        {
            StatusText = "Сначала выберите клетку со словом";
            return;
        }

        HintOutcome outcome = GameEngine.OpenLetter(target, Coins);
        await ApplyHintAsync(outcome, "буква");
    }

    [RelayCommand]
    private async Task OpenWordHintAsync()
    {
        if (GameEngine is null)
            return;

        if (GameEngine.SelectedWordId == 0)
        {
            StatusText = "Сначала выберите слово";
            return;
        }

        HintOutcome outcome = GameEngine.OpenWord(GameEngine.SelectedWordId, Coins);
        await ApplyHintAsync(outcome, "слово");
    }

    [RelayCommand]
    private async Task WatchAdAsync()
    {
        (bool watched, int newBalance) = await _rewardedAds.ShowRewardedAdAsync();
        if (watched)
        {
            Coins = newBalance;
            StatusText = "Реклама просмотрена: +10 монет";
        }
        else
        {
            StatusText = "Реклама сейчас недоступна";
        }
    }

    [RelayCommand]
    private void OpenStore()
    {
        IsStoreOpen = true;
    }

    [RelayCommand]
    private void CloseStore()
    {
        IsStoreOpen = false;
    }

    [RelayCommand]
    private async Task BuyPackAsync(CoinPack pack)
    {
        (PurchaseResult result, int balance) = await _purchases.PurchaseHintsAsync(pack.ProductId);
        Coins = balance;
        StatusText = result switch
        {
            PurchaseResult.Success => $"Покупка выполнена: +{pack.Coins} монет",
            PurchaseResult.AlreadyOwned => "Эта покупка уже активирована",
            PurchaseResult.Cancelled => "Покупка отменена",
            _ => "Покупка не удалась, попробуйте позже"
        };
    }

    [RelayCommand]
    private async Task RestorePurchasesAsync()
    {
        (int restored, int balance) = await _purchases.RestorePurchasesAsync();
        Coins = balance;
        StatusText = restored > 0
            ? $"Восстановлено покупок: {restored}"
            : "Покупок для восстановления не найдено";
    }

    [RelayCommand]
    private async Task OpenLevelSelectAsync()
    {
        LevelItems.Clear();
        for (int i = 0; i < _levels.Count; i++)
        {
            LevelSummary summary = _levels[i];
            string status = await DescribeLevelStatusAsync(summary.Id);
            LevelItems.Add(new LevelItemViewModel(
                i,
                summary.Title,
                $"{DifficultyName(summary.Difficulty)} · слов: {summary.WordCount} · {status}"));
        }

        IsLevelSelectOpen = true;
    }

    [RelayCommand]
    private void CloseLevelSelect()
    {
        IsLevelSelectOpen = false;
    }

    [RelayCommand]
    private async Task SelectLevelAsync(int index)
    {
        await LoadLevelAsync(index);
        IsLevelSelectOpen = false;
        IsMenuOpen = false;
    }

    private async Task<string> DescribeLevelStatusAsync(string levelId)
    {
        LevelProgress? progress = await _progressStore.LoadAsync(levelId);
        if (progress is null || progress.Status == LevelStatus.NotStarted)
            return "не начат";

        int solved = progress.Words.Count(w => w.Status == WordStatus.Solved);
        return progress.Status == LevelStatus.Completed
            ? "пройден"
            : $"в игре {solved}/{progress.Words.Length}";
    }

    private static string DifficultyName(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "Лёгкий",
        Difficulty.Normal => "Средний",
        Difficulty.Hard => "Сложный",
        Difficulty.Expert => "Эксперт",
        _ => string.Empty
    };

    [RelayCommand]
    private void ResetZoom()
    {
        Zoom = 1.0;
    }

    [RelayCommand]
    private Task NextLevelAsync()
    {
        if (_levels.Count == 0)
            return Task.CompletedTask;

        return LoadLevelAsync(_levelIndex + 1 < _levels.Count ? _levelIndex + 1 : 0);
    }

    [RelayCommand]
    private Task PreviousLevelAsync()
    {
        if (_levels.Count == 0)
            return Task.CompletedTask;

        return LoadLevelAsync(_levelIndex - 1 >= 0 ? _levelIndex - 1 : _levels.Count - 1);
    }

    private async Task LoadLevelAsync(int index)
    {
        LevelSummary summary = _levels[index];
        StatusText = "Загрузка уровня…";

        ScanwordEngine? engine = await _levelLoading.LoadLevelAsync(summary.Id, HintLetterCost, HintWordCost);
        if (engine is null)
        {
            StatusText = $"Уровень «{summary.Title}» не найден в levels.db";
            return;
        }

        _levelIndex = index;
        _lastTappedCell = -1;
        GameEngine = engine;
        LevelTitle = summary.Title;
        LevelPosition = $"{index + 1} / {_levels.Count}";
        IsCompleted = engine.LevelStatus == LevelStatus.Completed;
        StatusText = IsCompleted
            ? "Уровень уже пройден — выберите следующий ▶"
            : "Нажмите на клетку слова и вводите буквы";
        Revision++;
    }

    private int ResolveTargetCell()
    {
        if (GameEngine is null)
            return -1;

        if (GameEngine.CursorCellIndex >= 0)
            return GameEngine.CursorCellIndex;

        if (_lastTappedCell >= 0 && GameEngine.Level.Cells[_lastTappedCell].Kind == CellKind.Letter)
            return _lastTappedCell;

        return -1;
    }

    private async Task ApplyHintAsync(HintOutcome outcome, string hintKind)
    {
        if (outcome.Result != HintResult.Success)
        {
            StatusText = Describe(outcome.Result);
            return;
        }

        (int newBalance, int spent) = await _wallet.SpendAsync(outcome.CoinsSpent);
        Coins = newBalance;
        StatusText = $"Подсказка «{hintKind}» открыта: −{spent} монет";
        AfterMove();
    }

    private static string Describe(HintResult result) => result switch
    {
        HintResult.InsufficientCoins => "Недостаточно монет — посмотрите рекламу",
        HintResult.CellAlreadyRevealed => "Эта буква уже открыта",
        HintResult.WordAlreadySolved => "Это слово уже разгадано",
        HintResult.InvalidCell => "Выберите клетку с буквой",
        HintResult.LevelCompleted => "Уровень уже пройден",
        _ => string.Empty
    };

    private void AfterMove()
    {
        if (GameEngine is null)
            return;

        if (GameEngine.LevelStatus == LevelStatus.Completed && !IsCompleted)
        {
            IsCompleted = true;
            _ = GrantCompletionRewardAsync();
        }

        Revision++;
        QueueSave();
    }

    private async Task GrantCompletionRewardAsync()
    {
        if (GameEngine is null)
            return;

        int reward = RewardFor(GameEngine.Level.Meta.Difficulty);
        Coins = await _wallet.CreditAsync(reward);
        StatusText = $"🎉 Уровень пройден! Награда: +{reward} монет";
    }

    private static int RewardFor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 10,
        Difficulty.Normal => 15,
        Difficulty.Hard => 20,
        Difficulty.Expert => 25,
        _ => 10
    };

    private void QueueSave()
    {
        if (GameEngine is null)
            return;

        LevelProgress snapshot = GameEngine.CreateProgressSnapshot();
        _ = SaveAsync(snapshot);
    }

    private async Task SaveAsync(LevelProgress snapshot)
    {
        await _saveLock.WaitAsync();
        try
        {
            await _progressStore.SaveAsync(snapshot);
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
