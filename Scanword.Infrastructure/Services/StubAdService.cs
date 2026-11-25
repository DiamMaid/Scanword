using System.Threading.Tasks;
using Scanword.Domain;

namespace Scanword.Infrastructure.Services;

public sealed class StubAdService : IAdService
{
    public Task<bool> ShowRewardedAdAsync()
    {
        return Task.FromResult(true);
    }
}