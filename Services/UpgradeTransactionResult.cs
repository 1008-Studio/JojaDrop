namespace JojaDrop.Services;

public enum UpgradeTransactionStatus
{
    Success,
    SourceMissing,
    InvalidQuantity,
    InsufficientQuantity,
    InventoryFull,
    InvalidTarget,
    TransactionFailed
}

public readonly record struct UpgradeTransactionResult(UpgradeTransactionStatus Status)
{
    public bool IsSuccess => Status == UpgradeTransactionStatus.Success;
}
