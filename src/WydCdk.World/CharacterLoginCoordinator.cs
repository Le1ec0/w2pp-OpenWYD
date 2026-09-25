using WydCdk.Protocol;

namespace WydCdk.World;

public enum CharacterLoginStatus
{
    Success,
    SlotOutOfRange,
    SecureNotVerified,
    NotAvailable, // missing account, short/unavailable file, or an empty slot - the legacy handler treats all three alike (no response).
}

public sealed record CharacterLoginOutcome(CharacterLoginStatus Status, LegacyCharacterLoginData? Data)
{
    public bool IsSuccess => Status == CharacterLoginStatus.Success;
}

/// <summary>
/// Equivalent of <c>Exec_MSG_CharacterLogin</c> followed by <c>CFileDB::_MSG_DBCharacterLogin</c>
/// (CFileDB.cpp:1038-1104), merged into one step as with <see cref="CreateCharacterCoordinator"/>. The
/// reference TMSrv handler also gates login behind a parental-control/time-limit "billing" system, but that
/// whole block is only reachable when the global <c>BILLING == 2</c>; this fork initializes <c>BILLING = 3</c>
/// (Server.cpp:627) and nothing in the reference tree changes it away from 3 during normal play, which collapses
/// the gate to always take the "proceed" branch. Beyond slot range, slot occupancy, and SecurePass verification
/// (see <see cref="AccountSecureCoordinator"/>), this coordinator also depends on
/// <c>pUser[conn].Mode == USER_SELCHAR</c>, left to the caller (see <see cref="LoginSessionRegistry"/>) exactly
/// as with character creation. None of the rejection paths in the
/// reference handler send a client-visible failure signal for a bad slot or an empty one - they only log and
/// return - so callers should send nothing back either. The one exception, a plain "Wait a moment." chat message
/// for a client that is not in character selection, is not reproduced here: this port has no generic
/// chat-message wire message yet, and this path only fires on a protocol violation a normal client won't trigger.
/// </summary>
public sealed class CharacterLoginCoordinator(ICharacterStore accounts)
{
    public async ValueTask<CharacterLoginOutcome> HandleAsync(string accountName, CharacterLoginRequest request, bool secureVerified, CancellationToken cancellationToken = default)
    {
        if (request.Slot < 0 || request.Slot >= LegacyCharacterSelection.CharacterCount)
            return new(CharacterLoginStatus.SlotOutOfRange, null);

        if (!secureVerified)
            return new(CharacterLoginStatus.SecureNotVerified, null);

        var data = await accounts.ReadCharacterLoginDataAsync(accountName, request.Slot, cancellationToken);
        if (data is null)
            return new(CharacterLoginStatus.NotAvailable, null);

        // TMSrv initializes Carry[KILL_MARK] immediately after loading the MOB.
        // Without this, a new character carries zero in cEffect and the client
        // renders its name as negative while /cp reports -75.
        return new(CharacterLoginStatus.Success, data with
        {
            Mob = LegacyCharacterStorageDefaults.EnsureKillMark(data.Mob)
        });
    }
}
