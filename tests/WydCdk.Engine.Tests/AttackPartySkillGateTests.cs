using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackPartySkillGateTests
{
    public static void SummonLeaderMetadata()
    {
        var (world, summon) = CreateSummonFixture();

        Check(summon.LeaderConnectionId == 1 && summon.SummonerConnectionId == 2,
            $"A party member's summon did not retain separate party leader and summoner IDs: leader={summon.LeaderConnectionId}, summoner={summon.SummonerConnectionId}.");
    }

    public static void SummonPartyTargetGate()
    {
        var (world, summon) = CreateSummonFixture();
        var skill = CreateHealingSkill(partyCheck: 1);

        var partyLeaderResult = world.TryValidatePartySkillTarget(1, summon.ConnectionId, skill, 0, 100);
        var outsiderResult = world.TryValidatePartySkillTarget(3, summon.ConnectionId, skill, 0, 100);
        Check(partyLeaderResult == LegacySkillAttackResult.Accepted,
            $"The summon rejected its party leader: {partyLeaderResult}.");
        Check(outsiderResult == LegacySkillAttackResult.PartySkillBlocked,
            $"The summon party gate did not reject an outsider: {outsiderResult}.");

        var blockedAttack = world.TryApplySkillAttack(3, summon.ConnectionId, skill, null, 0, 90, out var blockedOutcome, targetIndex: 0, clientTick: 100);
        Check(blockedAttack == LegacySkillAttackResult.PartySkillBlocked && blockedOutcome is null,
            $"A supported party skill did not block an outsider targeting the summon: {blockedAttack}.");

        var allowedAttack = world.TryApplySkillAttack(1, summon.ConnectionId, skill, null, 0, 90, out var allowedOutcome, targetIndex: 0, clientTick: 100);
        Check(allowedAttack == LegacySkillAttackResult.UnsupportedSkillType && allowedOutcome is null,
            $"The eligible summon target unexpectedly applied an unported summon effect: {allowedAttack}.");

        var overLimit = world.TryApplySkillAttack(3, summon.ConnectionId, skill, null, 0, 90, out var overLimitOutcome, targetIndex: 2, clientTick: 100);
        Check(overLimit == LegacySkillAttackResult.TargetLimitExceeded && overLimitOutcome is null,
            $"The summon party gate took precedence over MaxTarget/type28: {overLimit}.");

        var summonHp = LegacyScore.Read(summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)).Hp;
        Check(summonHp > 0, "A bParty check changed the summon HP.");
    }

    public static void Run()
    {
        RejectsPartySkillsForPlayersOutsideThePartyAndGuild();
        AllowsPartyMembers();
        AllowsMembersOfTheSameGuild();
        LeavesNonPartySkillsUngated();
        MaxTargetKeepsPrecedenceOverPartyEligibility();
        RejectsPartySkillsForNpcTargetsOutsideTheGuild();
        AllowsPartySkillsForNpcTargetsInTheSameGuild();
        MaxTargetKeepsPrecedenceForNpcPartyTargets();
        ValidatesUnportedPartySkillsAgainstNpcTargets();
        BattleRoyalePartyGuildOverrideUsesAcceptedActionTarget();
        BattleRoyalePartyGuildOverrideHonorsInclusiveArenaBounds();
        BattleRoyaleOverrideRequiresBothActiveSignalsAndPreservesPartyRules();
        BattleRoyalePartyGuildOverrideAppliesToNpcTargets();
    }

    private static void RejectsPartySkillsForPlayersOutsideThePartyAndGuild()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 8);
        var result = world.TryApplySkillAttack(1, 2, CreateHealingSkill(partyCheck: 1), null, 0, 90, out var outcome, targetIndex: 0, clientTick: 100);

        Check(result.ToString() == "PartySkillBlocked" && outcome is null,
            "A party-only skill was applied to a player outside both the party and guild.");
        Check(world.TryGetCombatState(2, out var target) && target!.CurrentScore.Hp == 1000,
            "A blocked party skill changed the target HP.");
    }

    private static void AllowsPartyMembers()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 8);
        Check(world.TrySetParty(1, [1, 2]), "Party fixture could not be formed.");

        var result = world.TryApplySkillAttack(1, 2, CreateHealingSkill(partyCheck: 1), null, 0, 90, out var outcome, targetIndex: 0, clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { RemainingHp: > 1000 },
            "A party-only skill rejected a member of the same party.");
    }

    private static void AllowsMembersOfTheSameGuild()
    {
        var world = CreateWorld(attackerGuild: 77, targetGuild: 77);

        var result = world.TryApplySkillAttack(1, 2, CreateHealingSkill(partyCheck: 1), null, 0, 90, out var outcome, targetIndex: 0, clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { RemainingHp: > 1000 },
            "A party-only skill rejected a player from the same guild.");
    }

    private static void LeavesNonPartySkillsUngated()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 8);

        var result = world.TryApplySkillAttack(1, 2, CreateHealingSkill(partyCheck: 0), null, 0, 90, out var outcome, targetIndex: 0, clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { RemainingHp: > 1000 },
            "A skill without PartyCheck was incorrectly blocked by party eligibility.");
    }

    private static void MaxTargetKeepsPrecedenceOverPartyEligibility()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 8);

        var result = world.TryApplySkillAttack(1, 2, CreateHealingSkill(partyCheck: 1), null, 0, 90, out var outcome, targetIndex: 2, clientTick: 100);

        Check(result == LegacySkillAttackResult.TargetLimitExceeded && outcome is null,
            "The later party gate suppressed the earlier MaxTarget/type28 result.");
        Check(world.TryGetCombatState(2, out var target) && target!.CurrentScore.Hp == 1000,
            "An over-limit party skill changed the target HP.");
    }

    private static void RejectsPartySkillsForNpcTargetsOutsideTheGuild()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 8);
        var npcId = AddNpc(world, guildId: 8, hp: 1000);

        var result = world.TryApplySkillAttack(1, npcId, CreateHealingSkill(partyCheck: 1), null, 0, 90, out var outcome, targetIndex: 0, clientTick: 100);

        Check(result == LegacySkillAttackResult.PartySkillBlocked && outcome is null,
            "A party-only skill was applied to an NPC outside the party and guild.");
    }

    private static void AllowsPartySkillsForNpcTargetsInTheSameGuild()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 8);
        var npcId = AddNpc(world, guildId: 7, hp: 1000);

        var result = world.TryApplySkillAttack(1, npcId, CreateHealingSkill(partyCheck: 1), null, 0, 90, out var outcome, targetIndex: 0, clientTick: 100);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { RemainingHp: > 1000 },
            "A party-only skill rejected an NPC in the same guild.");
    }

    private static void MaxTargetKeepsPrecedenceForNpcPartyTargets()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 8);
        var npcId = AddNpc(world, guildId: 8, hp: 1000);

        var result = world.TryApplySkillAttack(1, npcId, CreateHealingSkill(partyCheck: 1), null, 0, 90, out var outcome, targetIndex: 2, clientTick: 100);

        Check(result == LegacySkillAttackResult.TargetLimitExceeded && outcome is null,
            "The party eligibility gate took precedence over MaxTarget for an NPC.");
    }

    private static void ValidatesUnportedPartySkillsAgainstNpcTargets()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 8);
        var npcId = AddNpc(world, guildId: 8, hp: 1000);

        var result = world.TryValidatePartySkillTarget(1, npcId, CreateHealingSkill(partyCheck: 1), 0, 100);

        Check(result == LegacySkillAttackResult.PartySkillBlocked,
            "The shared bParty eligibility check ignored an NPC target for an unported skill effect.");
    }

    private static void BattleRoyalePartyGuildOverrideUsesAcceptedActionTarget()
    {
        var world = CreateWorld(attackerGuild: 7, targetGuild: 7);
        Check(world.UpdatePosition(1, 2_604, 1_708), "The Battle Royale Action target was not recorded.");
        world.SetCombatWorldState(new LegacyCombatWorldState(BattleRoyaleState: 1, BattleRoyaleItem: 700));

        var result = world.TryValidatePartySkillTarget(1, 2, CreateHealingSkill(partyCheck: 1), 0, 100);

        Check(result == LegacySkillAttackResult.PartySkillBlocked,
            $"The Battle Royale guild override did not revoke same-Guild eligibility inside the arena: {result}.");
    }

    private static void BattleRoyalePartyGuildOverrideHonorsInclusiveArenaBounds()
    {
        var positions = new (short X, short Y, LegacySkillAttackResult Expected, string Name)[]
        {
            (2_604, 1_708, LegacySkillAttackResult.PartySkillBlocked, "lower-left corner"),
            (2_648, 1_708, LegacySkillAttackResult.PartySkillBlocked, "lower-right corner"),
            (2_604, 1_744, LegacySkillAttackResult.PartySkillBlocked, "upper-left corner"),
            (2_648, 1_744, LegacySkillAttackResult.PartySkillBlocked, "upper-right corner"),
            (2_603, 1_708, LegacySkillAttackResult.Accepted, "west of the arena"),
            (2_649, 1_708, LegacySkillAttackResult.Accepted, "east of the arena"),
            (2_604, 1_707, LegacySkillAttackResult.Accepted, "south of the arena"),
            (2_604, 1_745, LegacySkillAttackResult.Accepted, "north of the arena"),
        };

        foreach (var (x, y, expected, name) in positions)
        {
            var world = CreateWorld(attackerGuild: 77, targetGuild: 77);
            Check(world.UpdatePosition(1, x, y), $"The Action target was not recorded for {name}.");
            world.SetCombatWorldState(new LegacyCombatWorldState(BattleRoyaleState: 2, BattleRoyaleItem: 1));

            var result = world.TryValidatePartySkillTarget(1, 2, CreateHealingSkill(partyCheck: 1), 0, 100);

            Check(result == expected,
                $"Battle Royale party eligibility at {name} ({x},{y}) was {result}, expected {expected}.");
        }
    }

    private static void BattleRoyaleOverrideRequiresBothActiveSignalsAndPreservesPartyRules()
    {
        foreach (var state in new[]
                 {
                     (BattleRoyaleState: 0, BattleRoyaleItem: 700, Name: "inactive event state"),
                     (BattleRoyaleState: 1, BattleRoyaleItem: 0, Name: "missing event item"),
                 })
        {
            var sameGuildWorld = CreateWorld(attackerGuild: 77, targetGuild: 77);
            Check(sameGuildWorld.UpdatePosition(1, 2_604, 1_708), $"The Action target was not recorded for {state.Name}.");
            sameGuildWorld.SetCombatWorldState(new LegacyCombatWorldState(
                BattleRoyaleState: state.BattleRoyaleState,
                BattleRoyaleItem: state.BattleRoyaleItem));

            var sameGuildResult = sameGuildWorld.TryValidatePartySkillTarget(1, 2, CreateHealingSkill(partyCheck: 1), 0, 100);
            Check(sameGuildResult == LegacySkillAttackResult.Accepted,
                $"The Guild override activated with {state.Name}: {sameGuildResult}.");
        }

        var differentGuildWorld = CreateWorld(attackerGuild: 77, targetGuild: 78);
        Check(differentGuildWorld.UpdatePosition(1, 2_604, 1_708), "The Action target was not recorded for the different-Guild control.");
        differentGuildWorld.SetCombatWorldState(new LegacyCombatWorldState(BattleRoyaleState: 1, BattleRoyaleItem: 700));
        var differentGuildResult = differentGuildWorld.TryValidatePartySkillTarget(1, 2, CreateHealingSkill(partyCheck: 1), 0, 100);
        Check(differentGuildResult == LegacySkillAttackResult.PartySkillBlocked,
            $"The Battle Royale override bypassed bParty for a different Guild: {differentGuildResult}.");

        var samePartyWorld = CreateWorld(attackerGuild: 77, targetGuild: 78);
        Check(samePartyWorld.TrySetParty(1, [1, 2]), "The Battle Royale same-party fixture could not be formed.");
        Check(samePartyWorld.UpdatePosition(1, 2_604, 1_708), "The Action target was not recorded for the same-party control.");
        samePartyWorld.SetCombatWorldState(new LegacyCombatWorldState(BattleRoyaleState: 1, BattleRoyaleItem: 700));
        var samePartyResult = samePartyWorld.TryValidatePartySkillTarget(1, 2, CreateHealingSkill(partyCheck: 1), 0, 100);
        Check(samePartyResult == LegacySkillAttackResult.Accepted,
            $"The Battle Royale Guild override rejected a party member: {samePartyResult}.");

        foreach (var activeState in new[] { 1, 2, 3 })
        {
            var activeWorld = CreateWorld(attackerGuild: 77, targetGuild: 77);
            Check(activeWorld.UpdatePosition(1, 2_604, 1_708), $"The Action target was not recorded for active state {activeState}.");
            activeWorld.SetCombatWorldState(new LegacyCombatWorldState(BattleRoyaleState: activeState, BattleRoyaleItem: 700));

            var activeResult = activeWorld.TryValidatePartySkillTarget(1, 2, CreateHealingSkill(partyCheck: 1), 0, 100);
            Check(activeResult == LegacySkillAttackResult.PartySkillBlocked,
                $"The Battle Royale party gate did not apply in active state {activeState}: {activeResult}.");
        }
    }

    private static void BattleRoyalePartyGuildOverrideAppliesToNpcTargets()
    {
        var world = CreateWorld(attackerGuild: 77, targetGuild: 78);
        var npcId = AddNpc(world, guildId: 77, hp: 1000);
        Check(world.UpdatePosition(1, 2_648, 1_744), "The upper-right Battle Royale Action target was not recorded.");
        world.SetCombatWorldState(new LegacyCombatWorldState(BattleRoyaleState: 1, BattleRoyaleItem: 700));

        var result = world.TryValidatePartySkillTarget(1, npcId, CreateHealingSkill(partyCheck: 1), 0, 100);

        Check(result == LegacySkillAttackResult.PartySkillBlocked,
            $"The Battle Royale Guild override did not apply to the shared NPC bParty gate: {result}.");
    }

    internal static WorldHub CreateWorld(int attackerGuild, int targetGuild, int targetHp = 1000)
    {
        var attackerMob = CreateMob(hp: 5000);
        var targetMob = CreateMob(hp: targetHp);
        var world = new WorldHub();
        Check(world.Enter(1, "PARTY_SKILL_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "PARTY_SKILL_TARGET", (_, _) => ValueTask.CompletedTask),
            "Party-skill participants were not registered.");
        Check(world.SetCharacterState(1, 0, attackerGuild, 1, 0, 0, 2000, 2000, attackerMob) &&
            world.SetCharacterState(2, 0, targetGuild, 1, 0, 0, 2005, 2005, targetMob),
            "Party-skill character states were not installed.");
        return world;
    }

    private static (WorldHub World, LegacySummonedMob Summon) CreateSummonFixture()
    {
        var run = FindReference769TmsrvRun();
        var catalog = LegacySummonCatalog.Load(Path.Combine(run, "BaseSummon"));
        var casterMob = CreateMob(hp: 5000);
        var currentScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with { Special3 = 40 };
        currentScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var leaderMob = CreateMob(hp: 5000);
        var outsiderMob = CreateMob(hp: 5000);
        var world = new WorldHub(summonCatalog: catalog);
        Check(world.Enter(1, "SUMMON_PARTY_LEADER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(2, "SUMMON_PARTY_CASTER", (_, _) => ValueTask.CompletedTask) &&
            world.Enter(3, "SUMMON_PARTY_OUTSIDER", (_, _) => ValueTask.CompletedTask),
            "Summon-party participants were not registered.");
        Check(world.SetCharacterState(1, 0, 0, 1, 0, 0, 2000, 2000, leaderMob) &&
            world.SetCharacterState(2, 0, 0, 1, 0, 0, 2005, 2005, casterMob) &&
            world.SetCharacterState(3, 0, 0, 2, 0, 0, 2005, 2005, outsiderMob),
            "Summon-party character states were not installed.");
        Check(world.TrySetParty(1, [1, 2]), "Summon-party fixture could not be formed.");

        var summonSkill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 8, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "TEST_SUMMON");
        Check(world.TryConsumeSkillMana(2, summonSkill, out _, out _, out _) == LegacySkillManaResult.Accepted,
            "Summon-party fixture could not consume summon skill mana.");
        var summonResult = world.TryApplySkillAttack(2, 2, summonSkill, null, 0, 90, out var outcome);
        Check(summonResult == LegacySkillAttackResult.Accepted && outcome?.SummonedMobs is { Count: 1 },
            $"Summon-party fixture did not create one summoned mob: result={summonResult}, outcome={outcome}.");
        return (world, outcome!.SummonedMobs![0]);
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

    private static byte[] CreateMob(int hp)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        new LegacyScore(50, 0, 0, 0, 0, 0, 0, 5000, 5000, hp, 5000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static int AddNpc(WorldHub world, int guildId, int hp)
    {
        var mob = CreateMob(hp);
        BinaryPrimitives.WriteUInt16LittleEndian(
            mob.AsSpan(LegacyAccountSnapshot.MobGuildOffset, sizeof(ushort)),
            checked((ushort)guildId));
        var npcId = world.EnterNpc(mob, 2005, 2005);
        Check(npcId > 0, "Static NPC party-skill target could not be registered.");
        return npcId;
    }

    internal static LegacySkillDefinition CreateHealingSkill(int partyCheck) => new(
        27, 0, 0, 0, 0, 0, 6, 100, 0, 0, 0, 0, 0, [], [], 0, 0, 0, 1, partyCheck, 0, 0, 0, "PARTY_HEAL");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
