using System;
using System.Collections.Generic;
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
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private IReadOnlyList<LevelSummary> _levels = Array.Empty<LevelSummary>();
    private int _levelIndex;
    private int _lastTappedCell = -1;

    public MainViewModel(
        LevelLoadingService levelLoading,
        ILevelRepository levelRepository,
        IProgressStore progressStore,
        HintWalletService wallet,
        RewardedAdService rewardedAds)
    {
        _levelLoading = levelLoading;
        _levelRepository = levelRepository;
        _progressStore = progressStore;
        _wallet = wallet;
        _rewardedAds = rewardedAds;
    }

    public IReadOnlyList<char> KeyboardLetters { get; } =
        "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ".ToCharArray();

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

            await LoadLevelAsync(0);
        }
        catch (Exception ex)
        {
            StatusText = $"Ошибка запуска: {ex.Message}";
        }
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
            StatusText = "Уровень пройден! Нажмите ▶, чтобы перейти дальше";
        }

        Revision++;
        QueueSave();
    }

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
