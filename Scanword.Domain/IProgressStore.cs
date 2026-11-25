using System.Threading.Tasks;

namespace Scanword.Domain;

public interface IProgressStore
{
    Task<LevelProgress?> LoadAsync(string levelId);
    Task SaveAsync(LevelProgress progress);
}