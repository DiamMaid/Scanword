using System.Threading.Tasks;
using Scanword.Domain;

namespace Scanword.Application;

public sealed class LevelLoadingService
{
    private readonly ILevelRepository _levelRepo;
    private readonly IProgressStore _progressStore;

    public LevelLoadingService(ILevelRepository levelRepo, IProgressStore progressStore)
    {
        _levelRepo = levelRepo;
        _progressStore = progressStore;
    }

    public async Task<ScanwordEngine?> LoadLevelAsync(string levelId, int hintCostPerLetter = 1, int hintCostPerWord = 3)
    {
        ScanwordLevel? level = await _levelRepo.GetByIdAsync(levelId);
        if (level == null)
            return null;

        LevelProgress? progress = await _progressStore.LoadAsync(levelId);

        if (progress != null && progress.LevelVersion == level.Version)
            return ScanwordEngine.CreateFromProgress(level, progress);

        return new ScanwordEngine(level, hintCostPerLetter, hintCostPerWord);
    }
}