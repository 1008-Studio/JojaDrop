namespace JojaDrop.Services;

public enum UpgradeTransactionStatus
{
    Success,
    SourceMissing,
    InventoryFull,
    InvalidTarget,
    TransactionFailed
}

public readonly record struct UpgradeTransactionResult(UpgradeTransactionStatus Status)
{
    public bool IsSuccess => Status == UpgradeTransactionStatus.Success;
}
