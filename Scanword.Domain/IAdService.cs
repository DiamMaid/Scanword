using System.Threading.Tasks;

namespace Scanword.Domain;

public interface IAdService
{
    Task<bool> ShowRewardedAdAsync();
}