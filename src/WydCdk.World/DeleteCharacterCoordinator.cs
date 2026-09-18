namespace WydCdk.World;

public enum DeleteCharacterStatus
{
    Success,
    SlotOutOfRange,
    SecureNotVerified,
    AccountNotFound,
    WrongPassword,
    NotAvailable,
}

public sealed record DeleteCharacterOutcome(DeleteCharacterStatus Status, LegacyAccountSnapshot? Snapshot)
{
    public bool IsSuccess => Status == DeleteCharacterStatus.Success;
}

/// <summary>Coordinates the legacy TMSrv/DBSrv character-delete flow.</summary>
public sealed class DeleteCharacterCoordinator(ICharacterStore accounts)
{
    public async ValueTask<DeleteCharacterOutcome> HandleAsync(string accountName, WydCdk.Protocol.DeleteCharacterRequest request, bool secureVerified, CancellationToken cancellationToken = default)
    {
        if (request.Slot is < 0 or >= LegacyAccountSnapshot.CharacterCount) return new(DeleteCharacterStatus.SlotOutOfRange, null);
        if (!secureVerified) return new(DeleteCharacterStatus.SecureNotVerified, null);

        var result = await accounts.TryDeleteCharacterAsync(accountName, request.Slot, request.AccountPassword, cancellationToken);
        return result switch
        {
            DeleteCharacterStoreResult.AccountNotFound => new(DeleteCharacterStatus.AccountNotFound, null),
            DeleteCharacterStoreResult.WrongPassword => new(DeleteCharacterStatus.WrongPassword, null),
            DeleteCharacterStoreResult.NotAvailable => new(DeleteCharacterStatus.NotAvailable, null),
            _ => new(DeleteCharacterStatus.Success, await accounts.ReadSnapshotAsync(accountName, cancellationToken)),
        };
    }
}
