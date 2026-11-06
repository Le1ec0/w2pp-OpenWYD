using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackSkillPkGateTests
{
    private const int AttackerId = 1;
    private const int TargetId = 2;
    private const int TargetHp = 10_000;
    private const string FuryDivineRow = "6,90,1,75,3,6,0,0,0,0,0,0,0,10.0.0.24.0.0.0.0,10.0.0.24.0.0.0.0,0,0,1,1,0,0,0,0,FURIA_DIVINA";
    private static readonly LegacySkillDefinition DamageSkill = new(
        10, 69, 1, 0, 0, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "PK_DAMAGE");
    private static readonly LegacySkillDefinition FuryDivine = LegacySkillDataTable.Load(new StringReader(FuryDivineRow))[6]
        ?? throw new InvalidOperationException("Furia Divina fixture did not load.");
    private static readonly LegacyItemDataTable ItemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);

    public static void Run()
    {
        BlocksLowPkAttackerAgainstPkTargetAndNotifiesAttacker();
        PkMapAndCityExceptionsUseAttackerPosition();
        MapCityArenaCastleAndReciprocalGuildWarExceptionsMatch769();
        OutsideArenaAndOneSidedGuildWarDoNotExemptTheHit();
        HittingPkTargetMarksOnlyTheAttackerGuilty();
        FuryDivinaMarksAttackerGuiltyAgainstNonPkTarget();
        BlocksLowPkAttackerAgainstPkSummonOwner();
        PkSummonOwnerMarksOnlyTheAttackerGuilty();
        MissingSummonOwnerDoesNotApplyPlayerPkGate();
        LethalSkillRemovesSummonThroughDeathFinalization();
        ZeroDamageFurySkipsSummonPkGate();
    }

    private static void BlocksLowPkAttackerAgainstPkTargetAndNotifiesAttacker()
    {
        var world = CreateWorld(targetPk: 11);
        var result = Attack(world, DamageSkill, out var outcome);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: 0, RemainingHp: TargetHp },
            $"A character at PK 10 damaged a PK 11 target in a protected area: result={result}, outcome={outcome}.");
        Check(outcome!.PrivateNotices is [{ ConnectionId: AttackerId } notice] &&
              notice.Message == "Você não pode mais derrotar jogadores. (Atual : 10)",
            $"The PK denial did not match TMProject 7.69's localized message: {string.Join(';', outcome.PrivateNotices ?? [])}.");

        var missedWorld = CreateWorld(targetPk: 11);
        var missedResult = Attack(missedWorld, DamageSkill, out var missedOutcome, attackMissRoll: 0);
        Check(missedResult == LegacySkillAttackResult.Accepted && missedOutcome is { Damage: -3 } &&
            missedOutcome.PrivateNotices is [{ ConnectionId: AttackerId }],
            $"The post-PK 7.69 miss check did not replace denied damage with -3: result={missedResult}, outcome={missedOutcome}.");
    }

    private static void MapCityArenaCastleAndReciprocalGuildWarExceptionsMatch769()
    {
        var cases = new (string Name, short X, short Y, LegacyCombatWorldState? State, int AttackerGuild, int TargetGuild)[]
        {
            ("city", 2_052, 2_052, null, 0, 0),
            ("arena inclusive corner", 238, 230, null, 0, 0),
            ("TMProject PK map 1,31", 130, 3_970, null, 0, 0),
            ("TMProject PK map 19,30", 2_450, 3_900, null, 0, 0),
            ("active castle map 8,13", 1_100, 1_675, new LegacyCombatWorldState(CastleState: 1), 0, 0),
            ("reciprocal guild war", 3_000, 2_500,
                new LegacyCombatWorldState(GuildWarPartners: new Dictionary<int, int> { [41] = 42, [42] = 41 }), 41, 42),
        };

        foreach (var testCase in cases)
        {
            var world = CreateWorld(targetPk: 11, x: testCase.X, y: testCase.Y, attackerGuild: testCase.AttackerGuild, targetGuild: testCase.TargetGuild);
            if (testCase.State is not null)
            {
                world.SetCombatWorldState(testCase.State);
                if (testCase.State.GuildWarPartners is Dictionary<int, int> mutablePartners)
                    mutablePartners.Clear();
            }

            var result = Attack(world, DamageSkill, out var outcome);
            Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0, RemainingHp: < TargetHp },
                $"The 7.69 {testCase.Name} exception did not allow the hostile skill: result={result}, outcome={outcome}.");
            Check(outcome!.PrivateNotices is null or { Count: 0 }, $"The 7.69 {testCase.Name} exception emitted a PK denial notice.");
            Check(ReadGuilty(world, AttackerId) == 0, $"The 7.69 {testCase.Name} exception marked the attacker guilty.");
        }
    }

    private static void PkMapAndCityExceptionsUseAttackerPosition()
    {
        var outsideCityAttacker = CreateWorld(targetPk: 11, x: 2_047, y: 2_052);
        var cityResult = Attack(outsideCityAttacker, DamageSkill, out var cityOutcome);
        Check(cityResult == LegacySkillAttackResult.Accepted && cityOutcome is { Damage: 0, RemainingHp: TargetHp },
            $"The PK gate used the target's city position instead of the attacker's position: result={cityResult}, outcome={cityOutcome}.");
    }

    private static void OutsideArenaAndOneSidedGuildWarDoNotExemptTheHit()
    {
        var outsideArena = CreateWorld(targetPk: 11, x: 239, y: 230);
        var arenaResult = Attack(outsideArena, DamageSkill, out var arenaOutcome);
        Check(arenaResult == LegacySkillAttackResult.Accepted && arenaOutcome is { Damage: 0, RemainingHp: TargetHp },
            $"The 7.69 arena exception escaped its inclusive WarArea bounds: result={arenaResult}, outcome={arenaOutcome}.");

        var oneSidedWar = CreateWorld(targetPk: 11, x: 3_000, y: 2_500, attackerGuild: 41, targetGuild: 42);
        oneSidedWar.SetCombatWorldState(new LegacyCombatWorldState(GuildWarPartners: new Dictionary<int, int> { [41] = 42 }));
        var warResult = Attack(oneSidedWar, DamageSkill, out var warOutcome);
        Check(warResult == LegacySkillAttackResult.Accepted && warOutcome is { Damage: 0, RemainingHp: TargetHp },
            $"A one-sided guild-war relation bypassed the reciprocal 7.69 test: result={warResult}, outcome={warOutcome}.");

    }

    private static void HittingPkTargetMarksOnlyTheAttackerGuilty()
    {
        var world = CreateWorld(attackerPk: 11, targetPk: 11);
        var result = Attack(world, DamageSkill, out var outcome);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0 },
            $"A PK attacker could not damage another PK target: result={result}, outcome={outcome}.");
        Check(ReadGuilty(world, AttackerId) == 8 && ReadGuilty(world, TargetId) == 0,
            $"TMProject 7.69 did not apply Guilty=8 to only the attacker: attacker={ReadGuilty(world, AttackerId)}, target={ReadGuilty(world, TargetId)}.");
        Check(outcome!.CrimeStateUpdates is [{ ConnectionId: AttackerId }],
            $"The first guilty transition did not carry only the attacker's CreateMob state: {string.Join(';', outcome.CrimeStateUpdates ?? [])}.");

        var repeatedResult = Attack(world, DamageSkill, out var repeatedOutcome);
        Check(repeatedResult == LegacySkillAttackResult.Accepted && repeatedOutcome is { Damage: > 0 } &&
            repeatedOutcome.CrimeStateUpdates is null or { Count: 0 },
            $"An already guilty attacker emitted another first-transition CreateMob relay: result={repeatedResult}, outcome={repeatedOutcome}.");
    }

    private static void FuryDivinaMarksAttackerGuiltyAgainstNonPkTarget()
    {
        var world = CreateWorld(targetPk: 0);
        var result = Attack(world, FuryDivine, out var outcome, instanceRandomFactor: 0);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: 1 },
            $"Furia Divina did not resolve through the PK gate: result={result}, outcome={outcome}.");
        Check(ReadGuilty(world, AttackerId) == 8,
            $"The 7.69 skillnum==6 exception did not set attacker Guilty=8: guilty={ReadGuilty(world, AttackerId)}.");
        Check(outcome!.CrimeStateUpdates is [{ ConnectionId: AttackerId }],
            $"Furia Divina's first guilty transition did not carry the attacker's CreateMob state: {string.Join(';', outcome.CrimeStateUpdates ?? [])}.");
    }

    private static void BlocksLowPkAttackerAgainstPkSummonOwner()
    {
        var (world, summon) = CreateSummonWorld(attackerPk: 10, ownerPk: 11);
        var summonHp = LegacyScore.Read(summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Hp;
        var result = Attack(world, summon.ConnectionId, DamageSkill, out var outcome);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: 0 } && outcome.RemainingHp == summonHp,
            $"A character at PK 10 damaged a Clan 4 summon owned by a PK 11 player: result={result}, outcome={outcome}.");
        Check(outcome!.PrivateNotices is [{ ConnectionId: AttackerId } notice] &&
              notice.Message == LegacySkillPkNotice.CannotKillUser(10),
            $"The summon owner's PK denial did not notify the attacker: {string.Join(';', outcome.PrivateNotices ?? [])}.");
        Check(ReadGuilty(world, AttackerId) == 0 && ReadGuilty(world, TargetId) == 0,
            $"The denied summon hit changed attacker or owner PK state: attacker={ReadGuilty(world, AttackerId)}, owner={ReadGuilty(world, TargetId)}.");
    }

    private static void PkSummonOwnerMarksOnlyTheAttackerGuilty()
    {
        var (world, summon) = CreateSummonWorld(attackerPk: 11, ownerPk: 11);
        var summonHp = LegacyScore.Read(summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Hp;
        var result = Attack(world, summon.ConnectionId, DamageSkill, out var outcome);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0 } && outcome.RemainingHp < summonHp,
            $"A PK attacker could not damage a summon owned by another PK player: result={result}, outcome={outcome}.");
        Check(ReadGuilty(world, AttackerId) == 8 && ReadGuilty(world, TargetId) == 0,
            $"The summon PK branch did not mark only the attacker guilty: attacker={ReadGuilty(world, AttackerId)}, owner={ReadGuilty(world, TargetId)}.");
        Check(outcome!.CrimeStateUpdates is [{ ConnectionId: AttackerId }],
            $"The summon PK hit did not relay the attacker's first guilty transition: {string.Join(';', outcome.CrimeStateUpdates ?? [])}.");
    }

    private static void MissingSummonOwnerDoesNotApplyPlayerPkGate()
    {
        var result = LegacySkillPkGate.ResolveSummon(
            attackerPkPoint: 10,
            summonerPkPoint: null,
            attackerGuild: 0,
            summonerGuild: 0,
            attackerX: 3_000,
            attackerY: 2_500,
            skillId: DamageSkill.Id,
            damage: 250,
            worldState: new LegacyCombatWorldState());

        Check(result is { Damage: 250, PkPointGateBlocked: false, SendCannotKillNotice: false, MarkAttackerGuilty: false },
            $"A summon without a connected/playing owner ran the player PK gate: {result}.");
    }

    private static void LethalSkillRemovesSummonThroughDeathFinalization()
    {
        var (world, summon) = CreateSummonWorld(attackerPk: 0, ownerPk: 0);
        LegacySkillAttackResult result = LegacySkillAttackResult.ParticipantNotFound;
        LegacySkillAttackOutcome? outcome = null;
        for (var hit = 0; hit < 256 && outcome?.TargetRemoved != true; hit++)
            result = Attack(world, summon.ConnectionId, DamageSkill, out outcome);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { TargetRemoved: true, RemainingHp: 0 },
            $"A lethal hostile skill did not remove the summon through the existing death finalizer: result={result}, outcome={outcome}.");
        Check(outcome!.ExperienceAwards is null or { Count: 0 } && outcome.ItemDrops is null or { Count: 0 },
            "Removing a Clan 4 summon awarded NPC experience or drops.");
    }

    private static void ZeroDamageFurySkipsSummonPkGate()
    {
        var result = LegacySkillPkGate.ResolveSummon(
            attackerPkPoint: 11,
            summonerPkPoint: 11,
            attackerGuild: 0,
            summonerGuild: 0,
            attackerX: 3_000,
            attackerY: 2_500,
            skillId: FuryDivine.Id,
            damage: 0,
            worldState: new LegacyCombatWorldState());

        Check(result is { Damage: 0, PkPointGateBlocked: false, SendCannotKillNotice: false, MarkAttackerGuilty: false },
            $"Zero damage continued into the summon PK gate's unconditional skill-6 exception: {result}.");
    }

    private static LegacySkillAttackResult Attack(
        WorldHub world,
        int targetConnectionId,
        LegacySkillDefinition skill,
        out LegacySkillAttackOutcome? outcome,
        int instanceRandomFactor = -1,
        int attackMissRoll = 99) =>
        world.TryApplySkillAttack(
            AttackerId,
            targetConnectionId,
            skill,
            ItemData,
            weather: 0,
            randomFactor: 99,
            out outcome,
            attackMissRoll: attackMissRoll,
            instanceRandomFactor: instanceRandomFactor,
            targetIndex: 0,
            clientTick: 100);

    private static LegacySkillAttackResult Attack(
        WorldHub world,
        LegacySkillDefinition skill,
        out LegacySkillAttackOutcome? outcome,
        int instanceRandomFactor = -1,
        int attackMissRoll = 99) =>
        Attack(world, TargetId, skill, out outcome, instanceRandomFactor, attackMissRoll);

    private static (WorldHub World, LegacySummonedMob Summon) CreateSummonWorld(int attackerPk, int ownerPk)
    {
        var run = FindReference769TmsrvRun();
        var catalog = LegacySummonCatalog.Load(Path.Combine(run, "BaseSummon"));
        var attackerMob = CreateMob("SKILL_PK_ATTACKER", guildId: 0, hp: 5_000, damage: 2_000, magic: 2_000, pk: attackerPk);
        var ownerMob = CreateMob("SKILL_PK_SUMMON_OWNER", guildId: 0, hp: 5_000, damage: 0, magic: 0, pk: ownerPk);
        var ownerScore = LegacyScore.Read(ownerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with { Special3 = 40 };
        ownerScore.Write(ownerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

        var world = new WorldHub(summonCatalog: catalog);
        Check(world.Enter(AttackerId, "SKILL_PK_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
              world.Enter(TargetId, "SKILL_PK_SUMMON_OWNER", (_, _) => ValueTask.CompletedTask),
            "Summon PK participants were not registered.");
        Check(world.SetCharacterState(AttackerId, 0, 0, 1, 0, 0, 3_000, 2_500, attackerMob) &&
              world.SetCharacterState(TargetId, 0, 0, 2, 0, 0, 3_005, 2_500, ownerMob),
            "Summon PK character states were not installed.");

        var summonSkill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 8, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "TEST_SUMMON");
        Check(world.TryConsumeSkillMana(TargetId, summonSkill, out _, out _, out _) == LegacySkillManaResult.Accepted,
            "The summon owner could not consume its test summon skill.");
        var summonResult = world.TryApplySkillAttack(TargetId, TargetId, summonSkill, null, 0, 90, out var summonOutcome);
        Check(summonResult == LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            $"The summon owner did not create the Clan 4 target: result={summonResult}, outcome={summonOutcome}.");
        return (world, summonOutcome!.SummonedMobs![0]);
    }

    private static string FindReference769TmsrvRun()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Backup", "Tools", "ReferenceSources", "TMProject2GlobalClient", "Servidor", "Server", "TMSrv", "run");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("TMProject 7.69 TMSrv run directory was not found from the test output directory.");
    }

    private static WorldHub CreateWorld(
        int attackerPk = 10,
        int targetPk = 0,
        short x = 3_000,
        short y = 2_500,
        int attackerGuild = 0,
        int targetGuild = 0)
    {
        var world = new WorldHub();
        Check(world.Enter(AttackerId, "SKILL_PK_ATTACKER", (_, _) => ValueTask.CompletedTask), "The PK attacker was not registered.");
        Check(world.SetCharacterState(
                AttackerId, 0, attackerGuild, 1, 0, 0, x, y,
                CreateMob("SKILL_PK_ATTACKER", attackerGuild, hp: 5_000, damage: 2_000, magic: 2_000, pk: attackerPk)),
            "The PK attacker state was not installed.");
        Check(world.Enter(TargetId, "SKILL_PK_TARGET", (_, _) => ValueTask.CompletedTask), "The PK target was not registered.");
        Check(world.SetCharacterState(
                TargetId, 0, targetGuild, 2, 0, 0, checked((short)(x + 5)), y,
                CreateMob("SKILL_PK_TARGET", targetGuild, hp: TargetHp, damage: 0, magic: 0, pk: targetPk)),
            "The PK target state was not installed.");
        return world;
    }

    private static byte[] CreateMob(string name, int guildId, int hp, int damage, int magic, int pk)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).AsSpan(0, Math.Min(name.Length, 15)).CopyTo(mob);
        BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset), checked((ushort)guildId));
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), magic);
        var killMarkOffset = LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes);
        mob[killMarkOffset + 2] = checked((byte)pk);
        new LegacyScore(50, 0, damage, 0, 100, 0, 0, hp, 1_000, hp, 1_000, 100, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static int ReadGuilty(WorldHub world, int connectionId)
    {
        Check(world.TryGetCharacterSnapshot(connectionId, out var mob, out _, out _) && mob is not null,
            $"Character {connectionId} was not available for the guilty readback.");
        var offset = LegacyAccountSnapshot.MobCarryOffset + (LegacyAccountSnapshot.LegacyKillMarkCarrySlot * LegacyItem.SizeInBytes);
        return mob![offset + 4];
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
