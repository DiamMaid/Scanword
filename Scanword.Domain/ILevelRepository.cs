using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scanword.Domain;

public interface ILevelRepository
{
    Task<ScanwordLevel?> GetByIdAsync(string id);
    Task<IReadOnlyList<LevelSummary>> GetSummariesAsync();
}