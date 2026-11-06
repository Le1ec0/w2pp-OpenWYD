using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

/// <summary>
/// COMBAT-ATTACK-SKILL-NPC-SUMMON-PARTY-001: TMProject 7.69 GenerateSummon (Server.cpp:3763-4053)
/// party scan, first-eight-template conflicts, out-of-view pull, per-summon party limit, guild/cape
/// copy and SendAddParty frames for InstanceType 11.
/// </summary>
internal static class AttackNpcSummonPartyTests
{
    private const int LeaderId = 1;
    private static readonly Lazy<LegacySummonCatalog> Catalog = new(() => LegacySummonCatalog.Load(Path.Combine(FindReference769TmsrvRun(), "BaseSummon")));

    public static void SummonsSendLeaderAndMemberPartyAdds()
    {
        var hub = CreateWorld();
        AddPlayer(hub, LeaderId, "SUMMONER", 2_000, 2_000, special3: 60);
        var outcome = Cast(hub, LeaderId, instanceValue: 1);

        Check(outcome.SummonResult == LegacySummonGenerationResult.Created && outcome.SummonedMobs!.Count == 2,
            $"Special[2] 60 did not create two InstanceValue 1 summons: {outcome.SummonResult}.");
        var adds = outcome.PartyAddNotices!;
        // SummonNum (pre-existing same type) stays 0, so 7.69 repeats SendAddParty(Leader, Leader, 0) per summon.
        Check(adds.Count == 4 &&
            adds[0] is { RecipientConnectionId: LeaderId, MemberConnectionId: LeaderId, LeaderField: LeaderId } &&
            adds[1].RecipientConnectionId == LeaderId && adds[1].MemberConnectionId == outcome.SummonedMobs![0].ConnectionId && adds[1].LeaderField == 30000 &&
            adds[2] is { RecipientConnectionId: LeaderId, MemberConnectionId: LeaderId } &&
            adds[3].MemberConnectionId == outcome.SummonedMobs![1].ConnectionId,
            $"InstanceType 11 did not emit the 7.69 SendAddParty sequence: {string.Join(", ", adds.Select(add => $"{add.RecipientConnectionId}<-{add.MemberConnectionId}/{add.LeaderField}"))}.");
        Check(adds[1].MobName.EndsWith('^'), "The summon party entry did not carry the summon name.");
    }

