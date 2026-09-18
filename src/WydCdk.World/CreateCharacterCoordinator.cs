using WydCdk.Protocol;

namespace WydCdk.World;

public enum CreateCharacterStatus
{
    Success,
    SlotOutOfRange,
    ClassOutOfRange,
    SecureNotVerified,
    InvalidName,
    SlotOccupied,
    NameTaken,
    TemplateUnavailable,
    AccountNotFound,
}

/// <summary>Outcome of a character-creation attempt; carries the fresh selection snapshot only on success.</summary>
public sealed record CreateCharacterOutcome(CreateCharacterStatus Status, LegacyCharacterSelection? Characters)
{
    public bool IsSuccess => Status == CreateCharacterStatus.Success;
}

/// <summary>
/// Equivalent of TMSrv's <c>Exec_MSG_CreateCharacter</c> followed by <c>CFileDB::_MSG_DBCreateCharacter</c>
/// (CFileDB.cpp:866-1034), merged into one step since this port has no separate DBSrv process. The checks below
/// run in the same order as the original: slot range, class range, SecurePass/PIN verification, name policy,
/// slot-occupied, then the global name reservation - only then is the character actually written. The
/// <c>pUser[conn].Mode == USER_SELCHAR</c> precondition is the caller's responsibility, not this coordinator's.
/// <see cref="CreateCharacterStatus.SecureNotVerified"/> matches CFileDB.cpp:892-899's silent <c>break</c> (no
/// <c>SendDBSignal</c> call, unlike every other rejection here) - callers must send nothing back for it, exactly
/// as for the character-login rejections in <see cref="CharacterLoginCoordinator"/>.
/// </summary>
public sealed class CreateCharacterCoordinator(ICharacterStore accounts, LegacyCharacterTemplateStore templates)
{
    public async ValueTask<CreateCharacterOutcome> HandleAsync(string accountName, CreateCharacterRequest request, bool secureVerified, CancellationToken cancellationToken = default)
    {
        if (request.Slot < 0 || request.Slot >= LegacyCharacterSelection.CharacterCount)
            return new(CreateCharacterStatus.SlotOutOfRange, null);

        if (request.CharacterClass < 0 || request.CharacterClass >= LegacyCharacterTemplateStore.ClassCount)
            return new(CreateCharacterStatus.ClassOutOfRange, null);

        if (!secureVerified)
            return new(CreateCharacterStatus.SecureNotVerified, null);

        if (!LegacyCharacterName.IsValid(request.CharacterName))
            return new(CreateCharacterStatus.InvalidName, null);

        var slotEmpty = await accounts.IsCharacterSlotEmptyAsync(accountName, request.Slot, cancellationToken);
        if (slotEmpty is null) return new(CreateCharacterStatus.AccountNotFound, null);
        if (slotEmpty is false) return new(CreateCharacterStatus.SlotOccupied, null);

        if (!await accounts.TryReserveCharacterNameAsync(request.CharacterName, accountName, cancellationToken))
            return new(CreateCharacterStatus.NameTaken, null);

        var template = await templates.ReadTemplateAsync(request.CharacterClass, cancellationToken);
        if (template is null) return new(CreateCharacterStatus.TemplateUnavailable, null);

        if (!await accounts.TryWriteNewCharacterAsync(accountName, request.Slot, request.CharacterName, template, cancellationToken))
            return new(CreateCharacterStatus.SlotOccupied, null);

        var snapshot = await accounts.ReadSnapshotAsync(accountName, cancellationToken);
        return snapshot is null
            ? new(CreateCharacterStatus.AccountNotFound, null)
            : new(CreateCharacterStatus.Success, snapshot.Characters);
    }
}
