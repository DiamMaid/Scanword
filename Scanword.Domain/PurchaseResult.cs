namespace Scanword.Domain;

public enum PurchaseResult
{
    Success,
    AlreadyOwned,
    Cancelled,
    Failed,
    NetworkError
}