    public static void SummonCopiesGuildAndMapsCape()
    {
        var hub = CreateWorld();
        AddPlayer(hub, LeaderId, "SUMMONER", 2_000, 2_000, special3: 30, guild: 77, cape: 543);
        var outcome = Cast(hub, LeaderId, instanceValue: 1);
        var mob = outcome.SummonedMobs!.Single().MobSnapshot;
        Check(BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)) == 77,
            "The summon did not inherit the summoner's Guild.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (15 * LegacyItem.SizeInBytes))) == 734,
            "Cape 543 was not mapped to summon cape 734.");

        var disabledHub = CreateWorld();
        AddPlayer(disabledHub, LeaderId, "SUMMONER", 2_000, 2_000, special3: 30, guild: 77, cape: 548);
        Check(disabledHub.TrySetGuildDisabled(LeaderId, disabled: true) is not null, "guildoff could not be set.");
        var disabledMob = Cast(disabledHub, LeaderId, instanceValue: 1).SummonedMobs!.Single().MobSnapshot;
        Check(BinaryPrimitives.ReadUInt16LittleEndian(disabledMob.AsSpan(LegacyAccountSnapshot.MobGuildOffset)) == 0,
            "A guildoff summoner still passed the Guild to the summon.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(disabledMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (15 * LegacyItem.SizeInBytes))) == 550,
            "Cape 548 was not mapped to summon cape 550.");
    }

    public static void PartyMemberSummonOfAnotherTypeConflicts()
    {
        const int MemberId = 2;
        var hub = CreateWorld();
        AddPlayer(hub, LeaderId, "LEADER", 2_000, 2_000, special3: 30);
        AddPlayer(hub, MemberId, "MEMBER", 2_002, 2_000, special3: 30);
        Check(hub.TrySetParty(LeaderId, [LeaderId, MemberId]), "The party was not installed.");
        Check(Cast(hub, MemberId, instanceValue: 1).SummonResult == LegacySummonGenerationResult.Created, "The member summon was not created.");

        var conflict = Cast(hub, LeaderId, instanceValue: 2);
        Check(conflict.SummonResult == LegacySummonGenerationResult.TypeConflict && conflict.AttackerResourceChanged && conflict.SummonedMobs!.Count == 0,
            $"A different first-eight summon in Leader.PartyList did not block the leader's summon: {conflict.SummonResult}.");
    }

    public static void OutOfViewSummonIsPulledToTheCaster()
    {
        var hub = CreateWorld();
        AddPlayer(hub, LeaderId, "SUMMONER", 2_000, 2_000, special3: 30);
        var first = Cast(hub, LeaderId, instanceValue: 1).SummonedMobs!.Single();
        Check(hub.UpdatePosition(LeaderId, 2_100, 2_000), "The summoner could not move away.");

        var again = Cast(hub, LeaderId, instanceValue: 1);
        Check(again.SummonResult == LegacySummonGenerationResult.AlreadyPresent && again.AttackerResourceChanged,
            $"An existing same-type summon did not end GenerateSummon with the MP refund: {again.SummonResult}.");
        var pull = again.RecalledSummons!.Single();
        Check(pull.ConnectionId == first.ConnectionId && pull.Effect == 8 && pull.Speed == 6 &&
            pull.FromX == first.PositionX && Math.Abs(pull.ToX - 2_100) <= 2 && Math.Abs(pull.ToY - 2_000) <= 2,
            $"The out-of-view summon was not pulled next to the caster with Action 8/6: ({pull.ToX},{pull.ToY}).");

        var inView = Cast(hub, LeaderId, instanceValue: 1);
        Check(inView.RecalledSummons!.Count == 0, "A summon already in view was pulled again.");
    }

    public static void FullPartyStopsAfterPartialCreationWithNotice()
    {
        var hub = CreateWorld();
        AddPlayer(hub, LeaderId, "LEADER", 2_000, 2_000, special3: 60);
        var members = new List<int> { LeaderId };
        for (var id = 2; id <= 12; id++)
        {
            AddPlayer(hub, id, $"MEMBER{id}", 2_000, (short)(2_000 + id), special3: 0);
            members.Add(id);
        }
        Check(hub.TrySetParty(LeaderId, members), "The eleven-member party was not installed.");

        var outcome = Cast(hub, LeaderId, instanceValue: 1);
        Check(outcome.SummonResult == LegacySummonGenerationResult.PartyFull && outcome.SummonedMobs!.Count == 1 && outcome.AttackerResourceChanged,
            $"The 12th slot did not take one summon before PartyFull returned 0: result={outcome.SummonResult}, created={outcome.SummonedMobs?.Count}.");
        Check(outcome.PrivateNotices?.SingleOrDefault() is { ConnectionId: LeaderId } notice && notice.Message == WorldHub.LegacyPartyFullCantSummonMessage,
            "The party-full summon did not send Language.txt:33 to the caster.");
        Check(outcome.PartyAddNotices!.Count == 13, $"The created summon was not announced to the leader and the eleven members: {outcome.PartyAddNotices!.Count}.");
    }

    private static LegacySkillAttackOutcome Cast(WorldHub hub, int casterId, int instanceValue)
    {
        var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, instanceValue, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        var result = hub.TryApplySkillAttack(casterId, casterId, skill, itemData: null, weather: 0, randomFactor: 90, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome is not null, $"InstanceType 11 was not accepted: {result}.");
        return outcome!;
    }

    private static WorldHub CreateWorld() => new(summonCatalog: Catalog.Value);

    private static void AddPlayer(WorldHub hub, int connectionId, string name, short x, short y, short special3, ushort guild = 0, short cape = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), guild);
        BinaryPrimitives.WriteInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (15 * LegacyItem.SizeInBytes)), cape);
        new LegacyScore(100, 10, 30, 0, 0, 0, 0, 1_000, 1_000, 1_000, 100, 0, 0, 0, 0, 0, 0, special3, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyScore(100, 10, 30, 0, 0, 0, 0, 1_000, 1_000, 1_000, 100, 0, 120, 0, 0, 0, 0, special3, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(hub.Enter(connectionId, name, (_, _) => ValueTask.CompletedTask) &&
            hub.SetCharacterState(connectionId, 0, guild, 1, 0, 0, x, y, mob),
            $"Player {name} was not registered.");
    }

    private static string FindReference769TmsrvRun()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Backup", "Tools", "ReferenceSources", "TMProject2GlobalClient", "Servidor", "Server", "TMSrv", "run");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("Backup\\Tools\\ReferenceSources\\TMProject2GlobalClient\\Servidor\\Server\\TMSrv\\run was not found from the test output directory.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
