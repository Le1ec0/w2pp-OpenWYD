using System.Buffers.Binary;
using System.Text;
using WydCdk.Protocol;
using WydCdk.World;

internal static class MobAiWorldTests
{
    public static void Run()
    {
        MobNameByteDoesNotActAsMerchantFlag();
        SetBattleUsesInclusiveWindowAndPreservesCurrentTarget();
        EnterWithoutCharacterSnapshotIsNotReady();
        HiddenAndMerchantFiltersKeepCombatMode();
        EnemyListDeduplicatesUsesFirstEmptyAndCapsAtThirteen();
        TargetPartySummonsFromAnotherCasterReceiveAndKeepExplicitAggro();
        PartySummonPropagationAppliesPerSummonRangeAndId1000Filter();
        PositivePhysicalHitAggrosBothSummonParties();
        PositiveSkillHitAggrosBothSummonParties();
        PlayerAttackAggroUsesEachPartySummonsRange();
    }

    private static void SetBattleUsesInclusiveWindowAndPreservesCurrentTarget()
    {
        var hub = new WorldHub();
        AddPlayer(hub, 1, "READY", 23, 23);
        AddPlayer(hub, 2, "OUTSIDE", 24, 0);
        var npc = AddNpc(hub, 1001, 0, 0);
        Check(hub.TrySetNpcBattle(npc, 1), "SetBattle rejected the inclusive (23,23) rectangle corner.");
        Check(hub.TryGetNpcCombatState(npc, out var mode, out var currentTarget, out var enemies) &&
              mode == 5 && currentTarget == 77 && enemies[0] == 1,
            "SetBattle must enter MOB_COMBAT, insert the target, and leave CurrentTarget for the selector.");

        var outside = AddNpc(hub, 1002, 0, 0);
        Check(!hub.TrySetNpcBattle(outside, 2), "SetBattle accepted a target outside the inclusive 23-tile rectangle.");
        Check(hub.TryGetNpcCombatState(outside, out mode, out _, out _) && mode == 4,
            "A target rejected by the rectangle must not enter combat.");
    }

    private static void EnterWithoutCharacterSnapshotIsNotReady()
    {
        var hub = new WorldHub();
        AddNpc(hub, 1001, 0, 0);
        Check(hub.Enter(1, "ENTER_ONLY", (_, _) => ValueTask.CompletedTask), "Enter failed.");
        Check(!hub.TrySetNpcBattle(1001, 1), "Enter alone must not qualify a participant as USER_PLAY.");
        Check(hub.SetCharacterState(1, 0, 0, 1, 0, 0, 1, 1, PlayerMob("ENTER_ONLY")), "Character snapshot setup failed.");
        Check(hub.TrySetNpcBattle(1001, 1), "A complete character snapshot should make the participant targetable.");
    }

    private static void HiddenAndMerchantFiltersKeepCombatMode()
    {
        foreach (var (targetId, hidden, merchant) in new[] { (1, true, false), (2, false, true) })
        {
            var hub = new WorldHub();
            AddPlayer(hub, targetId, "FILTERED", 1, 1, hidden, merchant);
            var npc = AddNpc(hub, 1001, 0, 0);
            Check(hub.TrySetNpcBattle(npc, targetId), "A valid target rejected at AddEnemyList should still complete SetBattle.");
            Check(hub.TryGetNpcCombatState(npc, out var mode, out _, out var enemies) && mode == 5 && enemies.All(id => id == 0),
                "The source sets Combat before hidden/merchant insertion filtering.");
        }

        var boundaryHub = new WorldHub();
        var caller = AddNpc(boundaryHub, 1001, 0, 0);
        var boundaryNpc = boundaryHub.EnterNpc(CreateMob("BOUNDARY", hp: 500, hidden: true, merchant: true), 1, 1,
            requestedConnectionId: LegacyMobAiPlanner.MaximumPlayerId, generateIndex: 1);
        Check(boundaryNpc == LegacyMobAiPlanner.MaximumPlayerId && boundaryHub.TrySetNpcBattle(caller, boundaryNpc),
            "The active NPC boundary fixture or SetBattle operation failed.");
        Check(boundaryHub.TryGetNpcCombatState(caller, out var boundaryMode, out _, out var boundaryEnemies) &&
              boundaryMode == 5 && boundaryEnemies.All(id => id == 0),
            "AddEnemyList applies hidden/merchant insertion filtering through ID MAX_USER even when that ID resolves to an NPC.");
    }

