namespace NovaWallet.Infrastructure.Authentication;

public interface ITokenIssuer
{
    IssuedToken Issue(string customerId);
}

public sealed record IssuedToken(string AccessToken, string TokenType, int ExpiresInSeconds);