namespace NovaWallet.Domain.Auditing;

public enum AuditEventType
{
    WalletCreated = 1,
    WalletCredited = 2,
    TransferDebited = 3,
    TransferCredited = 4
}