    private static void MobNameByteDoesNotActAsMerchantFlag()
    {
        var hub = new WorldHub();
        var npc = AddNpc(hub, 20_100, 1, 1);
        var namedMob = CreateMob("ABCDEFGHIJKLM", hp: 1000);
        Check(namedMob[12] == (byte)'M' && namedMob[LegacyAccountSnapshot.MobMerchantOffset] == 0,
            "Merchant/name byte boundary fixture was not constructed.");
        AddPlayer(hub, 1, "ABCDEFGHIJKLM", 1, 1);
        Check(hub.TrySetNpcBattle(npc, 1) && hub.TryGetNpcCombatState(npc, out _, out _, out var enemies) && enemies.Contains(1),
            "The thirteenth name byte must not be interpreted as MOB.Merchant.");
    }

    private static void EnemyListDeduplicatesUsesFirstEmptyAndCapsAtThirteen()
    {
        var hub = new WorldHub();
        AddPlayer(hub, 1, "TARGET", 1, 1);
        var npc = AddNpc(hub, 1001, 0, 0);
        Check(hub.SetNpcCombatState(npc, 4, 91, [0, 1, 0]), "Could not seed NPC state.");
        Check(hub.TrySetNpcBattle(npc, 1), "SetBattle failed for a valid target.");
        Check(hub.TryGetNpcCombatState(npc, out _, out var currentTarget, out var enemies) &&
              currentTarget == 91 && enemies.SequenceEqual(new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
            "Duplicate targets must not be reinserted and CurrentTarget must remain untouched.");

        var full = AddNpc(hub, 1002, 0, 0);
        Check(hub.SetNpcCombatState(full, 4, 92, Enumerable.Range(3, 13).ToArray()), "Could not seed full list.");
        Check(hub.TrySetNpcBattle(full, 1), "A full list must still count as a successful SetBattle call.");
        Check(hub.TryGetNpcCombatState(full, out var fullMode, out var fullTarget, out var fullList) &&
              fullMode == 5 && fullTarget == 92 && fullList.SequenceEqual(Enumerable.Range(3, 13)),
            "A full list must remain intact while Mode changes to Combat and CurrentTarget stays deferred.");
    }

    private static void TargetPartySummonsFromAnotherCasterReceiveAndKeepExplicitAggro()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddPlayer(hub, 1, "LEADER", 10, 10, special3: 30);
        AddPlayer(hub, 2, "MEMBER", 10, 11, special3: 30);
        AddPlayer(hub, 3, "NEARBY", 11, 10, special3: 0);
        Check(hub.TrySetParty(1, [1, 2]), "Party setup failed.");
        var npc = AddNpc(hub, LegacyMobAiPlanner.MaximumPlayerId + 1, 10, 10, hidden: true, merchant: true);
        var summon = CreateSummon(hub, 2);

        Check(hub.TrySetNpcBattle(npc, summon), "SetBattle failed against a live party summon.");
        Check(hub.TrySelectSummonTarget(summon, out var selection) && selection is { TargetConnectionId: 1001 },
            "A live summon of another party member must receive and retain the explicit NPC target ahead of its dynamic fallback.");
        Check(hub.TryGetSummonTarget(summon, out var target) && target == 1001,
            "The explicit party summon target must remain the summon target source.");

        Check(hub.TryRemoveNpc(npc, out _), "Could not remove the explicit target to exercise stale-target cleanup.");
        Check(hub.TryGetSummonTarget(summon, out target) && target == 0,
            "Removing the explicit NPC target must clear the retained summon target before the next selection.");
        Check(hub.TrySelectSummonTarget(summon, out selection) && selection is { TargetConnectionId: 1 or 3 },
            "After explicit target removal, the absent list entry must clear and the dynamic participant fallback must remain available.");
    }

