namespace NovaWallet.Application.Wallets.Dtos;

/// <summary>
/// Opens a wallet. The customer id comes from the bearer token, not the body,
/// so a caller cannot create a wallet for someone else.
/// </summary>
public sealed record CreateWalletRequest;