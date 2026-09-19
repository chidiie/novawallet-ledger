namespace NovaWallet.Application.Exceptions;

/// <summary>Base for orchestration failures the API maps to specific status codes.</summary>
public abstract class ApplicationRuleException : Exception
{
    protected ApplicationRuleException(string message) : base(message) { }
}

/// <summary>The caller does not own the wallet they are acting on.</summary>
public sealed class WalletAccessDeniedException : ApplicationRuleException
{
    public WalletAccessDeniedException()
        : base("You do not have access to this wallet.") { }
}

/// <summary>Source and destination are the same wallet.</summary>
public sealed class SelfTransferException : ApplicationRuleException
{
    public SelfTransferException()
        : base("A wallet cannot transfer to itself.") { }
}

/// <summary>The idempotency key was reused with a different request body.</summary>
public sealed class IdempotencyConflictException : ApplicationRuleException
{
    public IdempotencyConflictException(string key)
        : base($"Idempotency key '{key}' was already used with a different request.") { }
}

/// <summary>The same key is still being processed by an earlier request.</summary>
public sealed class IdempotencyInProgressException : ApplicationRuleException
{
    public IdempotencyInProgressException(string key)
        : base($"A request with idempotency key '{key}' is still in progress. Retry shortly.") { }
}