    private static void PartySummonPropagationAppliesPerSummonRangeAndId1000Filter()
    {
        AssertPartySummonPropagationDistance(24, expectedPosition: 23, expectedAggro: true);
        AssertPartySummonPropagationDistance(25, expectedPosition: 24, expectedAggro: false);

        var boundaryHub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddPlayer(boundaryHub, 1, "LEADER", 10, 10, special3: 30);
        AddPlayer(boundaryHub, 2, "MEMBER", 10, 11, special3: 30);
        Check(boundaryHub.TrySetParty(1, [1, 2]), "Party setup failed for MAX_USER filter fixture.");
        AddNpc(boundaryHub, 1001, 10, 10);
        var boundaryCaller = AddNpc(boundaryHub, 1000, 10, 10, hidden: true);
        var boundarySummon = CreateSummon(boundaryHub, 2);
        Check(boundaryHub.TrySetNpcBattle(boundaryCaller, boundarySummon), "SetBattle rejected active ID 1000 caller.");
        Check(boundaryHub.TryGetSummonEnemyList(boundarySummon, out var boundaryEnemies) && !boundaryEnemies.Contains(boundaryCaller),
            "Party summon propagation must honor AddEnemyList hidden filtering for caller ID MAX_USER.");
    }

    private static void PositivePhysicalHitAggrosBothSummonParties()
    {
        var fixture = CreateSummonCombatFixture();
        var existingNpc = AddNpc(fixture.Hub, 1002, 10, 10);
        Check(fixture.Hub.TrySetNpcBattle(existingNpc, 1), "Could not seed an earlier explicit target for the attacker's summon.");

        var result = fixture.Hub.TryApplyPhysicalAttack(
            1,
            fixture.TargetSummon,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { Damage: > 0, RemainingHp: > 0, TargetRemoved: false },
            $"The physical summon hit did not reach the positive nonlethal tail: result={result}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}.");
        Check(fixture.Hub.TryGetSummonEnemyList(fixture.AttackerSummon, out var attackerEnemies) &&
              attackerEnemies.Take(2).SequenceEqual(new[] { existingNpc, fixture.TargetSummon }),
            "The attacker's party summon did not append the attacked summon after its existing explicit target.");
        Check(fixture.Hub.TryGetSummonEnemyList(fixture.TargetSummon, out var targetEnemies) && targetEnemies[0] == 1,
            "The attacked summon did not retain the attacking player as its explicit target.");
        Check(fixture.Hub.TrySelectSummonTarget(fixture.AttackerSummon, out var attackerSelection) &&
              attackerSelection?.TargetConnectionId == existingNpc,
            "Adding aggro replaced the attacker's summon existing higher-priority enemy.");
        Check(fixture.Hub.TryRemoveNpc(existingNpc, out _) &&
              fixture.Hub.TrySelectSummonTarget(fixture.AttackerSummon, out attackerSelection) &&
              attackerSelection?.TargetConnectionId == fixture.TargetSummon,
            "After clearing the earlier target, the attacked summon was not selected from the explicit list.");
        Check(fixture.Hub.TrySelectSummonTarget(fixture.TargetSummon, out var targetSelection) &&
              targetSelection?.TargetConnectionId == 1,
            "The attacked summon did not prioritize its explicit attacker over the clan fallback.");
    }

    private static void PositiveSkillHitAggrosBothSummonParties()
    {
        var fixture = CreateSummonCombatFixture();
        var skill = new LegacySkillDefinition(10, 69, 1, 20, 15, 1, 1, 40, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 2, 0, 0, 1, 0, "MOB_AI_AGGRO_TEST");
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var result = fixture.Hub.TryApplySkillAttack(
            1,
            fixture.TargetSummon,
            skill,
            itemData,
            weather: 0,
            randomFactor: 90,
            out var outcome,
            attackMissRoll: 99);

        Check(result == LegacySkillAttackResult.Accepted && outcome is { Damage: > 0, RemainingHp: > 0, TargetRemoved: false },
            $"The skill summon hit did not reach the positive nonlethal tail: result={result}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}.");
        Check(fixture.Hub.TryGetSummonEnemyList(fixture.AttackerSummon, out var attackerEnemies) && attackerEnemies.Contains(fixture.TargetSummon),
            "The attacker's party summon did not receive the skill's attacked summon as an explicit target.");
        Check(fixture.Hub.TryGetSummonEnemyList(fixture.TargetSummon, out var targetEnemies) && targetEnemies[0] == 1,
            "The skill-attacked summon did not retain the attacking player as its explicit target.");
        Check(fixture.Hub.TrySelectSummonTarget(fixture.AttackerSummon, out var attackerSelection) &&
              attackerSelection?.TargetConnectionId == fixture.TargetSummon,
            "The attacker's party summon did not prioritize the attacked summon after the skill hit.");
        Check(fixture.Hub.TrySelectSummonTarget(fixture.TargetSummon, out var targetSelection) &&
              targetSelection?.TargetConnectionId == 1,
            "The skill-attacked summon did not prioritize its explicit attacker.");
    }

