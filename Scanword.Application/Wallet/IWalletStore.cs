using System.Threading.Tasks;

namespace Scanword.Application;

public interface IWalletStore
{
    Task<int> GetBalanceAsync(int defaultBalance);
    Task SaveBalanceAsync(int newBalance);
}