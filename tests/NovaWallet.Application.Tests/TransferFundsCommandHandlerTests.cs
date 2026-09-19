using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.Extensions.Logging;
using NovaWallet.Application.Abstractions;
using NovaWallet.Application.Exceptions;
using NovaWallet.Application.Wallets.Commands.TransferFunds;
using NovaWallet.Application.Wallets.Dtos;
using NovaWallet.Domain.Common;
using NovaWallet.Domain.Exceptions;
using NovaWallet.Domain.Wallets;
using NSubstitute;
using System.Security.Policy;
using System.Text.Json;
using IClock = NovaWallet.Application.Abstractions.IClock;

namespace NovaWallet.Application.Tests;

public class TransferFundsCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 17, 10, 0, 0, DateTimeKind.Utc);
    private const string Customer = "customer-1";
    private const string Key = "idem-key-1";

    private readonly IWalletRepository _wallets = Substitute.For<IWalletRepository>();
    private readonly ITransferExecutor _executor = Substitute.For<ITransferExecutor>();
    private readonly IIdempotencyStore _idempotency = Substitute.For<IIdempotencyStore>();
    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IReferenceGenerator _refs = Substitute.For<IReferenceGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly ILogger<TransferFundsCommandHandler> _logger = Substitute.For<ILogger<TransferFundsCommandHandler>>();

    private readonly Wallet _source;
    private readonly Wallet _destination;

    public TransferFundsCommandHandlerTests()
    {
        _source = Wallet.Create(Customer, Now);
        _source.Credit(Money.FromNaira(10_000));
        _destination = Wallet.Create("customer-2", Now);

        _user.CustomerId.Returns(Customer);
        _user.CorrelationId.Returns("corr-1");
        _clock.UtcNow.Returns(Now);
        _refs.NewTransferReference().Returns("NW-REF-1");

        _wallets.GetByIdAsync(_source.Id, Arg.Any<CancellationToken>())
            .Returns(_source);
        _wallets.GetByIdAsync(_destination.Id, Arg.Any<CancellationToken>())
            .Returns(_destination);

        _idempotency.TryClaimAsync(Key, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(IdempotencyClaim.Claimed());

        _executor.ExecuteAsync(Arg.Any<TransferInstruction>(), Arg.Any<CancellationToken>())
            .Returns(new TransferOutcome("NW-REF-1", Money.FromNaira(9_000), Now));
    }

    private TransferFundsCommandHandler CreateHandler() =>
        new(_wallets, _executor, _idempotency, _user, _refs, _clock, _logger);

    private TransferFundsCommand ACommandFor(long amountKobo = 100_000) =>
        new(_source.Id.Value,
            new TransferRequest(_destination.Id.Value, amountKobo, "rent"),
            Key);

    [Fact]
    public async Task A_valid_transfer_executes_and_records_the_response()
    {
        var response = await CreateHandler().Handle(ACommandFor(), CancellationToken.None);

        response.Reference.Should().Be("NW-REF-1");
        response.SourceBalanceAfterKobo.Should().Be(Money.FromNaira(9_000).Kobo);

        await _executor.Received(1)
            .ExecuteAsync(Arg.Any<TransferInstruction>(), Arg.Any<CancellationToken>());
        await _idempotency.Received(1)
            .CompleteAsync(Key, 201, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_replayed_key_returns_the_stored_response_without_moving_money()
    {
        var stored = new TransferResponse("NW-REF-1", _source.Id.Value, _destination.Id.Value,
    100_000, Money.FromNaira(9_000).Kobo, Now);

        _idempotency.TryClaimAsync(Key, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(IdempotencyClaim.AlreadyCompleted(201, JsonSerializer.Serialize(stored)));

        var response = await CreateHandler().Handle(ACommandFor(), CancellationToken.None);

        response.Should().BeEquivalentTo(stored);

        await _executor.DidNotReceive()
            .ExecuteAsync(Arg.Any<TransferInstruction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_key_reused_with_a_different_payload_is_rejected()
    {
        _idempotency.TryClaimAsync(Key, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(IdempotencyClaim.PayloadMismatch());

        var act = () => CreateHandler().Handle(ACommandFor(), CancellationToken.None);

        await act.Should().ThrowAsync<IdempotencyConflictException>();

        await _executor.DidNotReceive()
            .ExecuteAsync(Arg.Any<TransferInstruction>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_key_still_in_flight_is_rejected_rather_than_run_twice()
    {
        _idempotency.TryClaimAsync(Key, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(IdempotencyClaim.InProgress());

        var act = () => CreateHandler().Handle(ACommandFor(), CancellationToken.None);

        await act.Should().ThrowAsync<IdempotencyInProgressException>();
    }

    [Fact]
    public async Task A_failed_transfer_releases_the_key_so_it_can_be_retried()
    {
        _executor.ExecuteAsync(Arg.Any<TransferInstruction>(), Arg.Any<CancellationToken>())
            .Returns<Task<TransferOutcome>>(_ => throw new InsufficientFundsException(
                _source.Id, Money.Zero, Money.FromKobo(100_000)));

        var act = () => CreateHandler().Handle(ACommandFor(), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientFundsException>();

        await _idempotency.Received(1).ReleaseAsync(Key, Arg.Any<CancellationToken>());
        await _idempotency.DidNotReceive()
            .CompleteAsync(Key, Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_transfer_to_the_same_wallet_is_refused_before_the_key_is_claimed()
    {
        var command = new TransferFundsCommand(
            _source.Id.Value,
            new TransferRequest(_source.Id.Value, 100_000, null),
            Key);

        var act = () => CreateHandler().Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<SelfTransferException>();

        await _idempotency.DidNotReceive()
            .TryClaimAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_caller_cannot_transfer_from_a_wallet_they_do_not_own()
    {
        _user.CustomerId.Returns("someone-else");

        var act = () => CreateHandler().Handle(ACommandFor(), CancellationToken.None);

        await act.Should().ThrowAsync<WalletAccessDeniedException>();

        await _executor.DidNotReceive()
            .ExecuteAsync(Arg.Any<TransferInstruction>(), Arg.Any<CancellationToken>());
    }
}