    private static void PlayerAttackAggroUsesEachPartySummonsRange()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddPlayer(hub, 1, "ATTACKER_LEADER", 10, 10, special3: 30);
        AddPlayer(hub, 2, "ATTACKER_MEMBER", 50, 50, special3: 30);
        AddPlayer(hub, 3, "TARGET_LEADER", 11, 10, special3: 60);
        AddPlayer(hub, 4, "TARGET_MEMBER", 50, 50, special3: 30);
        Check(hub.TrySetParty(1, [1, 2]), "Attacker party setup failed in range fixture.");
        Check(hub.TrySetParty(3, [3, 4]), "Target party setup failed in range fixture.");
        var attackerSummon = CreateSummons(hub, 2).Single().ConnectionId;
        var targetSummons = CreateSummons(hub, 3);
        Check(targetSummons.Count == 2, "The target leader did not create two summons for the range fixture.");
        var attackedSummon = targetSummons
            .OrderBy(summon => Math.Abs(summon.PositionX - 10) + Math.Abs(summon.PositionY - 10))
            .First();
        var targetSiblingSummon = targetSummons.Single(summon => summon.ConnectionId != attackedSummon.ConnectionId);

        var result = hub.TryApplyPhysicalAttack(
            1,
            attackedSummon.ConnectionId,
            randomFactor: 99,
            out var outcome,
            attackMissRoll: 99);

