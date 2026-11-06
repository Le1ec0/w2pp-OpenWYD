using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class PlayerTransformationAffectTimerTests
{
    public static void Run()
    {
        ExpiredTypeSixteenRebuildsScoreAndCapturesEquipmentView();
        FiniteTypeSixteenDecrementsAndLongDurationStaysPinned();
        TypeThirtyTwoExpiryRebuildsScoreWithoutEquipment();
        TypeSeventeenLifeAuraHealsAndExpiresForPlayers();
        TypeTwentyPoisonUsesThe769PlayerDamageCapAndExpires();
        TypeTwentyTwoThunderTimerExpiresWithoutHpOrEquipmentChanges();
        TypeTwentyTwoAndTwentyThreeTimerDispatchTheirAreaSkills();
        FairyItemsAttackTheLastLivingTargetOnThePlayerTimer();
        FairyTimerRespectsTheSourceTargetGates();
        PlayerAreaAttackExcludesOnlyTheAttackerGuildsDirectedAlly();
        PlayerAreaAttackUsesTheAcceptedMovementTile();
        PlayerAreaAttackScansInnerGridFirstAndCapsAtSix();
        UnhandledPlayerAffectsUseTheCommonFiniteTimerTail();
        TimerShardsFollowPlayerConnectionSlot();
        RuntimeTicksOnlyTheSelectedPlayerSlot();
    }

    private static void ExpiredTypeSixteenRebuildsScoreAndCapturesEquipmentView()
    {
        var world = CreateWorld();
        var affect = CreateAffect(type: 16, value: 1, time: 1);
        AddPlayer(world, 1, "TRANSFORM_TIMER", 2_000, 2_000, headIndex: 22, affect);
        AddPlayer(world, 2, "TRANSFORM_TIMER_VIEW", 2_001, 2_000, headIndex: 0, new byte[LegacyAccountSnapshot.AffectStride]);

        Check(world.TryGetCharacterSnapshot(1, out var beforeMob, out _, out _, out var beforeMobExtra,
                  out var beforeAffect, out var beforeEquipment) && beforeMob is not null &&
              beforeMobExtra is not null && beforeAffect is not null && beforeEquipment is not null,
            "The active Type 16 player snapshot was unavailable before its timer visit.");
        var expectedAffect = beforeAffect!.ToArray();
        expectedAffect.AsSpan(0, 8).Clear();
        var expectedProjection = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                beforeMob!,
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                expectedAffect,
                beforeMobExtra!,
                0,
                2_000,
                2_000,
                beforeEquipment),
            CreateNeutralItemData());

        var outcome = world.TryProcessPlayerTimerAffects(1);

        Check(outcome is not null, "An expiring Type 16 affect did not produce a player score/equipment update.");
        Check(outcome!.ConnectionId == 1 && outcome.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The Type 16 record was not cleared when its finite time reached zero.");
        Check(outcome.MobSnapshot.AsSpan().SequenceEqual(expectedProjection.Mob) &&
              outcome.AffectSnapshot.AsSpan().SequenceEqual(expectedProjection.Affect),
            "Type 16 expiry did not rebuild the score projection from the cleared affect snapshot.");
        Check(outcome.EquipmentChanged && outcome.ViewRecipientConnectionIds.Order().SequenceEqual([1, 2]),
            "The Type 16 expiry did not request an equipment refresh for the captured 33x33 player view.");
        Check(LegacyItem.Read(outcome.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes)).Index == 22,
            "Type 16 expiry did not preserve the legacy transformed head item in the score projection.");
        Check(world.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out var storedAffect) &&
              storedAffect is not null && storedAffect.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The expired Type 16 affect was not committed to the participant snapshot.");
    }

    private static void FiniteTypeSixteenDecrementsAndLongDurationStaysPinned()
    {
        var world = CreateWorld();
        AddPlayer(world, 1, "TRANSFORM_TIMER", 2_000, 2_000, headIndex: 22, CreateAffect(type: 16, value: 1, time: 2));

        var outcome = world.TryProcessPlayerTimerAffects(1);
        Check(outcome is null && world.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out var decremented) &&
              decremented is not null && BinaryPrimitives.ReadUInt32LittleEndian(decremented.AsSpan(4)) == 1,
            "A finite Type 16 timer visit must decrement without sending a score/equipment update before expiry.");

        AddPlayer(world, 3, "TRANSFORM_TIMER_LONG", 2_000, 2_000, headIndex: 22,
            CreateAffect(type: 16, value: 1, time: 32_400_000));
        var cappedOutcome = world.TryProcessPlayerTimerAffects(3);
        Check(cappedOutcome is null && world.TryGetCharacterSnapshot(3, out _, out _, out _, out _, out var pinned) &&
              pinned is not null && BinaryPrimitives.ReadUInt32LittleEndian(pinned.AsSpan(4)) == 32_400_000,
            "A Type 16 duration at the 7.69 cap must not decrement or expire.");
    }

    private static void TypeThirtyTwoExpiryRebuildsScoreWithoutEquipment()
    {
        var world = CreateWorld();
        AddPlayer(world, 1, "PLAYER_TYPE32_TIMER", 2_000, 2_000, headIndex: 0,
            CreateAffect(type: 32, value: 7, time: 2));
        AddPlayer(world, 2, "PLAYER_TYPE32_VIEW", 2_001, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride]);

        var countdown = world.TryProcessPlayerTimerAffects(1);
        Check(countdown is null && world.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out var decremented) &&
              decremented is not null && BinaryPrimitives.ReadUInt32LittleEndian(decremented.AsSpan(4)) == 1,
            "A finite Type 32 timer visit must decrement without relaying an unchanged score.");

        var outcome = world.TryProcessPlayerTimerAffects(1);
        Check(outcome is not null && outcome.ConnectionId == 1 &&
              outcome.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "Type 32 expiry must clear the full affect record and produce a score refresh.");
        Check(!outcome!.EquipmentChanged && outcome.ViewRecipientConnectionIds.Order().SequenceEqual([1, 2]),
            "Type 32 expiry must refresh only score for the atomically captured player view.");
        Check(world.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out var storedAffect) &&
              storedAffect is not null && storedAffect.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The expired Type 32 affect was not committed to the player snapshot.");

        AddPlayer(world, 3, "PLAYER_TYPE32_TIMER_LONG", 2_000, 2_000, headIndex: 0,
            CreateAffect(type: 32, value: 7, time: 32_400_000));
        var cappedOutcome = world.TryProcessPlayerTimerAffects(3);
        Check(cappedOutcome is null && world.TryGetCharacterSnapshot(3, out _, out _, out _, out _, out var pinned) &&
              pinned is not null && BinaryPrimitives.ReadUInt32LittleEndian(pinned.AsSpan(4)) == 32_400_000,
            "A Type 32 duration at the 7.69 cap must not decrement or expire.");
    }

    private static void TypeSeventeenLifeAuraHealsAndExpiresForPlayers()
    {
        var world = CreateWorld();
        var affect = CreateAffect(type: 17, value: 20, time: 2);
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 100);
        AddPlayer(world, 1, "PLAYER_LIFE_AURA_TIMER", 2_000, 2_000, headIndex: 0, affect, currentHp: 200);

        var firstTick = world.TryProcessPlayerTimerAffects(1);
        Check(firstTick is not null,
            "A player Type 17 Life Aura must heal and produce its timer update on the matching player shard.");
        var firstTickHp = LegacyMobCombatState.Read(firstTick!.MobSnapshot).CurrentScore.Hp;
        var firstTickTime = BinaryPrimitives.ReadUInt32LittleEndian(firstTick.AffectSnapshot.AsSpan(4));
        Check(firstTickHp == 270 && firstTickTime == 1,
            $"The player Life Aura must heal and decrement its finite timer; actual HP={firstTickHp}, time={firstTickTime}.");
        Check(firstTick.HpUpdateRequired && firstTick.ScoreRefreshRequired &&
              firstTick.Hp == 270 && firstTick.Damage == 70,
            "A player Life Aura heal that changes HP must request SetHpDam followed by UpdateScore with the signed heal delta.");
        Check(!firstTick.EquipmentChanged,
            "A player Life Aura tick must not request an equipment refresh.");

        var expiryTick = world.TryProcessPlayerTimerAffects(1);
        Check(expiryTick is not null &&
              LegacyMobCombatState.Read(expiryTick.MobSnapshot).CurrentScore.Hp == 340 &&
              expiryTick.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              expiryTick.HpUpdateRequired && expiryTick.ScoreRefreshRequired &&
              expiryTick.Hp == 340 && expiryTick.Damage == 70,
            "The player Life Aura must apply its final heal before clearing the expired affect.");

        var fullHealthAffect = CreateAffect(type: 17, value: 20, time: 2);
        BinaryPrimitives.WriteUInt16LittleEndian(fullHealthAffect.AsSpan(2), 100);
        AddPlayer(world, 3, "PLAYER_LIFE_AURA_FULL", 2_000, 2_000, headIndex: 0, fullHealthAffect, currentHp: 1_000);
        var fullHealthTick = world.TryProcessPlayerTimerAffects(3);
        Check(fullHealthTick is not null && fullHealthTick.HpUpdateRequired &&
              !fullHealthTick.ScoreRefreshRequired && fullHealthTick.Hp == 1_000 && fullHealthTick.Damage == 0 &&
              BinaryPrimitives.ReadUInt32LittleEndian(fullHealthTick.AffectSnapshot.AsSpan(4)) == 1,
            "A full-health Life Aura must keep its countdown and send a zero-delta HP update without an unnecessary score refresh.");
    }

    private static void TypeTwentyPoisonUsesThe769PlayerDamageCapAndExpires()
    {
        var world = CreateWorld();
        AddPlayer(world, 1, "PLAYER_POISON", 2_000, 2_000, headIndex: 0,
            CreateAffect(type: 20, value: 10, time: 1), currentHp: 900);
        AddPlayer(world, 2, "PLAYER_POISON_VIEW", 2_001, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride]);
        AddPlayer(world, 3, "PLAYER_POISON_OUT", 2_100, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride]);

        var outcome = world.TryProcessPlayerTimerAffects(1);

        Check(outcome is not null && outcome.ConnectionId == 1 &&
              LegacyMobCombatState.Read(outcome.MobSnapshot).CurrentScore.Hp == 400 &&
              outcome.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The 7.69 player Poison timer must apply its 500 HP loss cap before expiring the finite affect.");
        Check(outcome!.HpUpdateRequired && outcome.ScoreRefreshRequired && !outcome.EquipmentChanged &&
              outcome.Hp == 400 && outcome.Damage == -500 &&
              outcome.ViewRecipientConnectionIds.Order().SequenceEqual([1, 2]),
            "Player Poison must relay its signed HP loss then score refresh to only the captured in-view peers.");
        Check(world.TryGetResourceState(1, out var current, out var requestedHp, out _) &&
              current is not null && current.CurrentScore.Hp == 400 && requestedHp == 400,
            "The player Poison tick must keep the requested HP cache aligned with the authoritative score.");

        AddPlayer(world, 4, "PLAYER_POISON_FLOOR", 2_000, 2_000, headIndex: 0,
            CreateAffect(type: 20, value: 10, time: 2), currentHp: 200);
        var floorTick = world.TryProcessPlayerTimerAffects(4);
        Check(floorTick is not null && floorTick.Hp == 1 && floorTick.Damage == -199 &&
              BinaryPrimitives.ReadUInt32LittleEndian(floorTick.AffectSnapshot.AsSpan(4)) == 1,
            "Player Poison must preserve the legacy integer truncation and clamp positive HP to one while the affect remains active.");
    }

    private static void TypeTwentyTwoThunderTimerExpiresWithoutHpOrEquipmentChanges()
    {
        var world = CreateWorld();
        AddPlayer(world, 1, "PLAYER_THUNDER_TIMER", 2_000, 2_000, headIndex: 0,
            CreateAffect(type: 22, value: 7, time: 2), currentHp: 700);
        AddPlayer(world, 2, "PLAYER_THUNDER_VIEW", 2_001, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride]);

        var countdown = world.TryProcessPlayerTimerAffects(1);
        Check(countdown is null && world.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out var decremented) &&
              decremented is not null && BinaryPrimitives.ReadUInt32LittleEndian(decremented.AsSpan(4)) == 1,
            "A finite player Type 22 timer visit must decrement without an unchanged-score or equipment relay.");

        var expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is not null && expiry.ConnectionId == 1 &&
              expiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              expiry.ScoreRefreshRequired && !expiry.HpUpdateRequired && !expiry.EquipmentChanged,
            "Player Type 22 expiry must clear all eight bytes and refresh score without HP or equipment changes.");
        Check(LegacyMobCombatState.Read(expiry!.MobSnapshot).CurrentScore.Hp == 700 &&
              expiry.ViewRecipientConnectionIds.Order().SequenceEqual([1, 2]),
            "Player Type 22 expiry must preserve HP and capture only the current in-view recipients.");

        AddPlayer(world, 3, "PLAYER_THUNDER_TIMER_LONG", 2_000, 2_000, headIndex: 0,
            CreateAffect(type: 22, value: 7, time: 32_400_000));
        var capped = world.TryProcessPlayerTimerAffects(3);
        Check(capped is null && world.TryGetCharacterSnapshot(3, out _, out _, out _, out _, out var pinned) &&
              pinned is not null && BinaryPrimitives.ReadUInt32LittleEndian(pinned.AsSpan(4)) == 32_400_000,
            "A player Type 22 duration at the 7.69 persistent threshold must stay pinned.");
    }

    private static void TypeTwentyTwoAndTwentyThreeTimerDispatchTheirAreaSkills()
    {
        foreach (var (affectType, skillId) in new[] { ((byte)22, 33), ((byte)23, 52) })
        {
            var world = CreateWorld(CreatePlayerAreaSkillData());
            AddPlayer(world, 1, $"PLAYER_AREA_AFFECT_{affectType}", 2_000, 2_000, headIndex: 0,
                CreateAffect(affectType, value: 7, time: 2), currentHp: 700);
            var targetMob = CreateAreaAttackNpcMob();
            var targetId = world.EnterNpc(targetMob, 1_999, 1_999, requestedConnectionId: 20_001, generateIndex: 1);
            Check(targetId == 20_001, $"Type {affectType} area target was not registered at its expected index.");

            var beforeMana = LegacyMobCombatState.Read(GetMob(world, 1)).CurrentScore.Mp;
            var timerOutcome = world.TryProcessPlayerTimerAffects(1);

            Check(timerOutcome is not null && timerOutcome.ConnectionId == 1,
                $"Player Type {affectType} must produce a timer outcome when its area attack runs.");
            Check(world.TryGetNpcSnapshot(targetId, out var targetAfter) && targetAfter is not null &&
                  LegacyMobCombatState.Read(targetAfter.MobSnapshot).CurrentScore.Hp < 50_000,
                $"Player Type {affectType} must dispatch Skill {skillId} through the common combat resolver.");
            Check(world.TryGetResourceState(1, out var attackerAfter, out _, out _) && attackerAfter is not null &&
                  attackerAfter.CurrentScore.Mp < beforeMana,
                $"Player Type {affectType} must debit the authoritative mana cost of Skill {skillId}.");
            Check(world.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out var affectAfter) &&
                  affectAfter is not null && BinaryPrimitives.ReadUInt32LittleEndian(affectAfter.AsSpan(4)) == 1,
                $"Player Type {affectType} must run the common affect countdown after the area attack.");
        }

        var noTargetsWorld = CreateWorld(CreatePlayerAreaSkillData());
        AddPlayer(noTargetsWorld, 1, "PLAYER_AREA_AFFECT_NO_TARGET", 2_000, 2_000, headIndex: 0,
            CreateAffect(type: 22, value: 7, time: 2), currentHp: 700);
        var manaBeforeNoTargetTick = LegacyMobCombatState.Read(GetMob(noTargetsWorld, 1)).CurrentScore.Mp;
        var noTargetOutcome = noTargetsWorld.TryProcessPlayerTimerAffects(1);
        Check(noTargetOutcome is null,
            "A player area affect with no eligible first target must not synthesize an empty Attack outcome.");
        Check(noTargetsWorld.TryGetResourceState(1, out var noTargetState, out _, out _) &&
              noTargetState is not null && noTargetState.CurrentScore.Mp == manaBeforeNoTargetTick,
            "A player area affect with no eligible target must not spend mana.");
        Check(noTargetsWorld.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out var noTargetAffect) &&
              noTargetAffect is not null && BinaryPrimitives.ReadUInt32LittleEndian(noTargetAffect.AsSpan(4)) == 1,
            "A player area affect with no eligible target must still run the common countdown.");
    }

    private static void PlayerAreaAttackScansInnerGridFirstAndCapsAtSix()
    {
        var world = CreateWorld(CreatePlayerAreaSkillData());
        AddPlayer(world, 1, "PLAYER_AREA_TARGET_ORDER", 2_000, 2_000, headIndex: 0,
            CreateAffect(type: 22, value: 7, time: 2), currentHp: 700);
        var npcLocations = new (int Id, short X, short Y)[]
        {
            (20_001, 1_999, 1_999),
            (20_002, 2_000, 1_999),
            (20_003, 1_999, 2_000),
            (20_004, 1_996, 1_996),
            (20_005, 1_997, 1_996),
            (20_006, 1_998, 1_996),
            (20_007, 1_999, 1_996),
            (20_008, 2_000, 1_996),
        };
        foreach (var (id, x, y) in npcLocations)
            Check(world.EnterNpc(CreateAreaAttackNpcMob(), x, y, requestedConnectionId: id, generateIndex: 1) == id,
                $"The Type 22 area target {id} was not registered.");

        var outcome = world.TryProcessPlayerTimerAffects(1);

        Check(outcome?.AreaAttacks.Single().TargetConnectionIds.SequenceEqual(
                  [20_001, 20_002, 20_003, 20_004, 20_005, 20_006]) == true,
            "Player Type 22 must scan the 2x2 area first, then the 5x5 area in row-major order, stopping at six targets.");
        foreach (var (id, _, _) in npcLocations)
        {
            Check(world.TryGetNpcSnapshot(id, out var npc) && npc is not null,
                $"Area attack target {id} could not be read after the timer.");
            var hp = LegacyMobCombatState.Read(npc!.MobSnapshot).CurrentScore.Hp;
            var shouldBeHit = id <= 20_006;
            Check(shouldBeHit ? hp < 50_000 : hp == 50_000,
                $"Area target {id} damage did not match the 7.69 six-target scan order: hp={hp}.");
        }
    }

    private static void FairyItemsAttackTheLastLivingTargetOnThePlayerTimer()
    {
        foreach (var (fairyIndex, skillIndex) in new[] { (753, 32), (769, 34), (1726, 36) })
        {
            var skillData = CreateFairySkillData();
            var world = CreateWorld(skillData);
            AddPlayer(world, 1, $"PLAYER_FAIRY_TIMER_{fairyIndex}", 2_000, 2_000, headIndex: 0,
                new byte[LegacyAccountSnapshot.AffectStride], fairyIndex: fairyIndex);
            var targetId = world.EnterNpc(CreateAreaAttackNpcMob(), 2_001, 2_000,
                requestedConnectionId: 20_001, generateIndex: 1);
            Check(targetId == 20_001, $"Fairy {fairyIndex} target was not registered at the expected index.");

            var seedAttackResult = world.TryApplySkillAttack(
                1, targetId, skillData[skillIndex]!, CreateNeutralItemData(), weather: 0,
                randomFactor: 110, out var seedAttack);
            Check(seedAttackResult == LegacySkillAttackResult.Accepted && seedAttack is { RemainingHp: > 0 },
                $"The living seed target for Fairy {fairyIndex} was not accepted.");
            var hpAfterSeedAttack = GetNpcHp(world, targetId);
            var manaBeforeTimer = LegacyMobCombatState.Read(GetMob(world, 1)).CurrentScore.Mp;

            var timerOutcome = world.TryProcessPlayerTimerAffects(1);

            Check(timerOutcome is not null && timerOutcome.AreaAttacks is [{ SkillIndex: var actualSkillId } attack] &&
                  actualSkillId == skillIndex && attack.OneTargetMessage &&
                  attack.TargetConnectionIds.SequenceEqual([targetId]),
                $"Fairy item {fairyIndex} did not dispatch its single-target timer skill {skillIndex}.");
            Check(GetNpcHp(world, targetId) < hpAfterSeedAttack,
                $"Fairy item {fairyIndex} did not damage the last accepted living target.");
            Check(LegacyMobCombatState.Read(GetMob(world, 1)).CurrentScore.Mp < manaBeforeTimer,
                $"Fairy item {fairyIndex} did not charge skill {skillIndex}'s authoritative mana cost.");
        }
    }

    private static void FairyTimerRespectsTheSourceTargetGates()
    {
        var skillData = CreateFairySkillData();
        var noTargetWorld = CreateWorld(skillData);
        AddPlayer(noTargetWorld, 1, "PLAYER_FAIRY_NO_TARGET", 2_000, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride], fairyIndex: 753);
        Check(noTargetWorld.TryProcessPlayerTimerAffects(1) is null,
            "A Fairy without a selected target must not synthesize an attack or an empty timer result.");

        var noManaWorld = CreateWorld(skillData);
        AddPlayer(noManaWorld, 1, "PLAYER_FAIRY_NO_MANA", 2_000, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride], fairyIndex: 753);
        var noManaTargetId = noManaWorld.EnterNpc(CreateAreaAttackNpcMob(), 2_001, 2_000,
            requestedConnectionId: 20_001, generateIndex: 1);
        Check(noManaWorld.TryApplySkillAttack(
                  1, noManaTargetId, skillData[32]!, CreateNeutralItemData(), weather: 0,
                  randomFactor: 110, out var noManaSeed) == LegacySkillAttackResult.Accepted &&
              noManaSeed is { RemainingHp: > 0 },
            "The living target for the Fairy mana gate was not accepted.");
        var noManaMob = GetMob(noManaWorld, 1);
        BinaryPrimitives.WriteInt32LittleEndian(noManaMob.AsSpan(LegacyAccountSnapshot.MobCurrentMpOffset), 0);
        Check(noManaWorld.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, noManaMob),
            "The Fairy no-mana state could not be applied after selecting its target.");
        var noManaHpBeforeTimer = GetNpcHp(noManaWorld, noManaTargetId);
        var noManaOutcome = noManaWorld.TryProcessPlayerTimerAffects(1);
        Check(noManaOutcome is { ResourceRefreshRequired: true, AreaAttacks.Count: 0 } &&
              GetNpcHp(noManaWorld, noManaTargetId) == noManaHpBeforeTimer &&
              LegacyMobCombatState.Read(GetMob(noManaWorld, 1)).CurrentScore.Mp == 0,
            "A Fairy without sufficient mana must refresh only the actor's resources and leave the target unchanged.");

        var playerTargetWorld = CreateWorld(skillData);
        AddPlayer(playerTargetWorld, 1, "PLAYER_FAIRY_PK_GATE", 2_000, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride], fairyIndex: 753);
        var pvpTargetMob = CreateAreaAttackNpcMob();
        pvpTargetMob[LegacyAccountSnapshot.MobClassOffset] = 2;
        Check(playerTargetWorld.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, pvpTargetMob),
            "The Fairy player gate target state could not be installed.");
        Check(playerTargetWorld.SetCombatEligibilityState(1, pkMode: true, guilty: false, mapAttribute: 0x40) &&
              playerTargetWorld.SetCombatEligibilityState(2, pkMode: true, guilty: false, mapAttribute: 0x40),
            "The Fairy PvP target gate could not be configured for its seed attack.");
        var playerSeedResult = playerTargetWorld.TryApplySkillAttack(
            1, 2, skillData[32]!, CreateNeutralItemData(), weather: 0, randomFactor: 110, out var playerSeed);
        Check(playerSeedResult == LegacySkillAttackResult.Accepted && playerSeed is { RemainingHp: > 0 },
            $"The living player seed target for the Fairy PvP gate was not accepted: result={playerSeedResult}, remaining={(playerSeed is null ? -1 : playerSeed.RemainingHp)}.");
        Check(playerTargetWorld.SetCombatEligibilityState(1, pkMode: false, guilty: false, mapAttribute: 0x40),
            "The Fairy PvP mode could not be disabled after selecting the player target.");
        var playerHpBeforeTimer = LegacyMobCombatState.Read(GetMob(playerTargetWorld, 2)).CurrentScore.Hp;
        Check(playerTargetWorld.TryProcessPlayerTimerAffects(1) is null &&
              LegacyMobCombatState.Read(GetMob(playerTargetWorld, 2)).CurrentScore.Hp == playerHpBeforeTimer,
            "The Fairy timer attacked a player target after PK mode was disabled.");

        var deadTargetWorld = CreateWorld(skillData);
        AddPlayer(deadTargetWorld, 1, "PLAYER_FAIRY_DEAD_ACTOR", 2_000, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride], fairyIndex: 753);
        AddPlayer(deadTargetWorld, 2, "PLAYER_FAIRY_DEAD_TARGET", 2_001, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride], currentHp: 1);
        Check(deadTargetWorld.SetCombatEligibilityState(1, pkMode: true, guilty: false, mapAttribute: 0x40),
            "The Fairy dead-target seed attack could not enable PK mode.");
        var deadSeedResult = deadTargetWorld.TryApplySkillAttack(
            1, 2, skillData[32]!, CreateNeutralItemData(), weather: 0, randomFactor: 110, out var deadSeed);
        Check(deadSeedResult == LegacySkillAttackResult.Accepted && deadSeed is { RemainingHp: 0 },
            "The Fairy dead-target seed attack did not leave a dead selected target.");
        Check(deadTargetWorld.TryProcessPlayerTimerAffects(1) is null,
            "The Fairy timer attacked a target that was already dead before the timer visit.");

        var mountTargetWorld = CreateWorld(skillData);
        AddPlayer(mountTargetWorld, 1, "PLAYER_FAIRY_MOUNT", 2_000, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride], fairyIndex: 753);
        var mountMob = CreateAreaAttackNpcMob();
        mountMob[LegacyAccountSnapshot.MobClanOffset] = 4;
        const int mountTargetId = 20_001;
        Check(mountTargetWorld.EnterNpc(mountMob, 2_001, 2_000, requestedConnectionId: mountTargetId, generateIndex: 1) == mountTargetId,
            "The Fairy Clan 4 target was not registered.");
        var mountSeedResult = mountTargetWorld.TryApplyPhysicalAttack(
            1, mountTargetId, randomFactor: 100, out var mountSeed, itemData: CreateNeutralItemData());
        Check(mountSeedResult == LegacyPhysicalAttackResult.Accepted && mountSeed is { RemainingHp: > 0 },
            "The living Clan 4 seed target for the Fairy gate was not accepted.");
        var mountHpBeforeTimer = GetNpcHp(mountTargetWorld, mountTargetId);
        Check(mountTargetWorld.TryProcessPlayerTimerAffects(1) is null &&
              GetNpcHp(mountTargetWorld, mountTargetId) == mountHpBeforeTimer,
            "The Fairy timer attacked a Clan 4 target.");
    }

    private static void PlayerAreaAttackExcludesOnlyTheAttackerGuildsDirectedAlly()
    {
        foreach (var (affectType, skillIndex) in new[] { ((byte)22, 33), ((byte)23, 52) })
        {
            var world = CreateWorld(CreatePlayerAreaSkillData());
            AddPlayer(world, 1, $"PLAYER_AREA_GUILD_7_{affectType}", 2_000, 2_000, headIndex: 0,
                CreateAffect(type: affectType, value: 7, time: 2), currentHp: 700, guildId: 7);
            AddPlayer(world, 2, $"PLAYER_AREA_GUILD_8_ALLY_{affectType}", 1_999, 1_999, headIndex: 0,
                new byte[LegacyAccountSnapshot.AffectStride], currentHp: 700, guildId: 8);
            AddPlayer(world, 3, $"PLAYER_AREA_GUILD_9_ENEMY_{affectType}", 2_000, 1_999, headIndex: 0,
                new byte[LegacyAccountSnapshot.AffectStride], currentHp: 700, guildId: 9);
            AddPlayer(world, 4, $"PLAYER_AREA_GUILD_0_{affectType}", 1_999, 1_998, headIndex: 0,
                new byte[LegacyAccountSnapshot.AffectStride], currentHp: 700, guildId: 0);
            world.SetCombatWorldState(new LegacyCombatWorldState(GuildAllies: new Dictionary<int, int> { [7] = 8 }));
            Check(world.SetCombatEligibilityState(1, pkMode: true, guilty: false, mapAttribute: 0x40) &&
                  world.SetCombatEligibilityState(2, pkMode: true, guilty: false, mapAttribute: 0x40) &&
                  world.SetCombatEligibilityState(3, pkMode: true, guilty: false, mapAttribute: 0x40) &&
                  world.SetCombatEligibilityState(4, pkMode: true, guilty: false, mapAttribute: 0x40),
                "Player area attack guild eligibility could not be configured.");

            var outcome = world.TryProcessPlayerTimerAffects(1);

            Check(outcome?.AreaAttacks.Single().SkillIndex == skillIndex &&
                  outcome.AreaAttacks.Single().TargetConnectionIds.SequenceEqual([3, 4]),
                $"A Type {affectType} area attack must skip the directed ally while retaining enemy and guildless player targets.");
            Check(LegacyMobCombatState.Read(GetMob(world, 2)).CurrentScore.Hp == 700 &&
                  LegacyMobCombatState.Read(GetMob(world, 3)).CurrentScore.Hp < 700 &&
                  LegacyMobCombatState.Read(GetMob(world, 4)).CurrentScore.Hp < 700,
                $"Type {affectType} damaged the allied player or skipped an enemy/guildless player.");

            var reverseWorld = CreateWorld(CreatePlayerAreaSkillData());
            AddPlayer(reverseWorld, 1, $"PLAYER_AREA_GUILD_8_{affectType}", 2_000, 2_000, headIndex: 0,
                CreateAffect(type: affectType, value: 7, time: 2), currentHp: 700, guildId: 8);
            AddPlayer(reverseWorld, 2, $"PLAYER_AREA_GUILD_7_REVERSE_{affectType}", 1_999, 1_999, headIndex: 0,
                new byte[LegacyAccountSnapshot.AffectStride], currentHp: 700, guildId: 7);
            reverseWorld.SetCombatWorldState(new LegacyCombatWorldState(GuildAllies: new Dictionary<int, int> { [7] = 8 }));
            Check(reverseWorld.SetCombatEligibilityState(1, pkMode: true, guilty: false, mapAttribute: 0x40) &&
                  reverseWorld.SetCombatEligibilityState(2, pkMode: true, guilty: false, mapAttribute: 0x40),
                "Reverse-direction player area attack guild eligibility could not be configured.");
            var reverseOutcome = reverseWorld.TryProcessPlayerTimerAffects(1);
            Check(reverseOutcome?.AreaAttacks.Single().TargetConnectionIds.SequenceEqual([2]) == true,
                $"Type {affectType} must not infer the reverse of a directed alliance.");
        }
    }

    private static void PlayerAreaAttackUsesTheAcceptedMovementTile()
    {
        foreach (var (affectType, skillIndex) in new[] { ((byte)22, 33), ((byte)23, 52) })
        {
            var world = CreateWorld(CreatePlayerAreaSkillData());
            AddPlayer(world, 1, $"PLAYER_AREA_MOVED_{affectType}", 2_000, 2_000, headIndex: 0,
                CreateAffect(type: affectType, value: 7, time: 2), currentHp: 700);
            Check(world.EnterNpc(CreateAreaAttackNpcMob(), 1_999, 1_999, requestedConnectionId: 20_001, generateIndex: 1) == 20_001 &&
                  world.EnterNpc(CreateAreaAttackNpcMob(), 2_099, 2_099, requestedConnectionId: 20_002, generateIndex: 1) == 20_002,
                $"Type {affectType} movement-target NPC fixtures were not registered.");

            // This is the WorldHub state transition performed after an accepted Action.
            // TMProject 7.69/W2PP GridMulticast sets pMob[conn].TargetX/Y to the same destination tile.
            Check(world.UpdatePosition(1, 2_100, 2_100),
                $"Type {affectType} accepted movement did not update the authoritative player tile.");
            var outcome = world.TryProcessPlayerTimerAffects(1);

            Check(outcome?.AreaAttacks.Single().SkillIndex == skillIndex &&
                  outcome.AreaAttacks.Single().TargetConnectionIds.SequenceEqual([20_002]),
                $"Type {affectType} area selection did not follow the accepted movement tile used as legacy TargetX/Y.");
            Check(world.TryGetNpcSnapshot(20_001, out var oldTileNpc) && oldTileNpc is not null &&
                  world.TryGetNpcSnapshot(20_002, out var movementTileNpc) && movementTileNpc is not null &&
                  LegacyMobCombatState.Read(oldTileNpc.MobSnapshot).CurrentScore.Hp == 50_000 &&
                  LegacyMobCombatState.Read(movementTileNpc.MobSnapshot).CurrentScore.Hp < 50_000,
                $"Type {affectType} timer damaged a target around the pre-move tile instead of the accepted tile.");
        }
    }

    private static void UnhandledPlayerAffectsUseTheCommonFiniteTimerTail()
    {
        var world = CreateWorld();
        var affect = CreateAffect(type: 18, value: 9, time: 2);
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 123);
        AddPlayer(world, 1, "PLAYER_TYPE18_GENERIC_TIMER", 2_000, 2_000, headIndex: 0, affect);
        AddPlayer(world, 2, "PLAYER_TYPE18_GENERIC_VIEW", 2_001, 2_000, headIndex: 0,
            new byte[LegacyAccountSnapshot.AffectStride]);

        var countdown = world.TryProcessPlayerTimerAffects(1);
        Check(countdown is null && world.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out var decremented) &&
              decremented is not null && BinaryPrimitives.ReadUInt32LittleEndian(decremented.AsSpan(4)) == 1,
            "A player affect without a special ProcessAffect branch must use the common finite countdown.");

        var expiry = world.TryProcessPlayerTimerAffects(1);
        Check(expiry is not null && expiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              expiry.ScoreRefreshRequired && !expiry.HpUpdateRequired && !expiry.EquipmentChanged &&
              expiry.ViewRecipientConnectionIds.Order().SequenceEqual([1, 2]),
            "The common player affect timer tail must clear the full record and relay a score-only expiry to the captured view.");

        var zeroTime = CreateWorld();
        AddPlayer(zeroTime, 3, "PLAYER_ZERO_TIME_GENERIC_TIMER", 2_000, 2_000, headIndex: 0,
            CreateAffect(type: 18, value: 9, time: 0));
        var zeroVisit = zeroTime.TryProcessPlayerTimerAffects(3);
        Check(zeroVisit is null && zeroTime.TryGetCharacterSnapshot(3, out _, out _, out _, out _, out var wrapped) &&
              wrapped is not null && BinaryPrimitives.ReadUInt32LittleEndian(wrapped.AsSpan(4)) == uint.MaxValue,
            "The unsigned common timer tail must wrap a zero duration to UINT_MAX without expiring the record.");

        var longDuration = CreateWorld();
        AddPlayer(longDuration, 4, "PLAYER_LONG_GENERIC_TIMER", 2_000, 2_000, headIndex: 0,
            CreateAffect(type: 18, value: 9, time: 32_400_000));
        var capped = longDuration.TryProcessPlayerTimerAffects(4);
        Check(capped is null && longDuration.TryGetCharacterSnapshot(4, out _, out _, out _, out _, out var pinned) &&
              pinned is not null && BinaryPrimitives.ReadUInt32LittleEndian(pinned.AsSpan(4)) == 32_400_000,
            "The common player affect timer tail must keep the legacy persistent-duration cap pinned.");
    }

    private static void TimerShardsFollowPlayerConnectionSlot()
    {
        var world = CreateWorld();
        AddPlayer(world, 1, "TRANSFORM_TIMER_ONE", 2_000, 2_000, headIndex: 22, CreateAffect(type: 16, value: 1, time: 2));
        AddPlayer(world, 17, "TRANSFORM_TIMER_SEVENTEEN", 2_000, 2_000, headIndex: 22, CreateAffect(type: 16, value: 1, time: 2));

        Check(world.GetParticipantIdsForTimerShard(1, 16).SequenceEqual([1, 17]) &&
              world.GetParticipantIdsForTimerShard(0, 16).Count == 0,
            "Player affect timer shards must follow the legacy connection-slot modulo cadence.");
    }

    private static void RuntimeTicksOnlyTheSelectedPlayerSlot()
    {
        var world = CreateWorld();
        AddPlayer(world, 1, "TRANSFORM_TIMER_RUNTIME", 2_000, 2_000, headIndex: 22, CreateAffect(type: 16, value: 1, time: 1));
        var relayed = new List<LegacyPlayerAffectTimerOutcome>();
        var runtime = new LegacyPlayerAffectRuntime(world, (outcome, _) =>
        {
            relayed.Add(outcome);
            return ValueTask.CompletedTask;
        });

        var emptyShard = runtime.TickShardAsync(0).AsTask().GetAwaiter().GetResult();
        Check(emptyShard.Count == 0 && relayed.Count == 0 &&
              world.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out var stillActive) &&
              stillActive is not null && BinaryPrimitives.ReadUInt32LittleEndian(stillActive.AsSpan(4)) == 1,
            "A player timer shard processed a connection slot assigned to a different second.");

        var expiryShard = runtime.TickShardAsync(1).AsTask().GetAwaiter().GetResult();
        Check(expiryShard.Count == 1 && relayed.Count == 1 && relayed[0].ConnectionId == 1,
            "The player's matching timer shard did not expire and relay its Type 16 affect.");
    }

    private static WorldHub CreateWorld(LegacySkillDataTable? skillData = null)
    {
        var world = new WorldHub(itemData: CreateNeutralItemData(), skillData: skillData);
        foreach (var id in new[] { 1, 2, 3, 4, 17 })
            Check(world.Enter(id, $"TRANSFORM_TIMER_{id}", (_, _) => ValueTask.CompletedTask),
                $"Player {id} could not be registered for the timer fixture.");
        return world;
    }

    private static LegacySkillDataTable CreatePlayerAreaSkillData()
    {
        var rows = new[] { 33, 52 }.Select(static skillId =>
            $"{skillId},0,0,1,0,0,5,8000,0,0,0,0,0,0,0,0,0,1,6,0,0,0,0,PLAYER_AREA_{skillId}");
        return LegacySkillDataTable.Load(new StringReader(string.Join(Environment.NewLine, rows)));
    }

    private static byte[] CreateAreaAttackNpcMob()
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        mob[LegacyAccountSnapshot.MobClanOffset] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobMagicOffset), 0);
        var score = new LegacyScore(50, 0, 0, 0, 0, 0, 0, 50_000, 1_000, 50_000, 1_000,
            100, 100, 100, 100, 0, 0, 0, 0);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        return mob;
    }

    private static byte[] GetMob(WorldHub world, int id)
    {
        Check(world.TryGetCharacterSnapshot(id, out var mob, out _, out _, out _, out _, out _) && mob is not null,
            $"Player {id} did not have a readable combat snapshot.");
        return mob!;
    }

    private static int GetNpcHp(WorldHub world, int id)
    {
        Check(world.TryGetNpcSnapshot(id, out var npc) && npc is not null,
            $"NPC {id} did not have a readable combat snapshot.");
        return LegacyMobCombatState.Read(npc!.MobSnapshot).CurrentScore.Hp;
    }

    private static LegacySkillDataTable CreateFairySkillData()
    {
        var rows = new[] { 32, 34, 36 }.Select(static skillId =>
            $"{skillId},0,0,1,0,0,2,8000,0,0,0,0,0,0,0,0,0,1,1,0,0,0,0,FAIRY_{skillId}");
        return LegacySkillDataTable.Load(new StringReader(string.Join(Environment.NewLine, rows)));
    }

    private static LegacyItemDataTable CreateNeutralItemData()
    {
        var encoded = new byte[LegacyItemDataTable.BodySizeInBytes];
        Array.Fill(encoded, (byte)0x5A);
        return LegacyItemDataTable.Load(encoded);
    }

    private static void AddPlayer(WorldHub world, int id, string name, short x, short y, short headIndex, byte[] affect, int currentHp = 50, int guildId = 0, int fairyIndex = 0)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        var score = new LegacyScore(50, 100, 100, 0, 0, 0, 0, 1_000, 500, currentHp, 500,
            100, 100, 100, 100, 0, 0, 0, 10);
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        score.Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        mob[LegacyAccountSnapshot.MobClassOffset] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0x20000);
        new LegacyItem(headIndex, 0, 0, 0, 0, 0, 0).Write(mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        if (fairyIndex != 0)
            new LegacyItem(checked((short)fairyIndex), 0, 0, 0, 0, 0, 0).Write(
                mob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (13 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
        Check(world.SetCharacterState(id, 0, guildId, clan: 5, guildLevel: 0, coin: 0, x, y, mob,
                LegacyAccountSnapshot.ClassMasterMortal, affect),
            $"Player {name} state could not be installed.");
    }

    private static byte[] CreateAffect(byte type, byte value, uint time)
    {
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = type;
        affect[1] = value;
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), time);
        return affect;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
