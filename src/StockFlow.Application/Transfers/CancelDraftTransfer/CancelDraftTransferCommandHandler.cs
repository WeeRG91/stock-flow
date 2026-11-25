using StockFlow.Application.Abstractions;
using StockFlow.Application.Abstractions.Notifications;
using StockFlow.Application.Abstractions.Persistence;
using StockFlow.Application.Abstractions.Time;
using StockFlow.Application.Authorization;
using StockFlow.Application.Exceptions;
using StockFlow.Domain.Enums;

namespace StockFlow.Application.Transfers.CancelDraftTransfer
{
    public sealed class CancelDraftTransferCommandHandler
    {
        private readonly ITransferRepository _transfers;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;
        private readonly ITransferChangeNotifier _notifier;

        public CancelDraftTransferCommandHandler(
            ITransferRepository transfers,
            ICurrentUser currentUser,
            IClock clock,
            ITransferChangeNotifier notifier)
        {
            ArgumentNullException.ThrowIfNull(transfers);
            ArgumentNullException.ThrowIfNull(currentUser);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(notifier);

            _transfers = transfers;
            _currentUser = currentUser;
            _clock = clock;
            _notifier = notifier;
        }

        public async Task HandleAsync(
            CancelDraftTransferCommand command,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();

            var actorId = _currentUser.UserId;

            if (actorId is null || actorId == Guid.Empty)
            {
                throw new AuthenticationRequiredException();
            }

            if (!await _currentUser.HasPermissionAsync(
                StockFlowPolicies.TransfersManage, cancellationToken))
            {
                throw new PermissionDeniedException();
            }

            if (command.TransferId == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.TransferId),
                    "Transfer ID must not be empty.");
            }

            if (command.ExpectedVersion == Guid.Empty)
            {
                throw new RequestValidationException(
                    nameof(command.ExpectedVersion),
                    "Expected version must not be empty.");
            }

            var (transfer, version) = await _transfers.GetByIdAsync(
            command.TransferId, cancellationToken)
            ?? throw new ResourceNotFoundException(
                "Transfer", command.TransferId);

            if (version != command.ExpectedVersion)
            {
                throw new TransferConflictException(
                    transfer.Id,
                    "The transfer has changed. Reload it and try again.");
            }

            if (transfer.Status != TransferStatus.Draft)
            {
                throw new BusinessRuleException(
                    "Only draft transfers can be cancelled.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            transfer.Cancel(_clock.UtcNow, actorId.Value);

            await _transfers.SaveCancellationAsync(
                transfer, cancellationToken);

            await _notifier.NotifyAsync(
                new TransferChanged(transfer.Id),
                cancellationToken);
        }
    }
}