        Check(result == LegacyPhysicalAttackResult.Accepted && outcome is { Damage: > 0, RemainingHp: > 0 },
            "The range fixture did not produce a positive nonlethal physical hit.");
        Check(hub.TryGetSummonEnemyList(attackerSummon, out var attackerEnemies) && !attackerEnemies.Contains(attackedSummon.ConnectionId),
            "An attacker's party summon outside the target's inclusive 23-tile rectangle received aggro.");
        Check(hub.TryGetSummonEnemyList(targetSiblingSummon.ConnectionId, out var siblingEnemies) && siblingEnemies[0] == 1,
            "An in-range sibling summon in the target party did not receive the attacker's explicit aggro.");
        Check(hub.TryGetSummonEnemyList(attackedSummon.ConnectionId, out var targetEnemies) && targetEnemies[0] == 1,
            "The in-range attacked summon did not retain the explicit attacker despite out-of-range party members.");
    }

    private static (WorldHub Hub, int AttackerSummon, int TargetSummon)
        CreateSummonCombatFixture()
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddPlayer(hub, 1, "ATTACKER_LEADER", 10, 10, special3: 30);
        AddPlayer(hub, 2, "ATTACKER_MEMBER", 10, 11, special3: 30);
        AddPlayer(hub, 3, "TARGET_LEADER", 11, 10, special3: 30);
        AddPlayer(hub, 4, "TARGET_MEMBER", 11, 11, special3: 30);
        Check(hub.TrySetParty(1, [1, 2]), "Attacker party setup failed.");
        Check(hub.TrySetParty(3, [3, 4]), "Target party setup failed.");
        var attackerSummon = CreateSummons(hub, 2).Single();
        var targetSummon = CreateSummons(hub, 3).Single();
        Check(attackerSummon.LeaderConnectionId == 1 && targetSummon.LeaderConnectionId == 3,
            "Cross-caster summons did not retain their real party leaders.");
        return (hub, attackerSummon.ConnectionId, targetSummon.ConnectionId);
    }

    private static void AssertPartySummonPropagationDistance(short summonerX, short expectedPosition, bool expectedAggro)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddPlayer(hub, 1, "LEADER", 0, 0, special3: 30);
        AddPlayer(hub, 2, "MEMBER", summonerX, summonerX, special3: 30);
        Check(hub.TrySetParty(1, [1, 2]), "Party setup failed for per-summon range fixture.");
        var summon = CreateSummon(hub, 2, out var summonMob);
        Check(summonMob.PositionX == expectedPosition && summonMob.PositionY == expectedPosition,
            $"Expected summon at ({expectedPosition},{expectedPosition}), got ({summonMob.PositionX},{summonMob.PositionY}).");
        var caller = AddNpc(hub, 1001, 0, 0);
        Check(hub.TrySetNpcBattle(caller, 1), "SetBattle rejected the in-range party leader target.");
        Check(hub.TryGetSummonEnemyList(summon, out var enemyList) && enemyList.Contains(caller) == expectedAggro,
            expectedAggro
                ? "Party propagation must include the summon at the inclusive 23-tile edge."
                : "Party propagation must exclude a summon 24 tiles away.");
    }

    private static int AddNpc(WorldHub hub, int id, short x, short y, bool hidden = false, bool merchant = false)
    {
        var idResult = hub.EnterNpc(CreateMob("NPC", hp: 500, hidden: hidden, merchant: merchant), x, y, requestedConnectionId: id, generateIndex: 1);
        Check(idResult == id, $"Could not create NPC {id}.");
        Check(hub.SetNpcCombatState(id, 4, 77, new int[13]), "Could not initialize NPC combat state.");
        return id;
    }

    private static void AddPlayer(WorldHub hub, int id, string name, short x, short y, bool hidden = false, bool merchant = false, short special3 = 0, int clan = 5)
    {
        Check(hub.Enter(id, name, (_, _) => ValueTask.CompletedTask), $"Could not Enter player {id}.");
        var mob = PlayerMob(name, special3, hidden, merchant);
        Check(hub.SetCharacterState(id, 0, 0, clan, 0, 0, x, y, mob), $"Could not set character snapshot {id}.");
    }

    private static byte[] PlayerMob(string name, short special3 = 0, bool hidden = false, bool merchant = false) =>
        CreateMob(name, hp: 1000, special3, hidden, merchant);

    private static byte[] CreateMob(string name, int hp, short special3 = 0, bool hidden = false, bool merchant = false)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        if (merchant) mob[LegacyAccountSnapshot.MobMerchantOffset] |= 1;
        if (hidden) BinaryPrimitives.WriteUInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x10);
        new LegacyScore(100, 10, 30, 0, 0, 0, 0, 1000, 1000, 1000, 100, 0, 0, 0, 0, 0, 0, special3, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyScore(100, 10, 30, 0, 0, 0, 0, 1000, 1000, hp, 100, 0, 0, 0, 0, 0, 0, special3, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        return mob;
    }

    private static LegacySummonCatalog LoadSummonCatalog()
    {
        var root = FindReferenceRoot();
        return LegacySummonCatalog.Load(Path.Combine(root, "Server", "TMSrv", "run", "BaseSummon"));
    }

    private static int CreateSummon(WorldHub hub, int summonerId) => CreateSummon(hub, summonerId, out _);

    private static int CreateSummon(WorldHub hub, int summonerId, out LegacySummonedMob createdMob)
        => CreateSummon(hub, summonerId, instanceValue: 1, out createdMob);

    private static int CreateSummon(WorldHub hub, int summonerId, int instanceValue, out LegacySummonedMob createdMob)
    {
        var createdMobs = CreateSummons(hub, summonerId, instanceValue);
        Check(createdMobs.Count > 0, "Could not create party summon fixture.");
        createdMob = createdMobs[0];
        return createdMob.ConnectionId;
    }

    private static IReadOnlyList<LegacySummonedMob> CreateSummons(WorldHub hub, int summonerId, int instanceValue = 1)
    {
        var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, instanceValue, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        var result = hub.TryApplySkillAttack(summonerId, summonerId, skill, itemData: null, weather: 0, randomFactor: 90, out var outcome);
        Check(result == LegacySkillAttackResult.Accepted && outcome?.SummonedMobs is { Count: > 0 }, "Could not create party summon fixture.");
        return outcome!.SummonedMobs!;
    }

    private static string FindReferenceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Backup", "Tools", "ReferenceSources", "TMProject2GlobalClient", "Servidor");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("TMProject 7.69 reference tree was not found.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
