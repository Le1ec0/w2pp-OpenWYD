using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using WydCdk.Protocol;
using WydCdk.Server;
using WydCdk.World;

internal static class MobAiNetwork
{
    public static async Task RunAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        Check(IPAddress.IsLoopback(endpoint.Address), "The harness listener did not bind to a loopback address.");

        using var targetClient = new TcpClient();
        await targetClient.ConnectAsync(endpoint.Address, endpoint.Port);
        using var targetServer = await listener.AcceptTcpClientAsync();
        using var observerClient = new TcpClient();
        await observerClient.ConnectAsync(endpoint.Address, endpoint.Port);
        using var observerServer = await listener.AcceptTcpClientAsync();
        using var outsideClient = new TcpClient();
        await outsideClient.ConnectAsync(endpoint.Address, endpoint.Port);
        using var outsideServer = await listener.AcceptTcpClientAsync();
        AssertLoopbackEndpoints(targetClient, targetServer);
        AssertLoopbackEndpoints(observerClient, observerServer);
        AssertLoopbackEndpoints(outsideClient, outsideServer);

        var codec = LegacyFrameCodec.CreateDefault();
        await PositiveAttackIsRelayedOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), targetClient.GetStream(), observerClient.GetStream(), codec);
        await DeathWithoutScrollIsRelayedToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), targetClient.GetStream(), observerClient.GetStream(), codec);
        await ResurrectionScrollRefreshUsesExistingCallerBoundaryAsync(
            targetServer.GetStream(), observerServer.GetStream(), targetClient.GetStream(), observerClient.GetStream(), codec);
        await ChaseMovementRelaysItsNewViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), targetClient.GetStream(), observerClient.GetStream(), codec);
        await LeashReturnRelaysMovementAsync(
            targetServer.GetStream(), observerServer.GetStream(), targetClient.GetStream(), observerClient.GetStream(), codec);
        await RangedNpcRetreatRelaysMovementAcrossViewsAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PeaceRouteMovementRelaysActionAsync(
            targetServer.GetStream(), observerServer.GetStream(), targetClient.GetStream(), observerClient.GetStream(), codec);
        await PassiveAcquisitionAttacksThroughCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), targetClient.GetStream(), observerClient.GetStream(), codec);
        await PassiveLeaderAggroRemovesDeadGeneratedFollowersAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await DeadPeaceAndIdleGeneratedNpcsRelayTypeOneRemovalAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await GeneratedNpcTypeTwentyFourExpiryRelaysTypeThreeRemovalAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await GeneratedNpcTypeThirtyTwoExpiryRelaysScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await LifeAuraFramesRelayOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PoisonAffectFramesRelayOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await CombatNpcAffectFramesRelayOnlyToCapturedViewWithoutTargetsAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await CombatThunderExpiryRelaysOnlyScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await NonGeneratedNpcAreaAffectExpiryRelaysOnlyScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec,
            affectType: 22,
            marker: "NON_GENERATED_NPC_TYPE22_SMOKE_PASS score-only=true view-scoped=true");
        await NonGeneratedNpcAreaAffectExpiryRelaysOnlyScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec,
            affectType: 23,
            marker: "NON_GENERATED_NPC_TYPE23_SMOKE_PASS score-only=true view-scoped=true");
        await NonGeneratedNpcAreaAffectExpiryRelaysOnlyScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec,
            affectType: 32,
            marker: "NON_GENERATED_NPC_TYPE32_SMOKE_PASS score-only=true view-scoped=true");
        await CancelamentoNpcType32RelaysScoreToNpcViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await EnfraquecerNpcType10RelaysScoreToNpcViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PerseguicaoNpcType3RelaysScoreToNpcViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await ToqueSagradoNpcType1RelaysScoreToNpcViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await RsvFrostPhysicalNpcType1RelaysScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await RsvFrostSkillNpcType1RelaysScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await RsvFrostPhysicalPlayerType1RelaysScoreToTargetAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await Type1DamageSkillPlayerScoreRelaysToTargetAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec,
            LancaDeGeloSkill());
        await PursuicaoPlayerScoreRelaysToTargetAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await LaminaCongelantePlayerType7RelaysScoreToTargetAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await EnfraquecerPlayerType10RelaysScoreToTargetAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await AssaltoPlayerType13RelaysScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PossuidoPlayerType14RelaysScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await EscudoMagicoPlayerType11RelaysScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await ToqueDeAthenaPlayerType15RelaysScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await MeditacaoPlayerType21RelaysScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await ElementalProtectionPlayerType25RelaysScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await GoldenShieldPlayerType31RelaysScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await ImunidadePlayerType19RelaysScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await SpiritExchangePlayerType38RelaysScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await IceEnchantPlayerType27RelaysScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await InvisibilityPlayerType28RelaysHideScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await EvasionPlayerType26RelaysParryScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await EagleEyesPlayerType36RelaysDrainScoreToCasterAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await SpectralLinkPlayerType37AddsForceDamageAndRelaysToViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await EquipmentGradeCombatBonusesRelayToViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await SoulPlayerType29Skill102RebuildScoreAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await HunterProficiencySkill82DamageRelaysToViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await Type1DamageSkillNpcScoreRelaysToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec,
            TempestadeDeGeloSkill());
        await Type1DamageSkillNpcScoreRelaysToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec,
            NevascaSkill());
        await Type1DamageSkillNpcScoreRelaysToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec,
            LancaDeGeloSkill());
        await LaminaCongelanteNpcType7RelaysScoreToNpcViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PoisonTickNpcType20RelaysScoreToNpcViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await NonGeneratedNpcLifeAuraRelaysHpThenScoreOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await NonGeneratedNpcPoisonRelaysHpThenScoreOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await CombatBestialAuraExpiryRelaysOnlyScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PeaceSummonBestialAuraExpiryRelaysOnlyScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await CombatSummonBestialAuraExpiryRelaysOnlyScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PeaceSummonThunderExpiryRelaysOnlyScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await CombatSummonThunderExpiryRelaysOnlyScoreToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PeaceSummonLifeAuraRelaysHealExpiryAndMovementAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await CombatSummonLifeAuraRelaysHealAndExpiryAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PeaceSummonPoisonRelaysMountHpThenAffectFramesAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PeaceSummonExpiryRelaysRemoveMobOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await CombatSummonExpiryRelaysRemoveMobOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await UnlistedMixedSkillBarAttackUsesGenericGetAttackAndAttackOneFieldsAsync(
            targetServer.GetStream(), observerServer.GetStream(), targetClient.GetStream(), observerClient.GetStream(), codec);
        await CombatRouteTypeFiveTeleportRelaysAcrossViewsAsync(
            targetServer.GetStream(), observerServer.GetStream(), targetClient.GetStream(), observerClient.GetStream(), codec);
        await DeadPartySummonCleanupRelaysCapturedViewAsync(
            targetServer.GetStream(),
            observerServer.GetStream(),
            outsideServer.GetStream(),
            targetClient.GetStream(),
            observerClient.GetStream(),
            outsideClient.GetStream(),
            codec);
        await GeneratedNpcGroupCreateFramesUseEachNpcViewAsync(
            targetServer.GetStream(),
            observerServer.GetStream(),
            outsideServer.GetStream(),
            targetClient.GetStream(),
            observerClient.GetStream(),
            outsideClient.GetStream(),
            codec);
        await RouteTypeThreeDespawnRelaysRemoveMobToCapturedViewAsync(
            targetServer.GetStream(),
            observerServer.GetStream(),
            outsideServer.GetStream(),
            targetClient.GetStream(),
            observerClient.GetStream(),
            outsideClient.GetStream(),
            codec);
        await RouteTypeFiveInvalidLeaderDespawnRelaysRemoveMobToCapturedViewAsync(
            targetServer.GetStream(),
            observerServer.GetStream(),
            outsideServer.GetStream(),
            targetClient.GetStream(),
            observerClient.GetStream(),
            outsideClient.GetStream(),
            codec);
        await RunequestLv2ExitMovementRelaysActionBeforeRemoveAsync(
            targetServer.GetStream(),
            observerServer.GetStream(),
            outsideServer.GetStream(),
            targetClient.GetStream(),
            observerClient.GetStream(),
            outsideClient.GetStream(),
            codec);
        await RunequestLv6AreaDespawnRelaysRemoveMobToCapturedViewAsync(
            targetServer.GetStream(),
            observerServer.GetStream(),
            outsideServer.GetStream(),
            targetClient.GetStream(),
            observerClient.GetStream(),
            outsideClient.GetStream(),
            codec);
        await PlayerTransformationExpiryRelaysScoreAndEquipmentOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerTypeThirtyTwoExpiryRelaysScoreOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerAffectExpiryRelaysScoreOnlyToCapturedViewAsync(22,
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerAffectExpiryRelaysScoreOnlyToCapturedViewAsync(18,
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerAffectExpiryRelaysScoreOnlyToCapturedViewAsync(42,
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerAffectExpiryRelaysScoreOnlyToCapturedViewAsync(24,
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerAffectExpiryRelaysScoreOnlyToCapturedViewAsync(4,
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerLifeAuraRelaysHpThenScoreOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerPoisonRelaysHpThenScoreOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerAreaAffectAttacksRelayOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerAreaAffectAttackUsesAcceptedMovementTileAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerAreaAffectAttackTargetPlayerResourcesSyncAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);
        await PlayerFairyTimerAttackRelaysOneTargetOnlyToCapturedViewAsync(
            targetServer.GetStream(), observerServer.GetStream(), outsideServer.GetStream(),
            targetClient.GetStream(), observerClient.GetStream(), outsideClient.GetStream(), codec);

        Console.WriteLine("MOB_AI_SMOKE_PASS loopback=verified shards=explicit chase=relayed leash-return=relayed ranged-retreat=relayed peace-route=relayed route3-despawn=relayed route5-invalid-leader-despawn=relayed pista-lv2-exit=movement-then-remove pista-lv6-bounds-despawn=relayed passive-aggro=relayed passive-group-dead-cleanup=relayed dead-peace-idle-cleanup=relayed npc-type24-expiry=type3-relayed npc-type32-expiry=score-only-relayed npc-type7=attackrun-view-scoped npc-type20=attack-view-scoped equipment-grade-combat=attack-relayed non-generated-npc-type32=score-only-view-scoped non-generated-npc-type17=hp-score-view-scoped non-generated-npc-type20=hp-score-view-scoped player-type16-type33-expiry=score-equip-relayed player-type32-expiry=score-only-relayed player-type22-expiry=score-only-relayed player-type42-expiry=score-only-relayed player-type24-expiry=score-only-relayed player-type4-expiry=score-only-relayed player-generic-affect-expiry=score-only-relayed player-type17-life-aura=hp-score-relayed player-type20-poison=hp-score-relayed player-type22-23-area-attack=view-scoped player-type22-23-movement-target=accepted-tile player-type22-23-target-resource=target-only player-fairy-753-769-1726=attackone-view-scoped player-area-quest-effects=mobleft-reward-pista-spawn-relayed player-type14=score-affect-caster-only player-type11=score-affect-caster-only player-type38=score-affect-caster-only player-type27=score-affect-caster-only player-type28=hide-affect-caster-only player-type26=parry-affect-caster-only player-type36=drain-affect-caster-only life-aura=hp-score-relayed poison=hp-score-relayed combat-affects-no-target=hp-score-relayed combat-thunder=score-only-relayed combat-bestial-aura=score-only-relayed summon-peace-bestial-aura=score-only-relayed summon-combat-bestial-aura=score-only-relayed summon-peace-thunder=score-only-relayed summon-combat-thunder=score-only-relayed summon-peace-life-aura=hp-score-movement-expiry-relayed summon-combat-life-aura=hp-score-expiry-relayed summon-poison-mount=send-item-then-hp-score-relayed summon-expiry=type3-relayed summon-combat-expiry=type3-relayed summon-combat-no-target-expiry=type3-relayed getattack-generic=relayed route5-combat=relayed summon-cleanup=relayed generated-group=relayed relocated-generated-group=relayed attack=positive death=relayed resurrection=refresh view-scoped=true");
    }

    private static async Task PlayerAreaAffectAttacksRelayOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        foreach (var (affectType, skillIndex) in new[] { ((byte)22, 33), ((byte)23, 52) })
        {
            var skillRows = new[] { skillIndex }.Select(static id =>
                $"{id},0,0,1,0,0,5,8000,0,0,0,0,0,0,0,0,0,1,6,0,0,0,0,PLAYER_TIMER_AREA_{id}");
            var skillData = LegacySkillDataTable.Load(new StringReader(string.Join(Environment.NewLine, skillRows)));
            var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
            var npcCatalog = LegacyNpcGenerationCatalog.Parse(
                """
                #[4283]
                MaxNumMob: 10
                MinGroup: 0
                MaxGroup: 0
                Leader: PISTA_LV6_NEXT
                StartX: 2000
                StartY: 2000
                """,
                new Dictionary<string, byte[]>
                {
                    ["PISTA_LV6_NEXT"] = CreateMob("PISTA_LV6_NEXT", 1_000, 1, skillBar: false),
                });
            var world = new WorldHub(itemData: itemData, npcGenerationCatalog: npcCatalog, skillData: skillData);
            Check(world.Enter(1, "PLAYER_AREA_TIMER", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
                  world.Enter(2, "PLAYER_AREA_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
                  world.Enter(3, "PLAYER_AREA_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
                $"Player Type {affectType} area-attack peers could not be registered.");

            var actorMob = CreateMob("AREA_TIMER", 1_000, 5, skillBar: false);
            actorMob[LegacyAccountSnapshot.MobClassOffset] = 2;
            new LegacyItem(0, 0, 79, 0, 0, 0, 0).Write(
                actorMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (12 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            var affect = new byte[LegacyAccountSnapshot.AffectStride];
            affect[0] = affectType;
            affect[1] = 7;
            BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 2);
            var observerMob = CreateMob("AREA_VIEW", 1_000, 5, skillBar: false);
            var outsideMob = CreateMob("AREA_OUTSIDE", 1_000, 5, skillBar: false);
            Check(world.SetCharacterState(1, 0, 0, 5, 0, 0, 2_000, 2_000, actorMob,
                      LegacyAccountSnapshot.ClassMasterMortal, affect) &&
                  world.SetCharacterState(2, 0, 0, 5, 0, 0, 2_001, 2_000, observerMob) &&
                  world.SetCharacterState(3, 0, 0, 5, 0, 0, 2_100, 2_000, outsideMob) &&
                  world.ConfigurePistaLv6State(1, mobCount: 100, mobsLeft: 99),
                $"Player Type {affectType} area-attack state could not be installed.");

            var targetMob = CreateMob("Zard", 50_000, 1, skillBar: false);
            Check(world.EnterNpc(targetMob, 1_999, 1_999, requestedConnectionId: 20_001, generateIndex: 1) == 20_001,
                $"Player Type {affectType} Zard target was not registered.");
            var lethalTargetMob = CreateMob("PISTA_LV6_LAST", 1, 1, skillBar: false);
            Check(world.EnterNpc(lethalTargetMob, 2_000, 1_999, requestedConnectionId: 20_002, generateIndex: 4_291) == 20_002,
                $"Player Type {affectType} lethal Pista Lv6 target was not registered.");

            var runtime = new LegacyPlayerAffectRuntime(world, (outcome, cancellationToken) =>
                LegacyPlayerAffectRelay.RelayAsync(world, outcome, codec, cancellationToken));
            var tick = await runtime.TickShardAsync(1);
            Check(tick.Count == 1 && targetReadStream.DataAvailable && observerReadStream.DataAvailable,
                $"Player Type {affectType} did not relay its timer Attack to the actor and captured view.");

            var mobLeftFrame = await ReadFrameAsync(targetReadStream, codec);
            Check(mobLeftFrame.Header.Type == MobLeftConfirmation.MessageType &&
                  mobLeftFrame.Header.Id == MobLeftConfirmation.SceneId && mobLeftFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(mobLeftFrame.Payload.Span) == 80,
                $"Player Type {affectType} did not relay the Zard MobLeft counter to its attacker.");
            var actorFrame = await ReadFrameAsync(targetReadStream, codec);
            var observerFrame = await ReadFrameAsync(observerReadStream, codec);
            Check(actorFrame.Header.Type == AttackRequest.AreaMessageType && actorFrame.IsChecksumValid &&
                  actorFrame.Payload.Span.SequenceEqual(observerFrame.Payload.Span) && observerFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadUInt16LittleEndian(actorFrame.Payload.Span[26..]) == 2_000 &&
                  BinaryPrimitives.ReadUInt16LittleEndian(actorFrame.Payload.Span[28..]) == 2_000 &&
                  BinaryPrimitives.ReadUInt16LittleEndian(actorFrame.Payload.Span[30..]) == 1 &&
                  BinaryPrimitives.ReadInt16LittleEndian(actorFrame.Payload.Span[44..]) == skillIndex &&
                  BinaryPrimitives.ReadUInt16LittleEndian(actorFrame.Payload.Span[48..]) == 20_001 &&
                  BinaryPrimitives.ReadInt32LittleEndian(actorFrame.Payload.Span[52..]) > 0 &&
                  BinaryPrimitives.ReadUInt16LittleEndian(actorFrame.Payload.Span[56..]) == 20_002 &&
                  BinaryPrimitives.ReadInt32LittleEndian(actorFrame.Payload.Span[60..]) > 0,
                $"Player Type {affectType} Attack did not carry Skill {skillIndex}'s authoritative target and damage.");
            var actorRemoval = await ReadFrameAsync(targetReadStream, codec);
            var observerRemoval = await ReadFrameAsync(observerReadStream, codec);
            Check(actorRemoval.Header.Type == RemoveMobConfirmation.MessageType &&
                  actorRemoval.Header.Id == 20_002 && actorRemoval.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(actorRemoval.Payload.Span) == 3 &&
                  observerRemoval.Header.Type == RemoveMobConfirmation.MessageType &&
                  observerRemoval.Header.Id == 20_002 && observerRemoval.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(observerRemoval.Payload.Span) == 3,
                $"Player Type {affectType} did not relay the lethal target removal only to the captured view.");
            var rewardFrame = await ReadFrameAsync(targetReadStream, codec);
            Check(rewardFrame.Header.Id == 1 && SendItemConfirmation.TryParse(rewardFrame, out var reward) &&
                  reward is { InventoryType: 1, Item.Index: 475 },
                $"Player Type {affectType} did not relay the 80th Zard rune reward to its attacker.");
            var actorSpawn = await ReadFrameAsync(targetReadStream, codec);
            var observerSpawn = await ReadFrameAsync(observerReadStream, codec);
            Check(actorSpawn.Header.Type == CreateMobConfirmation.MessageType && actorSpawn.IsChecksumValid &&
                  observerSpawn.Header.Type == CreateMobConfirmation.MessageType && observerSpawn.IsChecksumValid,
                $"Player Type {affectType} did not relay the Pista Lv6 next-wave NPC to its view.");
            await AssertNoFrameAsync(targetReadStream);
            await AssertNoFrameAsync(observerReadStream);
            await AssertNoFrameAsync(outsideReadStream);
        }
    }

    private static async Task PlayerAreaAffectAttackTargetPlayerResourcesSyncAsync(
        NetworkStream attackerSendStream,
        NetworkStream targetSendStream,
        NetworkStream outsideSendStream,
        NetworkStream attackerReadStream,
        NetworkStream targetReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        const int targetHp = 100_000;
        foreach (var (affectType, skillIndex) in new[] { ((byte)22, 33), ((byte)23, 52) })
        {
            var skillRow = $"{skillIndex},0,0,1,0,0,5,8000,0,0,0,0,0,0,0,0,0,1,6,0,0,0,0,PLAYER_TIMER_AREA_{skillIndex}";
            var skillData = LegacySkillDataTable.Load(new StringReader(skillRow));
            var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
            var world = new WorldHub(itemData: itemData, skillData: skillData);
            Check(world.Enter(1, $"AREA_RESOURCE_ATTACKER_{affectType}", (bytes, token) => attackerSendStream.WriteAsync(bytes, token)) &&
                  world.Enter(2, $"AREA_RESOURCE_TARGET_{affectType}", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
                  world.Enter(3, $"AREA_RESOURCE_OUTSIDE_{affectType}", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
                $"Player Type {affectType} resource-sync peers could not be registered.");

            var attackerMob = CreateMob("AREA_RES_ACTOR", 1_000, 5, skillBar: false);
            attackerMob[LegacyAccountSnapshot.MobClassOffset] = 2;
            var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            (attackerScore with { Dexterity = 10 }).Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            new LegacyItem(0, 0, 79, 0, 0, 0, 0).Write(
                attackerMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (12 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            var affect = new byte[LegacyAccountSnapshot.AffectStride];
            affect[0] = affectType;
            affect[1] = 7;
            BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 2);
            var targetMob = CreateMob("AREA_RES_TARGET", targetHp, 5, skillBar: false);
            var outsideMob = CreateMob("AREA_RES_OUT", 1_000, 5, skillBar: false);
            Check(world.SetCharacterState(1, 0, guildId: 11, clan: 5, guildLevel: 0, coin: 0,
                      positionX: 2_000, positionY: 2_000, attackerMob,
                      LegacyAccountSnapshot.ClassMasterMortal, affect) &&
                  world.SetCharacterState(2, 0, guildId: 12, clan: 5, guildLevel: 0, coin: 0,
                      positionX: 1_999, positionY: 1_999, targetMob) &&
                  world.SetCharacterState(3, 0, guildId: 13, clan: 5, guildLevel: 0, coin: 0,
                      positionX: 2_100, positionY: 2_000, outsideMob) &&
                  world.SetCombatEligibilityState(1, pkMode: true, guilty: false, mapAttribute: 0x40) &&
                  world.SetCombatEligibilityState(2, pkMode: true, guilty: false, mapAttribute: 0x40),
                $"Player Type {affectType} resource-sync state could not be initialized.");

            var runtime = new LegacyPlayerAffectRuntime(world, (outcome, cancellationToken) =>
                LegacyPlayerAffectRelay.RelayAsync(world, outcome, codec, cancellationToken));
            var tick = await runtime.TickShardAsync(1);
            Check(tick.Count == 1 && tick[0].AreaAttacks is [{ TargetConnectionIds: [2], BatchOutcome.SlotOutcomes: [{ SkillOutcome: { Damage: > 0, TargetHpChanged: true } }] }],
                $"Player Type {affectType} timer did not produce a damaging slot against its player target.");

            var targetResourceFrame = await ReadFrameAsync(targetReadStream, codec);
            var targetSyncedHp = BinaryPrimitives.ReadInt32LittleEndian(targetResourceFrame.Payload.Span);
            var targetSyncedMp = BinaryPrimitives.ReadInt32LittleEndian(targetResourceFrame.Payload.Span[4..]);
            Check(targetResourceFrame.Header.Type == SetHpMpConfirmation.MessageType &&
                  targetResourceFrame.Header.Id == 2 && targetResourceFrame.Header.Size == SetHpMpConfirmation.PacketSize &&
                  targetResourceFrame.IsChecksumValid && targetSyncedHp is > 0 and < targetHp && targetSyncedMp >= 0 &&
                  BinaryPrimitives.ReadInt32LittleEndian(targetResourceFrame.Payload.Span[8..]) >= targetSyncedHp &&
                  BinaryPrimitives.ReadInt32LittleEndian(targetResourceFrame.Payload.Span[12..]) == targetSyncedMp,
                $"Player Type {affectType} did not sync the authoritative HP/MP to the damaged player target.");

            var attackerAttack = await ReadFrameAsync(attackerReadStream, codec);
            var targetAttack = await ReadFrameAsync(targetReadStream, codec);
            Check(attackerAttack.Header.Type == AttackRequest.AreaMessageType &&
                  attackerAttack.IsChecksumValid && targetAttack.Header.Type == AttackRequest.AreaMessageType &&
                  targetAttack.IsChecksumValid && attackerAttack.Payload.Span.SequenceEqual(targetAttack.Payload.Span) &&
                  BinaryPrimitives.ReadInt32LittleEndian(targetAttack.Payload.Span[48..]) == 2 &&
                  BinaryPrimitives.ReadInt32LittleEndian(targetAttack.Payload.Span[52..]) > 0,
                $"Player Type {affectType} timer Attack did not reach the attacker and damaged player target.");

            await AssertNoFrameAsync(attackerReadStream);
            await AssertNoFrameAsync(targetReadStream);
            await AssertNoFrameAsync(outsideReadStream);
        }
    }

    private static async Task PlayerFairyTimerAttackRelaysOneTargetOnlyToCapturedViewAsync(
        NetworkStream attackerSendStream,
        NetworkStream targetSendStream,
        NetworkStream outsideSendStream,
        NetworkStream attackerReadStream,
        NetworkStream targetReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        foreach (var (fairyIndex, skillIndex) in new[] { (753, 32), (769, 34), (1726, 36) })
        {
            var skillRows = new[] { 32, 34, 36 }.Select(static id =>
                $"{id},0,0,1,0,0,2,8000,0,0,0,0,0,0,0,0,0,1,1,0,0,0,0,PLAYER_FAIRY_{id}");
            var skillData = LegacySkillDataTable.Load(new StringReader(string.Join(Environment.NewLine, skillRows)));
            var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
            var world = new WorldHub(itemData: itemData, skillData: skillData);
            Check(world.Enter(1, $"PLAYER_FAIRY_ATTACKER_{fairyIndex}", (bytes, token) => attackerSendStream.WriteAsync(bytes, token)) &&
                  world.Enter(2, $"PLAYER_FAIRY_TARGET_{fairyIndex}", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
                  world.Enter(3, $"PLAYER_FAIRY_OUTSIDE_{fairyIndex}", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
                $"Fairy {fairyIndex} loopback peers could not be registered.");

            var attackerMob = CreateMob("FAIRY_ACTOR", 1_000, 5, skillBar: false);
            attackerMob[LegacyAccountSnapshot.MobClassOffset] = 2;
            var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            (attackerScore with { Dexterity = 11 }).Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            new LegacyItem(checked((short)fairyIndex), 0, 0, 0, 0, 0, 0).Write(
                attackerMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (13 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            var targetMob = CreateMob("FAIRY_TARGET", 50_000, 5, skillBar: false);
            var outsideMob = CreateMob("FAIRY_OUT", 1_000, 5, skillBar: false);
            Check(world.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                      positionX: 2_000, positionY: 2_000, attackerMob) &&
                  world.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                      positionX: 2_001, positionY: 2_000, targetMob) &&
                  world.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                      positionX: 2_100, positionY: 2_000, outsideMob) &&
                  world.SetCombatEligibilityState(1, pkMode: true, guilty: false, mapAttribute: 0x40) &&
                  world.SetCombatEligibilityState(2, pkMode: true, guilty: false, mapAttribute: 0x40),
                $"Fairy {fairyIndex} player-target combat state could not be initialized.");

            var seedResult = world.TryApplySkillAttack(
                1, 2, skillData[skillIndex]!, itemData, weather: 0, randomFactor: 110, out var seedOutcome);
            Check(seedResult == LegacySkillAttackResult.Accepted && seedOutcome is { RemainingHp: > 0 },
                $"Fairy {fairyIndex} seed hit did not select a living player target.");
            var hpAfterSeed = LegacyMobCombatState.Read(seedOutcome!.TargetMobSnapshot).CurrentScore.Hp;

            var runtime = new LegacyPlayerAffectRuntime(world, (outcome, token) =>
                LegacyPlayerAffectRelay.RelayAsync(world, outcome, codec, token));
            var tick = await runtime.TickShardAsync(1);
            Check(tick is [{ AreaAttacks: [{ OneTargetMessage: true, SkillIndex: var attackSkill,
                    TargetConnectionIds: [2], BatchOutcome.SlotOutcomes: [{ SkillOutcome: { Damage: > 0, TargetHpChanged: true } }] }] }] &&
                  attackSkill == skillIndex,
                $"Fairy {fairyIndex} did not run skill {skillIndex} as one server-authored target attack.");

            var targetResources = await ReadFrameAsync(targetReadStream, codec);
            Check(targetResources.Header.Type == SetHpMpConfirmation.MessageType && targetResources.Header.Id == 2 &&
                  targetResources.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(targetResources.Payload.Span) is > 0 and < 50_000,
                $"Fairy {fairyIndex} did not synchronize the damaged player's authoritative resources.");
            var actorAttack = await ReadFrameAsync(attackerReadStream, codec);
            var targetAttack = await ReadFrameAsync(targetReadStream, codec);
            Check(actorAttack.Header.Type == AttackRequest.OneTargetMessageType &&
                  actorAttack.Header.Size == LegacyMobAttackConfirmation.PacketSize && actorAttack.IsChecksumValid &&
                  targetAttack.Header.Type == AttackRequest.OneTargetMessageType && targetAttack.IsChecksumValid &&
                  actorAttack.Payload.Span.SequenceEqual(targetAttack.Payload.Span) &&
                  actorAttack.Payload.Span[34] == 254 &&
                  BinaryPrimitives.ReadInt16LittleEndian(actorAttack.Payload.Span[44..]) == skillIndex &&
                  BinaryPrimitives.ReadUInt16LittleEndian(actorAttack.Payload.Span[48..]) == 2 &&
                  BinaryPrimitives.ReadInt32LittleEndian(actorAttack.Payload.Span[52..]) > 0,
                $"Fairy {fairyIndex} AttackOne did not carry skill, target, damage, and updated target HP.");
            Check(world.TryGetResourceState(2, out var targetState, out _, out _) && targetState is not null &&
                  targetState.CurrentScore.Hp < hpAfterSeed,
                $"Fairy {fairyIndex} did not apply its timer damage to the player target.");

            await AssertNoFrameAsync(attackerReadStream);
            await AssertNoFrameAsync(targetReadStream);
            await AssertNoFrameAsync(outsideReadStream);
        }
    }

    private static async Task PlayerAreaAffectAttackUsesAcceptedMovementTileAsync(
        NetworkStream attackerSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream attackerReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        foreach (var (affectType, skillIndex) in new[] { ((byte)22, 33), ((byte)23, 52) })
        {
            var skillRow = $"{skillIndex},0,0,1,0,0,5,8000,0,0,0,0,0,0,0,0,0,1,6,0,0,0,0,PLAYER_TIMER_MOVEMENT_{skillIndex}";
            var skillData = LegacySkillDataTable.Load(new StringReader(skillRow));
            var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
            var world = new WorldHub(itemData: itemData, skillData: skillData);
            Check(world.Enter(1, $"AREA_MOVE_ATTACKER_{affectType}", (bytes, token) => attackerSendStream.WriteAsync(bytes, token)) &&
                  world.Enter(2, $"AREA_MOVE_OBSERVER_{affectType}", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
                  world.Enter(3, $"AREA_MOVE_OUTSIDE_{affectType}", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
                $"Player Type {affectType} movement-tile peers could not be registered.");

            var attackerMob = CreateMob("AREA_MOVE_ACTOR", 1_000, 5, skillBar: false);
            attackerMob[LegacyAccountSnapshot.MobClassOffset] = 2;
            new LegacyItem(0, 0, 79, 0, 0, 0, 0).Write(
                attackerMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + (12 * LegacyItem.SizeInBytes), LegacyItem.SizeInBytes));
            var affect = new byte[LegacyAccountSnapshot.AffectStride];
            affect[0] = affectType;
            affect[1] = 7;
            BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 2);
            Check(world.SetCharacterState(1, 0, guildId: 11, clan: 5, guildLevel: 0, coin: 0,
                      positionX: 2_000, positionY: 2_000, attackerMob,
                      LegacyAccountSnapshot.ClassMasterMortal, affect) &&
                  world.SetCharacterState(2, 0, guildId: 12, clan: 5, guildLevel: 0, coin: 0,
                      positionX: 2_101, positionY: 2_100, CreateMob("AREA_MOVE_VIEW", 1_000, 5, skillBar: false)) &&
                  world.SetCharacterState(3, 0, guildId: 13, clan: 5, guildLevel: 0, coin: 0,
                      positionX: 2_200, positionY: 2_200, CreateMob("AREA_MOVE_OUT", 1_000, 5, skillBar: false)),
                $"Player Type {affectType} movement-tile state could not be initialized.");

            Check(world.EnterNpc(CreateMob("AREA_MOVE_OLD", 50_000, 1, skillBar: false),
                      1_999, 1_999, requestedConnectionId: 20_001, generateIndex: 1) == 20_001 &&
                  world.EnterNpc(CreateMob("AREA_MOVE_NEW", 50_000, 1, skillBar: false),
                      2_099, 2_099, requestedConnectionId: 20_002, generateIndex: 1) == 20_002 &&
                  world.UpdatePosition(1, 2_100, 2_100),
                $"Player Type {affectType} accepted movement tile could not be applied to the smoke world.");

            var runtime = new LegacyPlayerAffectRuntime(world, (outcome, cancellationToken) =>
                LegacyPlayerAffectRelay.RelayAsync(world, outcome, codec, cancellationToken));
            var tick = await runtime.TickShardAsync(1);
            Check(tick.Count == 1 && tick[0].AreaAttacks is
                    [{ TargetConnectionIds: [20_002], BatchOutcome.SlotOutcomes: [{ SkillOutcome: { Damage: > 0 } }] }],
                $"Player Type {affectType} timer did not target the NPC beside the accepted movement tile.");

            var attackerAttack = await ReadFrameAsync(attackerReadStream, codec);
            var observerAttack = await ReadFrameAsync(observerReadStream, codec);
            Check(attackerAttack.Header.Type == AttackRequest.AreaMessageType && attackerAttack.IsChecksumValid &&
                  observerAttack.Header.Type == AttackRequest.AreaMessageType && observerAttack.IsChecksumValid &&
                  attackerAttack.Payload.Span.SequenceEqual(observerAttack.Payload.Span) &&
                  BinaryPrimitives.ReadUInt16LittleEndian(attackerAttack.Payload.Span[48..]) == 20_002 &&
                  BinaryPrimitives.ReadInt32LittleEndian(attackerAttack.Payload.Span[52..]) > 0 &&
                  BinaryPrimitives.ReadUInt16LittleEndian(attackerAttack.Payload.Span[56..]) == 0,
                $"Player Type {affectType} Attack relay did not identify only the target at the accepted movement tile.");
            Check(world.TryGetNpcSnapshot(20_001, out var oldTileNpc) && oldTileNpc is not null &&
                  world.TryGetNpcSnapshot(20_002, out var movementTileNpc) && movementTileNpc is not null &&
                  LegacyMobCombatState.Read(oldTileNpc.MobSnapshot).CurrentScore.Hp == 50_000 &&
                  LegacyMobCombatState.Read(movementTileNpc.MobSnapshot).CurrentScore.Hp < 50_000,
                $"Player Type {affectType} timer damaged around its pre-move tile or skipped the accepted tile.");
            await AssertNoFrameAsync(attackerReadStream);
            await AssertNoFrameAsync(observerReadStream);
            await AssertNoFrameAsync(outsideReadStream);
        }
    }

    private static async Task PlayerTransformationExpiryRelaysScoreAndEquipmentOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var encodedItemData = new byte[LegacyItemDataTable.BodySizeInBytes];
        Array.Fill(encodedItemData, (byte)0x5A);
        var hub = new WorldHub(itemData: LegacyItemDataTable.Load(encodedItemData));
        Check(hub.Enter(1, "PLAYER_TRANSFORM_TIMER", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "PLAYER_TRANSFORM_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "PLAYER_TRANSFORM_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The player transformation timer peers could not be registered.");

        var targetMob = CreateMob("TRANSFORM_TIMER", 1_000, 5, skillBar: false);
        targetMob[LegacyAccountSnapshot.MobClassOffset] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(targetMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0x20000);
        new LegacyItem(22, 0, 0, 0, 0, 0, 0).Write(
            targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 16;
        affect[1] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        affect[8] = 33;
        affect[9] = 5;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(12), 1);

        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, targetMob, LegacyAccountSnapshot.ClassMasterMortal, affect) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("TRANSFORM_VIEW", 1_000, 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 28, positionY: 10, CreateMob("TRANS_OUTSIDE", 1_000, 5, skillBar: false)),
            "The player transformation timer peer positions or active affect could not be initialized.");

        var runtime = new LegacyPlayerAffectRuntime(hub, (outcome, token) =>
            LegacyPlayerAffectRelay.RelayAsync(hub, outcome, codec, token));
        var skipped = await runtime.TickShardAsync(0);
        Check(skipped.Count == 0 &&
              hub.TryGetCharacterSnapshot(1, out _, out _, out _, out _, out var stillActive) &&
              stillActive is not null && stillActive[0] == 16,
            "A non-matching player slot shard advanced the Type 16 timer.");
        var expired = await runtime.TickShardAsync(1);
        Check(expired.Count == 1 && expired[0].AffectSnapshot[0] == 0 && expired[0].AffectSnapshot[8] == 0,
            "The matching player timer shard did not clear Type 16 and Type 33 at expiry.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            var equipmentFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == 1 && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset + 8] == 0,
                "An in-view peer did not receive the final Type 16/33 UpdateScore before equipment refresh.");
            Check(equipmentFrame.Header.Type == EquipmentAppearanceV769.MessageType &&
                  equipmentFrame.Header.Id == 1 && equipmentFrame.Header.Size == EquipmentAppearanceV769.PacketSize &&
                  equipmentFrame.IsChecksumValid,
                "An in-view peer did not receive the legacy SendEquip-equivalent 7.69 frame.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task PlayerTypeThirtyTwoExpiryRelaysScoreOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var encodedItemData = new byte[LegacyItemDataTable.BodySizeInBytes];
        Array.Fill(encodedItemData, (byte)0x5A);
        var hub = new WorldHub(itemData: LegacyItemDataTable.Load(encodedItemData));
        Check(hub.Enter(1, "PLAYER_TYPE32_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "PLAYER_TYPE32_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "PLAYER_TYPE32_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The player Type 32 timer peers could not be registered.");

        var targetMob = CreateMob("TYPE32_TIMER", 1_000, 5, skillBar: false);
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 32;
        affect[1] = 7;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, targetMob, LegacyAccountSnapshot.ClassMasterMortal, affect) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("TYPE32_VIEW", 1_000, 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 28, positionY: 10, CreateMob("TYPE32_OUTSIDE", 1_000, 5, skillBar: false)),
            "The player Type 32 timer positions or affect could not be initialized.");

        var runtime = new LegacyPlayerAffectRuntime(hub, (outcome, token) =>
            LegacyPlayerAffectRelay.RelayAsync(hub, outcome, codec, token));
        var expired = await runtime.TickShardAsync(1);
        Check(expired.Count == 1 && expired[0].AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              !expired[0].EquipmentChanged,
            "The matching player shard did not clear Type 32 with a score-only outcome.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == 1 && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0,
                "An in-view peer did not receive the final Type 32 UpdateScore frame.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task PlayerAffectExpiryRelaysScoreOnlyToCapturedViewAsync(
        byte affectType,
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var encodedItemData = new byte[LegacyItemDataTable.BodySizeInBytes];
        Array.Fill(encodedItemData, (byte)0x5A);
        var hub = new WorldHub(itemData: LegacyItemDataTable.Load(encodedItemData));
        Check(hub.Enter(1, $"PLAYER_AFFECT_{affectType}_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, $"PLAYER_AFFECT_{affectType}_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, $"PLAYER_AFFECT_{affectType}_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            $"The player affect {affectType} timer peers could not be registered.");

        var targetMob = CreateMob($"AF{affectType}TIMER", 1_000, 5, skillBar: false);
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = affectType;
        affect[1] = 7;
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 2);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, targetMob, LegacyAccountSnapshot.ClassMasterMortal, affect) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob($"AF{affectType}VIEW", 1_000, 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 28, positionY: 10, CreateMob($"AF{affectType}OUT", 1_000, 5, skillBar: false)),
            $"The player affect {affectType} timer positions or active affect could not be initialized.");

        var runtime = new LegacyPlayerAffectRuntime(hub, (outcome, token) =>
            LegacyPlayerAffectRelay.RelayAsync(hub, outcome, codec, token));
        var countdown = await runtime.TickShardAsync(1);
        Check(countdown.Count == 0,
            $"A non-expiring player affect {affectType} timer tick must not produce an unchanged-score update.");
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);

        var expired = await runtime.TickShardAsync(1);
        Check(expired.Count == 1 && expired[0].AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              expired[0].ScoreRefreshRequired && !expired[0].HpUpdateRequired && !expired[0].EquipmentChanged,
            $"The player affect {affectType} expiry must clear the full affect and produce a score-only update.");
        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == 1 && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset + 7] == 0,
                $"An in-view peer did not receive the player affect {affectType} score with the complete expired record cleared.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task PlayerLifeAuraRelaysHpThenScoreOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var encodedItemData = new byte[LegacyItemDataTable.BodySizeInBytes];
        Array.Fill(encodedItemData, (byte)0x5A);
        var hub = new WorldHub(itemData: LegacyItemDataTable.Load(encodedItemData));
        Check(hub.Enter(1, "PLAYER_LIFE_AURA_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "PLAYER_LIFE_AURA_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "PLAYER_LIFE_AURA_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The player Life Aura loopback peers could not be registered.");

        var targetMob = CreateMob("LIFE_TARGET", 1_000, 5, skillBar: false);
        var targetScore = LegacyScore.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        (targetScore with { Hp = 500 }).Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 17;
        affect[1] = 20;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, targetMob, LegacyAccountSnapshot.ClassMasterMortal, affect) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("LIFE_VIEW", 1_000, 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 28, positionY: 10, CreateMob("LIFE_OUTSIDE", 1_000, 5, skillBar: false)),
            "The player Life Aura peers or active affect could not be initialized.");

        var runtime = new LegacyPlayerAffectRuntime(hub, (outcome, token) =>
            LegacyPlayerAffectRelay.RelayAsync(hub, outcome, codec, token));
        var expired = await runtime.TickShardAsync(1);
        Check(expired.Count == 1 && expired[0].Hp == 570 && expired[0].Damage == 70 &&
              expired[0].HpUpdateRequired && expired[0].ScoreRefreshRequired &&
              expired[0].AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The matching player shard did not heal and expire Life Aura with both HP and score updates.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var hpFrame = await ReadFrameAsync(viewerStream, codec);
            Check(hpFrame.Header.Type == SetHpDamConfirmation.MessageType && hpFrame.Header.Id == 1 &&
                  hpFrame.Header.Size == SetHpDamConfirmation.PacketSize && hpFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span) == 570 &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span[4..]) == 70,
                "An in-view peer did not receive the player Life Aura SetHpDam heal before UpdateScore.");

            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType && scoreFrame.Header.Id == 1 &&
                  scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize && scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 570 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset + 1] == 0,
                "An in-view peer did not receive the final player Life Aura score and cleared affect.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task PlayerPoisonRelaysHpThenScoreOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var encodedItemData = new byte[LegacyItemDataTable.BodySizeInBytes];
        Array.Fill(encodedItemData, (byte)0x5A);
        var hub = new WorldHub(itemData: LegacyItemDataTable.Load(encodedItemData));
        Check(hub.Enter(1, "POISON_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "POISON_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "POISON_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The player Poison timer peers could not be registered.");

        var targetMob = CreateMob("POISON_TARGET", 1_000, 5, skillBar: false);
        var targetScore = LegacyScore.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        (targetScore with { Hp = 900 }).Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 20;
        affect[1] = 10;
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, targetMob, LegacyAccountSnapshot.ClassMasterMortal, affect) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("POISON_VIEW", 1_000, 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 28, positionY: 10, CreateMob("POISON_OUTSIDE", 1_000, 5, skillBar: false)),
            "The player Poison timer positions or active affect could not be initialized.");

        var runtime = new LegacyPlayerAffectRuntime(hub, (outcome, token) =>
            LegacyPlayerAffectRelay.RelayAsync(hub, outcome, codec, token));
        var expired = await runtime.TickShardAsync(1);
        Check(expired.Count == 1 && expired[0].Hp == 400 && expired[0].Damage == -500 &&
              expired[0].HpUpdateRequired && expired[0].ScoreRefreshRequired &&
              expired[0].AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The matching player shard did not apply the 7.69 Poison cap and expire the Type 20 affect.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var hpFrame = await ReadFrameAsync(viewerStream, codec);
            Check(hpFrame.Header.Type == SetHpDamConfirmation.MessageType && hpFrame.Header.Id == 1 &&
                  hpFrame.Header.Size == SetHpDamConfirmation.PacketSize && hpFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span) == 400 &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span[4..]) == -500,
                "An in-view peer did not receive the signed player Poison SetHpDam before UpdateScore.");

            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType && scoreFrame.Header.Id == 1 &&
                  scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize && scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 400 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0,
                "An in-view peer did not receive the final player Poison score with the expired affect cleared.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task LifeAuraFramesRelayOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        const short npcX = 10;
        const short npcY = 10;
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "LIFE_AURA_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "LIFE_AURA_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "LIFE_AURA_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The Life Aura loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("LIFE_TARGET", 1_000, 5, false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("LIFE_VIEW", 1_000, 5, false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 27, positionY: 26, CreateMob("LIFE_OUTSIDE", 1_000, 5, false)),
            "The Life Aura loopback peer positions could not be initialized.");

        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 17;
        affect[1] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        const int npcId = 20_002;
        var actualNpcId = hub.EnterNpc(
            CreateMob("LIFE_AURA", 900, 2, skillBar: true),
            npcX,
            npcY,
            affect,
            requestedConnectionId: npcId,
            generateIndex: 42);
        Check(actualNpcId == npcId &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Idle, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Idle Life Aura NPC could not be registered.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickNextShardAsync();
        Check(tick.PeaceShardIndex == 0 && tick.VisitedPeaceNpcIds.Contains(npcId) &&
              hub.TryGetNpcSnapshot(npcId, out var updatedNpc) && updatedNpc is not null &&
              LegacyMobCombatState.Read(updatedNpc.MobSnapshot).CurrentScore.Hp == 901 &&
              BinaryPrimitives.ReadUInt32LittleEndian(updatedNpc.AffectSnapshot.AsSpan(4)) == 0,
            "The Peace timer did not apply the Idle NPC Life Aura before the other shard work.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var hpFrame = await ReadFrameAsync(viewerStream, codec);
            Check(hpFrame.Header.Type == SetHpDamConfirmation.MessageType && hpFrame.Header.Id == npcId &&
                  hpFrame.Header.Size == SetHpDamConfirmation.PacketSize && hpFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span) == 901 &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span[4..]) == 1,
                "An in-view peer did not receive Life Aura SetHpDam with the current HP and actual delta.");

            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 901 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0,
                "An in-view peer did not receive the final 7.69 score after Life Aura expired.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);

    }

    private static async Task PoisonAffectFramesRelayOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "POISON_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "POISON_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "POISON_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The poison loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("POISON_TARGET", 1_000, 5, false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("POISON_VIEW", 1_000, 5, false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 27, positionY: 26, CreateMob("POISON_OUTSIDE", 1_000, 5, false)),
            "The poison loopback peer positions could not be initialized.");

        var mob = CreateMob("POISON_AFFECT", 10_000, 2, skillBar: true);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0);
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 20;
        affect[1] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        const int npcId = 20_002;
        var actualNpcId = hub.EnterNpc(mob, 10, 10, affect, requestedConnectionId: npcId, generateIndex: 42);
        Check(actualNpcId == npcId &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Idle, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Idle poison NPC could not be registered.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickNextShardAsync();
        Check(tick.PeaceShardIndex == 0 && tick.VisitedPeaceNpcIds.Contains(npcId) &&
              hub.TryGetNpcSnapshot(npcId, out var updatedNpc) && updatedNpc is not null &&
              LegacyMobCombatState.Read(updatedNpc.MobSnapshot).CurrentScore.Hp == 9_000 &&
              updatedNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The Peace timer did not apply the generated Idle NPC's Type 20 damage and expiry.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var hpFrame = await ReadFrameAsync(viewerStream, codec);
            Check(hpFrame.Header.Type == SetHpDamConfirmation.MessageType && hpFrame.Header.Id == npcId &&
                  hpFrame.Header.Size == SetHpDamConfirmation.PacketSize && hpFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span) == 9_000 &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span[4..]) == -1_000,
                "An in-view peer did not receive the signed Type 20 SetHpDam delta.");

            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 9_000 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0,
                "An in-view peer did not receive the final 7.69 score after Type 20 expired.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task CombatNpcAffectFramesRelayOnlyToCapturedViewWithoutTargetsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "COMBAT_AFFECT_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "COMBAT_AFFECT_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "COMBAT_AFFECT_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The Combat affect loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("CMB_AFF_TARGET", 1_000, 5, false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("CMB_AFF_VIEW", 1_000, 5, false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 27, positionY: 26, CreateMob("CMB_AFF_OUT", 1_000, 5, false)),
            "The Combat affect loopback peer positions could not be initialized.");

        var lifeAura = new byte[LegacyAccountSnapshot.AffectStride];
        lifeAura[0] = 17;
        lifeAura[1] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(lifeAura.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(lifeAura.AsSpan(4), 1);
        const int lifeAuraNpcId = 20_002;
        Check(hub.EnterNpc(
                  CreateMob("COMBAT_LIFE_AURA", 900, 2, true), 10, 10, lifeAura,
                  requestedConnectionId: lifeAuraNpcId, generateIndex: 42) == lifeAuraNpcId &&
              hub.SetNpcCombatState(lifeAuraNpcId, LegacyNpcMode.Combat, 0,
                  new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Combat Life Aura NPC could not be registered without a target.");

        var poisonMob = CreateMob("COMBAT_POISON", 10_000, 2, true);
        BinaryPrimitives.WriteInt32LittleEndian(
            poisonMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0);
        var poison = new byte[LegacyAccountSnapshot.AffectStride];
        poison[0] = 20;
        poison[1] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(poison.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(poison.AsSpan(4), 1);
        const int poisonNpcId = 20_006;
        Check(hub.EnterNpc(
                  poisonMob, 10, 10, poison,
                  requestedConnectionId: poisonNpcId, generateIndex: 42) == poisonNpcId &&
              hub.SetNpcCombatState(poisonNpcId, LegacyNpcMode.Combat, 0,
                  new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Combat Poison NPC could not be registered without a target.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        for (var tickIndex = 0; tickIndex < 16; tickIndex++)
        {
            var tick = await runtime.TickNextShardAsync();
            Check(tick.Attacks.Count == 0,
                "Combat NPC affects without an enemy list must not synthesize an attack.");
        }

        Check(hub.TryGetNpcSnapshot(lifeAuraNpcId, out var lifeAuraAfter) && lifeAuraAfter is not null &&
              LegacyMobCombatState.Read(lifeAuraAfter.MobSnapshot).CurrentScore.Hp == 901 &&
              lifeAuraAfter.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]) &&
              hub.TryGetNpcSnapshot(poisonNpcId, out var poisonAfter) && poisonAfter is not null &&
              LegacyMobCombatState.Read(poisonAfter.MobSnapshot).CurrentScore.Hp == 9_000 &&
              poisonAfter.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The Combat timer did not apply and expire Life Aura and Poison for targetless generated NPCs.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var frames = new List<DecodedFrame>();
            for (var frameIndex = 0; frameIndex < 4; frameIndex++)
                frames.Add(await ReadFrameAsync(viewerStream, codec));

            var hpFrames = frames.Where(frame => frame.Header.Type == SetHpDamConfirmation.MessageType)
                .ToDictionary(static frame => (int)frame.Header.Id);
            var scoreFrames = frames.Where(frame => frame.Header.Type == UpdateScoreConfirmationV769.MessageType)
                .ToDictionary(static frame => (int)frame.Header.Id);
            var framePairsPreserveOrder = frames.GroupBy(static frame => frame.Header.Id).All(static group =>
            {
                var pair = group.ToArray();
                return pair.Length == 2 &&
                       pair[0].Header.Type == SetHpDamConfirmation.MessageType &&
                       pair[1].Header.Type == UpdateScoreConfirmationV769.MessageType;
            });
            Check(hpFrames.Count == 2 && scoreFrames.Count == 2 &&
                  hpFrames.Keys.ToHashSet().SetEquals([lifeAuraNpcId, poisonNpcId]) &&
                  scoreFrames.Keys.ToHashSet().SetEquals([lifeAuraNpcId, poisonNpcId]) &&
                  framePairsPreserveOrder,
                "Each in-view peer must receive SetHpDam then UpdateScore for both Combat affects.");

            var lifeHp = hpFrames[lifeAuraNpcId];
            var poisonHp = hpFrames[poisonNpcId];
            Check(lifeHp.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(lifeHp.Payload.Span) == 901 &&
                  BinaryPrimitives.ReadInt32LittleEndian(lifeHp.Payload.Span[4..]) == 1 &&
                  poisonHp.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(poisonHp.Payload.Span) == 9_000 &&
                  BinaryPrimitives.ReadInt32LittleEndian(poisonHp.Payload.Span[4..]) == -1_000,
                "Combat Life Aura and Poison SetHpDam frames did not preserve their signed HP deltas.");

            Check(scoreFrames.Values.All(static frame => frame.IsChecksumValid &&
                      frame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0) &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrames[lifeAuraNpcId].Payload.Span[24..]) == 901 &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrames[poisonNpcId].Payload.Span[24..]) == 9_000,
                "Combat affect expiry did not relay the final score and cleared affect state.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task CombatThunderExpiryRelaysOnlyScoreToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "THUNDER_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "THUNDER_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "THUNDER_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The Combat Thunder loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("THUNDER_TARGET", 1_000, 5, false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("THUNDER_VIEW", 1_000, 5, false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 27, positionY: 26, CreateMob("THUNDER_OUTSIDE", 1_000, 5, false)),
            "The Combat Thunder loopback peer positions could not be initialized.");

        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 22;
        affect[1] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 2);
        const int npcId = 20_002;
        Check(hub.EnterNpc(
                  CreateMob("COMBAT_THUNDER", 1_000, 1, true), 10, 10, affect,
                  requestedConnectionId: npcId, generateIndex: 42) == npcId &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0,
                  new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Combat Thunder NPC could not be registered without a target.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        for (var tickIndex = 0; tickIndex < 31; tickIndex++)
        {
            var tick = await runtime.TickNextShardAsync();
            Check(tick.Attacks.Count == 0,
                "Type 22 on a generated NPC must consume its timer without synthesizing a lightning attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var expiredNpc) && expiredNpc is not null &&
              LegacyMobCombatState.Read(expiredNpc.MobSnapshot).CurrentScore.Hp == 1_000 &&
              expiredNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The Combat Type 22 timer did not expire without changing the generated NPC's HP.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 1_000 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0,
                "Type 22 expiry must relay UpdateScore without a preceding SetHpDam to in-view peers.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task NonGeneratedNpcAreaAffectExpiryRelaysOnlyScoreToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec,
        byte affectType,
        string marker)
    {
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "ST22_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "ST22_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "ST22_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            $"The non-generated Type {affectType} loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("ST22_TARGET", 1_000, 5, false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("ST22_VIEW", 1_000, 5, false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 27, positionY: 26, CreateMob("ST22_OUT", 1_000, 5, false)),
            $"The non-generated Type {affectType} loopback peer positions could not be initialized.");

        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = affectType;
        affect[1] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 2);
        const int npcId = 20_002;
        Check(hub.EnterNpc(
                  CreateMob("STATIC_T22_NPC", 1_000, 1, true), 10, 10, affect,
                  requestedConnectionId: npcId) == npcId,
            $"The ungenerated Peace NPC Type {affectType} fixture could not be registered.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = await runtime.TickNextShardAsync();
            Check(tick.Attacks.Count == 0,
                $"An ungenerated NPC Type {affectType} timer must not synthesize an area attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterCountdown) && afterCountdown is not null &&
              LegacyMobCombatState.Read(afterCountdown.MobSnapshot).CurrentScore.Hp == 1_000 &&
              BinaryPrimitives.ReadUInt32LittleEndian(afterCountdown.AffectSnapshot.AsSpan(4)) == 1,
            $"The ungenerated NPC Type {affectType} timer did not decrement on its Peace shard visit.");
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);

        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = await runtime.TickNextShardAsync();
            Check(tick.Attacks.Count == 0,
                $"An ungenerated NPC Type {affectType} expiry must remain score-only, without an attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var afterExpiry) && afterExpiry is not null &&
              LegacyMobCombatState.Read(afterExpiry.MobSnapshot).CurrentScore.Hp == 1_000 &&
              afterExpiry.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            $"The ungenerated NPC Type {affectType} expiry did not clear the affect without changing HP.");
        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 1_000 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0,
                $"Type {affectType} expiry must relay only UpdateScore to each in-view peer.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine(marker);
    }

    private static async Task CancelamentoNpcType32RelaysScoreToNpcViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "CANCELAMENTO_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "CANCELAMENTO_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "CANCELAMENTO_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Cancelamento loopback peers could not be registered.");

        Check(world.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, CreateMob("CXL_ACT", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_017, positionY: 2_016, CreateMob("CXL_VIEW", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("CXL_OUT", 1_000, 5, skillBar: false)),
            "Cancelamento loopback player state could not be initialized.");

        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        Check(world.EnterNpc(
                  CreateMob("CXL_NPC", 1_000, 1, skillBar: false),
                  2_001,
                  2_000,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  requestedConnectionId: npcId) == npcId,
            "Cancelamento loopback NPC 1000 could not be registered.");

        var cancelamento = new LegacySkillDefinition(
            47, 269, 5, 34, 30, 5, 0, 0, 0, 0, 32, 0, 1,
            [], [], 0, 0, 1, 5, 0, 0, 0, 0, "Cancelamento");
        Check(world.TryApplySkillAttack(
                  1, npcId, cancelamento, itemData: null, weather: 0, randomFactor: -1, out var outcome) ==
              LegacySkillAttackResult.Accepted && outcome?.TargetAffectSnapshot is { } updatedAffect && updatedAffect[0] == 32,
            "Cancelamento did not produce the NPC Type 32 score update.");

        await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
            world, outcome!, codec, clientTick: 17, keywordIndex: 3, CancellationToken.None);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  (BinaryPrimitives.ReadUInt16LittleEndian(
                      scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 32,
                "An in-view peer did not receive the NPC Type 32 UpdateScore frame.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("CANCELAMENTO_NPC_TYPE32_SMOKE_PASS score-view-scoped=true");
    }

    private static async Task EnfraquecerNpcType10RelaysScoreToNpcViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "WEAKEN_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "WEAKEN_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "WEAKEN_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Enfraquecer loopback peers could not be registered.");

        Check(world.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, CreateMob("WKN_ATK", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_017, positionY: 2_016, CreateMob("WKN_VIEW", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("WKN_OUT", 1_000, 5, skillBar: false)),
            "Enfraquecer loopback player state could not be initialized.");

        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var npcMob = CreateMob("WKN_NPC", 1_000, 1, skillBar: false);
        var initialDamage = LegacyMobCombatState.Read(npcMob).CurrentScore.Damage;
        Check(world.EnterNpc(
                  npcMob,
                  2_001,
                  2_000,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  requestedConnectionId: npcId) == npcId,
            "Enfraquecer loopback NPC 1000 could not be registered.");

        var weaken = new LegacySkillDefinition(
            51, 45, 3, 35, 2, 6, 0, 0, 0, 0, 10, 10, 1,
            [6, 0, 0, 8, 0, 0, 0, 0], [10, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 1, 13, 0, 0, 0, 0, "Enfraquecer");
        Check(world.TryApplySkillAttack(
                  1, npcId, weaken, itemData: null, weather: 0, randomFactor: -1, out var outcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              outcome?.TargetAffectSnapshot is { } updatedAffect && updatedAffect[0] == 10,
            "Enfraquecer did not produce a Type 10 NPC score update.");

        await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
            world, outcome!, codec, clientTick: 18, keywordIndex: 4, CancellationToken.None);

        var reduction = LegacyCurrentScoreMath.CalculateWeakenAffectStage(outcome!.TargetAffectSnapshot!).DamageReduction;
        var expectedDamage = initialDamage - reduction;
        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  (BinaryPrimitives.ReadUInt16LittleEndian(
                      scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 10 &&
                  BinaryPrimitives.ReadInt32LittleEndian(
                      scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 8)..]) == expectedDamage,
                "An in-view peer did not receive the Type 10 NPC UpdateScore with reduced damage.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("ENFRAQUECER_NPC_TYPE10_SMOKE_PASS damage-view-scoped=true");
    }

    private static async Task PerseguicaoNpcType3RelaysScoreToNpcViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "PURSUICAO_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "PURSUICAO_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "PURSUICAO_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Perseguição loopback peers could not be registered.");

        var attackerMob = CreateMob("PUR_ATK", 1_000, 5, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 1_500,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_017, positionY: 2_016, CreateMob("PUR_VIEW", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("PUR_OUT", 1_000, 5, skillBar: false)),
            "Perseguição loopback player state could not be initialized.");

        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var npcMob = CreateMob("PUR_NPC", 1_000, 1, skillBar: false);
        npcMob[LegacyAccountSnapshot.MobResistOffset] = 80;
        npcMob[LegacyAccountSnapshot.MobResistOffset + 1] = 90;
        npcMob[LegacyAccountSnapshot.MobResistOffset + 2] = 100;
        npcMob[LegacyAccountSnapshot.MobResistOffset + 3] = 40;
        BinaryPrimitives.WriteUInt16LittleEndian(
            npcMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset), 50);
        Check(world.EnterNpc(
                  npcMob,
                  2_001,
                  2_000,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  requestedConnectionId: npcId) == npcId,
            "Perseguição loopback NPC 1000 could not be registered.");

        var pursuit = new LegacySkillDefinition(
            16, 24, 3, 25, 2, 5, 0, 0, 0, 0, 3, 60, 0,
            new byte[8], new byte[8], 2, 0, 1, 13, 0, 2, 0, 0, "Perseguição");
        Check(world.TryApplySkillAttack(
                  1, npcId, pursuit, itemData: null, weather: 0, randomFactor: -1, out var outcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              outcome?.TargetAffectSnapshot is { } updatedAffect && updatedAffect[0] == 3 &&
              outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(new byte[] { 30, 40, 50, 0 }),
            "Perseguição did not produce the expected Type 3 NPC resistance update.");

        await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
            world, outcome!, codec, clientTick: 19, keywordIndex: 5, CancellationToken.None);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  (BinaryPrimitives.ReadUInt16LittleEndian(
                      scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 3,
                "An in-view peer did not receive the Type 3 NPC UpdateScore frame.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("PERSEGUICAO_NPC_TYPE3_SMOKE_PASS resist-view-scoped=true");
    }

    private static async Task ToqueSagradoNpcType1RelaysScoreToNpcViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "HOLY_TOUCH_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "HOLY_TOUCH_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "HOLY_TOUCH_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Toque Sagrado loopback peers could not be registered.");

        var attackerMob = CreateMob("HOLY_ATK", 1_000, 5, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 1_500,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_017, positionY: 2_016, CreateMob("HOLY_VIEW", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("HOLY_OUT", 1_000, 5, skillBar: false)),
            "Toque Sagrado loopback player state could not be initialized.");

        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var npcMob = CreateMob("HOLY_NPC", 1_000, 1, skillBar: false);
        var npcScore = LegacyScore.Read(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Intelligence = 100,
        };
        npcScore.Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(51, 0, 0, 0, 0, 0, 0).Write(
            npcMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        Check(world.EnterNpc(
                  npcMob,
                  2_001,
                  2_000,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  requestedConnectionId: npcId) == npcId,
            "Toque Sagrado loopback NPC 1000 could not be registered.");

        var holyTouch = new LegacySkillDefinition(
            1, 48, 4, 20, 3, 0, 0, 0, 0, 0, 1, 2, 0,
            [10, 0, 0, 10, 0, 0, 0, 0], [10, 0, 0, 10, 0, 0, 0, 0],
            2, 0, 1, 13, 0, 2, 0, 0, "Toque_Sagrado");
        Check(world.TryApplySkillAttack(
                  1, npcId, holyTouch, itemData: null, weather: 0, randomFactor: -1, out var outcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              outcome?.TargetAffectSnapshot is { } updatedAffect && updatedAffect[0] == 1 && updatedAffect[1] == 2 &&
              LegacyScore.Read(outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 60 },
            "Toque Sagrado did not project its Type 1 NPC score update.");

        await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
            world, outcome!, codec, clientTick: 20, keywordIndex: 6, CancellationToken.None);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  (BinaryPrimitives.ReadUInt16LittleEndian(
                      scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 1 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.ScoreOffset + 13] == 0x64 &&
                  BinaryPrimitives.ReadInt16LittleEndian(
                      scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 34)..]) == 60,
                "An in-view peer did not receive the Type 1 NPC UpdateScore with the projected AttackRun and Intelligence.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("TOQUE_SAGRADO_NPC_TYPE1_SMOKE_PASS attackrun-view-scoped=true");
    }

    private static async Task RsvFrostPhysicalNpcType1RelaysScoreToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        using var skillReader = new StringReader(
            "36,72,1,35,5,6,3,200,0,0,1,2,1,6.0.0.7.0.0.0.0,6.0.0.7.0.0.0.0,2,0,1,1,0,2,0,1,Nevasca");
        var world = new WorldHub(skillData: LegacySkillDataTable.Load(skillReader));
        Check(world.Enter(1, "RSV_FROST_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "RSV_FROST_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "RSV_FROST_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The RSV_FROST physical attack loopback peers could not be registered.");

        var attackerMob = CreateMob("RSV_FROST_ATK", 1_000, 5, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Special2 = 40,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x01);
        Check(world.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_017, positionY: 2_016, CreateMob("RSV_FROST_VIEW", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("RSV_FROST_OUT", 1_000, 5, skillBar: false)),
            "The RSV_FROST physical attack peer state could not be initialized.");

        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var npcMob = CreateMob("RSV_FROST_NPC", 100_000, 1, skillBar: false);
        var npcScore = LegacyScore.Read(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Intelligence = 100,
        };
        npcScore.Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(51, 0, 0, 0, 0, 0, 0).Write(
            npcMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        Check(world.EnterNpc(
                  npcMob,
                  2_001,
                  2_000,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  requestedConnectionId: npcId) == npcId,
            "The RSV_FROST physical attack NPC 1000 could not be registered.");

        Check(world.TryApplyPhysicalAttack(
                  1, npcId, randomFactor: 99, out var outcome,
                  attackMissRoll: 99, frostRoll: 0, drainRoll: 1) == LegacyPhysicalAttackResult.Accepted &&
              outcome is { Damage: > 0, TargetAffectSnapshot: { } affect } && affect[0] == 1 &&
              outcome.TargetAffectViewRecipients.SequenceEqual([1, 2]),
            "RSV_FROST did not apply Type 1 and capture the NPC's two in-view peers.");

        await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
            world, outcome!, codec, clientTick: 22, keywordIndex: 8, CancellationToken.None);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  (BinaryPrimitives.ReadUInt16LittleEndian(
                      scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 1 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.ScoreOffset + 13] == 0x64 &&
                  BinaryPrimitives.ReadInt16LittleEndian(
                      scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 34)..]) == 60,
                "An in-view peer did not receive RSV_FROST's Type 1 NPC UpdateScore.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("RSV_FROST_NPC_TYPE1_SMOKE_PASS attackrun-view-scoped=true");
    }

    private static async Task RsvFrostSkillNpcType1RelaysScoreToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        using var skillReader = new StringReader(
            "36,72,1,35,5,6,3,200,0,0,1,2,1,6.0.0.7.0.0.0.0,6.0.0.7.0.0.0.0,2,0,1,1,0,2,0,1,Nevasca\n" +
            "40,24,1,9,2,6,4,20,20,10,0,0,1,10.0.0.9.0.0.0.0,10.0.0.9.0.0.0.0,4,0,1,1,0,0,0,1,Neblina_Venenosa");
        var world = new WorldHub(skillData: LegacySkillDataTable.Load(skillReader));
        Check(world.Enter(1, "FROST_SKILL_TGT", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "FROST_SKILL_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "FROST_SKILL_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The RSV_FROST skill-hit loopback peers could not be registered.");

        var attackerMob = CreateMob("FRST_ATK", 1_000, 5, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Special2 = 40,
            Intelligence = 100,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x01);
        Check(world.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_017, positionY: 2_016, CreateMob("FRST_VIEW", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("FRST_OUT", 1_000, 5, skillBar: false)),
            "The RSV_FROST skill-hit peer state could not be initialized.");

        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var npcMob = CreateMob("FRST_NPC", 100_000, 1, skillBar: false);
        var npcScore = LegacyScore.Read(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Intelligence = 100,
        };
        npcScore.Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(51, 0, 0, 0, 0, 0, 0).Write(
            npcMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        Check(world.EnterNpc(
                  npcMob,
                  2_001,
                  2_000,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  requestedConnectionId: npcId) == npcId,
            "The RSV_FROST skill-hit NPC 1000 could not be registered.");

        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        var damageSkill = new LegacySkillDefinition(
            2, 66, 1, 10, 3, 3, 1, 13, 0, 0, 0, 0, 0,
            [7, 0, 0, 7, 0, 0, 0, 0], [7, 0, 0, 7, 0, 0, 0, 0],
            0, 0, 1, 2, 0, 0, 0, 1, "Golpe_Duplo");
        Check(world.TryApplySkillAttack(
                  1, npcId, damageSkill, itemData, weather: 0, randomFactor: 100, out var outcome,
                  attackMissRoll: 99, frostRoll: 0, drainRoll: 1) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: > 0, TargetAffectSnapshot: { } affected } && affected[0] == 1 && affected[1] == 2 &&
              LegacyScore.Read(outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 60 },
            "A positive skill hit with RSV_FROST did not project Type 1 onto the NPC.");

        await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
            world, outcome!, codec, clientTick: 23, keywordIndex: 8, CancellationToken.None);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  (BinaryPrimitives.ReadUInt16LittleEndian(
                      scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 1 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.ScoreOffset + 13] == 0x64 &&
                  BinaryPrimitives.ReadInt16LittleEndian(
                      scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 34)..]) == 60,
                "An in-view peer did not receive RSV_FROST's skill-hit Type 1 NPC UpdateScore.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("RSV_FROST_SKILL_NPC_TYPE1_SMOKE_PASS attackrun-view-scoped=true");
    }

    private static async Task RsvFrostPhysicalPlayerType1RelaysScoreToTargetAsync(
        NetworkStream attackerSendStream,
        NetworkStream targetSendStream,
        NetworkStream outsideSendStream,
        NetworkStream attackerReadStream,
        NetworkStream targetReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        using var skillReader = new StringReader(
            "36,72,1,35,5,6,3,200,0,0,1,2,1,6.0.0.7.0.0.0.0,6.0.0.7.0.0.0.0,2,0,1,1,0,2,0,1,Nevasca");
        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        var world = new WorldHub(itemData: itemData, skillData: LegacySkillDataTable.Load(skillReader));
        Check(world.Enter(1, "RSV_FROST_PLAYER_ATTACKER", (bytes, token) => attackerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "RSV_FROST_PLAYER_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "RSV_FROST_PLAYER_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The RSV_FROST player attack loopback peers could not be registered.");

        var attackerMob = CreateMob("FROST_ATK", 1_000, 5, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Special2 = 40,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(attackerMob.AsSpan(LegacyAccountSnapshot.MobRsvOffset), 0x01);

        var targetMob = CreateMob("FROST_TARGET", 1_000, 5, skillBar: false);
        var targetScore = LegacyScore.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Intelligence = 100,
        };
        targetScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        targetScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(51, 0, 0, 0, 0, 0, 0).Write(
            targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_005, positionY: 2_005, targetMob) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("FROST_OUT", 1_000, 5, skillBar: false)),
            "The RSV_FROST player peer state could not be initialized.");

        Check(world.TryApplyPhysicalAttack(
                  1, 2, randomFactor: 99, out var outcome,
                  attackMissRoll: 99, itemData: itemData, frostRoll: 0, drainRoll: 1) == LegacyPhysicalAttackResult.Accepted &&
              outcome is { Damage: > 0, TargetAffectSnapshot: { } affect } && affect[0] == 1 &&
              LegacyScore.Read(outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 60 },
            "RSV_FROST did not apply Type 1 and project the player's score.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var sourceScorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                sourceScorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 24, keywordIndex: 9, id: 2);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 2, clientTick: 24, keywordIndex: 10);
        await world.SendAsync(2, scoreFrame, CancellationToken.None);
        await world.SendAsync(2, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(targetReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 2 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 1 &&
              receivedScore.Payload.Span[UpdateScoreConfirmationV769.ScoreOffset + 13] == 0x64 &&
              BinaryPrimitives.ReadInt16LittleEndian(
                  receivedScore.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 34)..]) == 60,
            "The target player did not receive the projected Type 1 UpdateScore.");

        var receivedAffect = await ReadFrameAsync(targetReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 2 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 1,
            "The target player did not receive the Type 1 UpdateAffect.");

        await AssertNoFrameAsync(attackerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("RSV_FROST_PLAYER_TYPE1_SMOKE_PASS score-affect-target-only=true");
    }

    private static async Task Type1DamageSkillPlayerScoreRelaysToTargetAsync(
        NetworkStream attackerSendStream,
        NetworkStream targetSendStream,
        NetworkStream outsideSendStream,
        NetworkStream attackerReadStream,
        NetworkStream targetReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec,
        LegacySkillDefinition skill)
    {
        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "TYPE1_SKILL_PLAYER_ATTACKER", (bytes, token) => attackerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "TYPE1_SKILL_PLAYER_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "TYPE1_SKILL_PLAYER_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            $"Skill {skill.Id} player-target loopback peers could not be registered.");

        var attackerMob = CreateMob("TYPE1_SKILL_ATK", 1_000, 1, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 1_500,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

        var targetMob = CreateMob("T1_SKILL_TARGET", 100_000, 2, skillBar: false);
        targetMob[LegacyAccountSnapshot.MobClassOffset] = 1;
        var targetScore = LegacyScore.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 50,
            AttackRun = 0x96,
            Intelligence = 100,
        };
        targetScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        targetScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(51, 0, 0, 0, 0, 0, 0).Write(
            targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_005, positionY: 2_005, targetMob) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("TYPE1_SKILL_OUT", 1_000, 3, skillBar: false)),
            $"Skill {skill.Id} player-target loopback state could not be initialized.");

        Check(world.TryApplySkillAttack(
                  1, 2, skill, itemData, weather: 0, randomFactor: 100, out var outcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: > 0, TargetAffectSnapshot: { } affect } && affect[0] == 1 &&
              LegacyScore.Read(outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 60 },
            $"Skill {skill.Id} did not project Type 1 on the player-target outcome.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                LegacyMobCombatState.Read(outcome.TargetMobSnapshot).Rsv)
            .ToFrame(codec, clientTick: 25, keywordIndex: 11, id: 2);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 2, clientTick: 25, keywordIndex: 12);
        await world.SendAsync(2, scoreFrame, CancellationToken.None);
        await world.SendAsync(2, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(targetReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 2 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 1 &&
              receivedScore.Payload.Span[UpdateScoreConfirmationV769.ScoreOffset + 13] == 0x64 &&
              BinaryPrimitives.ReadInt16LittleEndian(
                  receivedScore.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 34)..]) == 60,
            $"The player target did not receive skill {skill.Id}'s projected Type 1 UpdateScore.");

        var receivedAffect = await ReadFrameAsync(targetReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 2 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 1,
            $"The player target did not receive skill {skill.Id}'s Type 1 UpdateAffect.");

        await AssertNoFrameAsync(attackerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine($"{skill.Name.ToUpperInvariant()}_PLAYER_TYPE1_SMOKE_PASS score-affect-target-only=true");
    }

    private static async Task PursuicaoPlayerScoreRelaysToTargetAsync(
        NetworkStream attackerSendStream,
        NetworkStream targetSendStream,
        NetworkStream outsideSendStream,
        NetworkStream attackerReadStream,
        NetworkStream targetReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "PURSUIT_SKILL_ATTACKER", (bytes, token) => attackerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "PURSUIT_SKILL_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "PURSUIT_SKILL_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Perseguição player-target loopback peers could not be registered.");

        var attackerMob = CreateMob("PURSUIT_ATK", 1_000, 1, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 2_500,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

        var targetMob = CreateMob("PURSUIT_TARGET", 100_000, 2, skillBar: false);
        targetMob[LegacyAccountSnapshot.MobClassOffset] = 1;
        var targetScore = LegacyScore.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 2_000,
        };
        targetScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        targetScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new byte[] { 80, 90, 100, 60 }.CopyTo(targetMob.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4));
        new LegacyItem(51, 0, 0, 0, 0, 0, 0).Write(
            targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_005, positionY: 2_005, targetMob) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("PURSUIT_OUT", 1_000, 3, skillBar: false)),
            "Perseguição player-target loopback state could not be initialized.");

        var skill = PursuicaoSkill();
        var attackResult = world.TryApplySkillAttack(
            1, 2, skill, itemData, weather: 0, randomFactor: 100, out var outcome,
            affectRandomFactor: 0);
        var outcomeAffectType = outcome?.TargetAffectSnapshot is { Length: > 0 } affectSnapshot
            ? affectSnapshot[0]
            : -1;
        var outcomeResistances = outcome is null
            ? "none"
            : string.Join(',', outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4).ToArray());
        Check(attackResult == LegacySkillAttackResult.Accepted &&
              outcome is { TargetAffectSnapshot: { } affect } && affect[0] == 3 &&
              outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobResistOffset, 4)
                  .SequenceEqual(new byte[] { 30, 40, 50, 10 }),
            $"Perseguição did not project Type 3 onto the player-target outcome: result={attackResult}, damage={outcome?.Damage}, hp={outcome?.RemainingHp}, affectType={outcomeAffectType}, resist={outcomeResistances}.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 25, keywordIndex: 21, id: 2);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 2, clientTick: 25, keywordIndex: 22);
        await world.SendAsync(2, scoreFrame, CancellationToken.None);
        await world.SendAsync(2, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(targetReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 2 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 3 &&
              receivedScore.Payload.Span.Slice(UpdateScoreConfirmationV769.ResistOffset, 4)
                  .SequenceEqual(new byte[] { 30, 40, 50, 10 }),
            "The target player did not receive Perseguição's projected Type 3 UpdateScore.");

        var receivedAffect = await ReadFrameAsync(targetReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 2 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 3,
            "The target player did not receive Perseguição's Type 3 UpdateAffect.");

        await AssertNoFrameAsync(attackerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("PERSEGUICAO_PLAYER_TYPE3_SMOKE_PASS score-affect-target-only=true");
    }

    private static async Task LaminaCongelantePlayerType7RelaysScoreToTargetAsync(
        NetworkStream attackerSendStream,
        NetworkStream targetSendStream,
        NetworkStream outsideSendStream,
        NetworkStream attackerReadStream,
        NetworkStream targetReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "FROZ_ATK", (bytes, token) => attackerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "FROZ_TGT", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "FROZ_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Lamina Congelante player-target loopback peers could not be registered.");

        var attackerMob = CreateMob("FROZEN_ATK", 1_000, 1, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 1_500,
            Special4 = 50,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

        var targetMob = CreateMob("FROZEN_TGT", 100_000, 2, skillBar: false);
        targetMob[LegacyAccountSnapshot.MobClassOffset] = 1;
        var targetScore = LegacyScore.Read(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 50,
            AttackRun = 0xA6,
            Intelligence = 100,
        };
        targetScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        targetScore.Write(targetMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(51, 0, 0, 0, 0, 0, 0).Write(
            targetMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));

        Check(world.SetCharacterState(1, LegacyAccountSnapshot.ClassMasterMortal, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_005, positionY: 2_005, targetMob) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("FROZEN_OUT", 1_000, 3, skillBar: false)),
            "Lamina Congelante player-target loopback state could not be initialized.");

        var laminaCongelante = new LegacySkillDefinition(
            19, 78, 1, 22, 4, 5, 3, 50, 0, 0, 7, 1, 0,
            [8, 0, 0, 7, 0, 0, 0, 0], [8, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 1, 0, 2, 0, 1, "Lamina_Congelante");
        Check(world.TryApplySkillAttack(
                  1, 2, laminaCongelante, itemData, weather: 0, randomFactor: 100, out var outcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: > 0, TargetAffectSnapshot: { } affect } && affect[0] == 7 && affect[1] == 1 &&
              LegacyScore.Read(outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x86, Intelligence: 75 },
            "Lamina Congelante did not project Type 7 on the player-target outcome.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 25, keywordIndex: 23, id: 2);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 2, clientTick: 25, keywordIndex: 24);
        await world.SendAsync(2, scoreFrame, CancellationToken.None);
        await world.SendAsync(2, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(targetReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 2 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 7 &&
              receivedScore.Payload.Span[UpdateScoreConfirmationV769.ScoreOffset + 13] == 0x86 &&
              BinaryPrimitives.ReadInt16LittleEndian(
                  receivedScore.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 34)..]) == 75,
            "The target player did not receive the Lamina Congelante Type 7 UpdateScore.");

        var receivedAffect = await ReadFrameAsync(targetReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 2 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 7,
            "The target player did not receive the Lamina Congelante Type 7 UpdateAffect.");

        await AssertNoFrameAsync(attackerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("LAMINA_CONGELANTE_PLAYER_TYPE7_SMOKE_PASS score-affect-target-only=true");
    }

    private static async Task EnfraquecerPlayerType10RelaysScoreToTargetAsync(
        NetworkStream attackerSendStream,
        NetworkStream targetSendStream,
        NetworkStream outsideSendStream,
        NetworkStream attackerReadStream,
        NetworkStream targetReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "WEAKEN_PLAYER_ATTACKER", (bytes, token) => attackerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "WEAKEN_PLAYER_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "WEAKEN_PLAYER_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Enfraquecer player-target loopback peers could not be registered.");

        var attackerMob = CreateMob("WEAKEN_ATK", 1_000, 1, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Special2 = 50,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));

        var targetMob = CreateMob("WEAKEN_TGT", 100_000, 2, skillBar: false);
        targetMob[LegacyAccountSnapshot.MobClassOffset] = 1;
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                targetMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                new byte[LegacyAccountSnapshot.AffectStride],
                new byte[LegacyAccountSnapshot.MobExtraStride],
                0,
                0,
                0),
            itemData);
        var baselineDamage = LegacyMobCombatState.Read(baseline.Mob).CurrentScore.Damage;

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, targetMob) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("WEAKEN_OUT", 1_000, 3, skillBar: false)),
            "Enfraquecer player-target loopback state could not be initialized.");

        var weaken = new LegacySkillDefinition(
            51, 45, 3, 35, 2, 6, 0, 0, 0, 0, 10, 10, 1,
            [6, 0, 0, 8, 0, 0, 0, 0], [10, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 1, 13, 0, 0, 0, 0, "Enfraquecer");
        Check(world.TryApplySkillAttack(
                  1, 2, weaken, itemData, weather: 0, randomFactor: 100, out var outcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 10 && affect[1] == 10 &&
              LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore.Damage == baselineDamage - 20,
            "Enfraquecer did not apply Type 10 and reduce the player score by the 7.69 value.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 25, keywordIndex: 25, id: 2);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 2, clientTick: 25, keywordIndex: 26);
        await world.SendAsync(2, scoreFrame, CancellationToken.None);
        await world.SendAsync(2, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(targetReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 2 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 10 &&
              BinaryPrimitives.ReadInt32LittleEndian(
                  receivedScore.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 8)..]) == baselineDamage - 20,
            "The target player did not receive Enfraquecer's Type 10 UpdateScore with reduced damage.");

        var receivedAffect = await ReadFrameAsync(targetReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 2 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 10,
            "The target player did not receive Enfraquecer's Type 10 UpdateAffect.");

        await AssertNoFrameAsync(attackerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("ENFRAQUECER_PLAYER_TYPE10_SMOKE_PASS score-affect-target-only=true");
    }

    private static async Task AssaltoPlayerType13RelaysScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "ASSALTO_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "ASSALTO_OBSERVER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "ASSALTO_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Assalto player loopback peers could not be registered.");

        var casterMob = CreateMob("ASSALTO_CASTER", 100_000, 1, skillBar: false);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 1;
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Special3 = 50,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                new byte[LegacyAccountSnapshot.AffectStride],
                new byte[LegacyAccountSnapshot.MobExtraStride],
                0,
                0,
                0),
            itemData);
        var baselineDamage = LegacyMobCombatState.Read(baseline.Mob).CurrentScore.Damage;

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("ASSALTO_OBS", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("ASSALTO_OUT", 1_000, 3, skillBar: false)),
            "Assalto player loopback state could not be initialized.");

        var assalto = new LegacySkillDefinition(
            11, 78, 0, 47, 0, 0, 0, 0, 0, 0, 13, 7, 15,
            [19, 0, 0, 24, 0, 0, 0, 0], [20, 0, 0, 24, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Assalto");
        Check(world.TryApplySkillAttack(
                  1, 1, assalto, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 13 && affect[1] == 7 &&
              BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(2)) == 50 &&
              BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(4)) == 24,
            "Assalto did not apply the source-derived Type 13 affect to its caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var expectedProjection = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                targetAffect.ToArray(),
                new byte[LegacyAccountSnapshot.MobExtraStride],
                0,
                0,
                0),
            itemData);
        var expectedDamage = LegacyMobCombatState.Read(expectedProjection.Mob).CurrentScore.Damage;
        Check(expectedDamage > baselineDamage &&
              LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore.Damage == expectedDamage,
            "Assalto did not project the 7.69 Type 13 damage multiplier onto its caster.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 25, keywordIndex: 25, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 25, keywordIndex: 26);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 13 &&
              BinaryPrimitives.ReadInt32LittleEndian(
                  receivedScore.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 8)..]) == expectedDamage,
            "The caster did not receive Assalto's Type 13 UpdateScore with projected damage.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 13,
            "The caster did not receive Assalto's Type 13 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("ASSALTO_PLAYER_TYPE13_SMOKE_PASS score-affect-caster-only=true");
    }

    private static async Task PossuidoPlayerType14RelaysScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "POSSUIDO_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "POSSUIDO_OBSERVER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "POSSUIDO_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Possuído player loopback peers could not be registered.");

        var casterMob = CreateMob("POSSUIDO_CASTER", 700, 1, skillBar: false);
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Mp = 250,
            Special2 = 50,
            Constitution = 40,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("POSSUIDO_OBS", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("POSSUIDO_OUT", 1_000, 3, skillBar: false)),
            "Possuído player loopback state could not be initialized.");

        var possuido = new LegacySkillDefinition(
            3, 84, 0, 105, 0, 0, 0, 0, 0, 0, 14, 10, 15,
            [24, 0, 0, 24, 0, 0, 0, 0], [24, 0, 0, 24, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Possuído");
        Check(world.TryApplySkillAttack(
                  1, 1, possuido, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 14 && affect[1] == 10 &&
              BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(2)) == 50 &&
              BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(4)) == 24,
            "Possuído did not apply the source-derived Type 14 affect to its caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        var expectedValue = (50 * 3 / 4 + 10) * 2;
        Check(projectedScore.MaxHp == baselineScore.MaxHp + expectedValue * 2 &&
              projectedScore.Constitution == baselineScore.Constitution + expectedValue &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Possuído did not project the 7.69 Type 14 score while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 25, keywordIndex: 25, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 25, keywordIndex: 26);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        var wireScore = receivedScore.Payload.Span;
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              BinaryPrimitives.ReadInt32LittleEndian(wireScore[16..]) == projectedScore.MaxHp &&
              BinaryPrimitives.ReadInt16LittleEndian(wireScore[38..]) == projectedScore.Constitution &&
              BinaryPrimitives.ReadInt32LittleEndian(wireScore[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(wireScore[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(wireScore[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 14,
            "The caster did not receive Possuído's Type 14 UpdateScore with projected score and preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 14 &&
              receivedAffect.Payload.Span[1] == 50 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 10 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 24,
            "The caster did not receive Possuído's Type 14 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("POSSUIDO_PLAYER_TYPE14_SMOKE_PASS score-affect-caster-only=true");
    }

    private static async Task EscudoMagicoPlayerType11RelaysScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "MAGIC_SHIELD_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "MAGIC_SHIELD_OBSERVER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "MAGIC_SHIELD_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Escudo Mágico player loopback peers could not be registered.");

        var casterMob = CreateMob("SHIELD_CASTER", 700, 1, skillBar: false);
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Mp = 250,
            Special4 = 50,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineAc = LegacyMobCombatState.Read(baseline.Mob).CurrentScore.Ac;

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("SHIELD_OBS", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("SHIELD_OUT", 1_000, 3, skillBar: false)),
            "Escudo Mágico player loopback state could not be initialized.");

        var shield = new LegacySkillDefinition(
            43, 63, 2, 52, 1, 6, 0, 0, 0, 0, 11, 15, 15,
            [24, 0, 0, 24, 0, 0, 0, 0], [9, 0, 0, 6, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Escudo Mágico");
        Check(world.TryApplySkillAttack(
                  1, 1, shield, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 11 && affect[1] == 15 &&
              BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(2)) == 50 &&
              BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(4)) == 24,
            "Escudo Mágico did not apply the source-derived Type 11 affect to its caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(projectedScore.Ac == baselineAc + 25 && projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Escudo Mágico did not project 7.69 Type 11 AC while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 25, keywordIndex: 25, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 25, keywordIndex: 26);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[4..]) == projectedScore.Ac &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 11,
            "The caster did not receive Escudo Mágico's Type 11 UpdateScore with projected AC and preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 11 &&
              receivedAffect.Payload.Span[1] == 50 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 15 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 24,
            "The caster did not receive Escudo Mágico's Type 11 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("ESCUDO_MAGICO_PLAYER_TYPE11_SMOKE_PASS score-affect-caster-only=true");
    }

    private static async Task ToqueDeAthenaPlayerType15RelaysScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "ATHENA_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "ATHENA_OBSERVER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "ATHENA_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Toque de Athena player loopback peers could not be registered.");

        var casterMob = CreateMob("ATHENA_CASTER", 700, 1, skillBar: false);
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Mp = 250,
            Special1 = 250,
            Special2 = 247,
            Special3 = 254,
            Special4 = 50,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("ATHENA_OBS", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("ATHENA_OUT", 1_000, 3, skillBar: false)),
            "Toque de Athena player loopback state could not be initialized.");

        var athenaTouch = new LegacySkillDefinition(
            45, 72, 2, 98, 4, 6, 0, 0, 0, 0, 15, 7, 15,
            [19, 0, 0, 24, 0, 0, 0, 0], [19, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Toque de Athena");
        Check(world.TryApplySkillAttack(
                  1, 1, athenaTouch, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 15 && affect[1] == 7 &&
              BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(2)) == 50 &&
              BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(4)) == 24,
            "Toque de Athena did not apply its source-derived Type 15 affect to its caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(projectedScore.Special1 == 255 && projectedScore.Special2 == 255 &&
              projectedScore.Special3 == 255 && projectedScore.Special4 == 62 &&
              baselineScore.Special1 == 250 && baselineScore.Special2 == 247 &&
              baselineScore.Special3 == 254 && baselineScore.Special4 == 50 &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Toque de Athena did not project all four 7.69 Specials while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 25, keywordIndex: 25, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 25, keywordIndex: 26);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedScore.Payload.Span[40..]) == 255 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedScore.Payload.Span[42..]) == 255 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedScore.Payload.Span[44..]) == 255 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedScore.Payload.Span[46..]) == 62 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 15,
            "The caster did not receive Toque de Athena's Type 15 UpdateScore with projected Specials and preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 15 &&
              receivedAffect.Payload.Span[1] == 50 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 7 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 24,
            "The caster did not receive Toque de Athena's Type 15 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("TOQUE_DE_ATHENA_PLAYER_TYPE15_SMOKE_PASS score-affect-caster-only=true");
    }

    private static async Task MeditacaoPlayerType21RelaysScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "MEDIT_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "MEDIT_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "MEDIT_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Meditacao player loopback peers could not be registered.");

        var casterMob = CreateMob("MEDIT_CASTER", 700, 1, skillBar: false);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 3;
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Ac = 100,
            Mp = 250,
            Special2 = 50,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("MEDIT_VIEW", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("MEDIT_OUT", 1_000, 3, skillBar: false)),
            "Meditacao player loopback state could not be initialized.");

        var meditation = new LegacySkillDefinition(
            77, 87, 0, 90, 6, 0, 0, 0, 0, 0, 21, 15, 15,
            [20, 0, 0, 9, 0, 0, 0, 0], [20, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Meditacao");
        Check(world.TryApplySkillAttack(
                  1, 1, meditation, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 21 && affect[1] == 15 &&
              BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(2)) == 50 &&
              BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(4)) == 24,
            "Meditacao did not apply its source-derived Type 21 affect to its caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var expectedProjection = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                targetAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var expectedScore = LegacyMobCombatState.Read(expectedProjection.Mob).CurrentScore;
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(projectedScore.Ac == baselineScore.Ac - 26 &&
              projectedScore.Damage == expectedScore.Damage && projectedScore.Damage > baselineScore.Damage &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Meditacao did not project 7.69 Type 21 AC and damage multiplier while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 25, keywordIndex: 25, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 25, keywordIndex: 26);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[4..]) == projectedScore.Ac &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[8..]) == projectedScore.Damage &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 21,
            "The caster did not receive Meditacao's Type 21 UpdateScore with projected AC/damage and preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 21 &&
              receivedAffect.Payload.Span[1] == 50 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 15 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 24,
            "The caster did not receive Meditacao's Type 21 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("MEDITACAO_PLAYER_TYPE21_SMOKE_PASS score-affect-caster-only=true");
    }

    private static async Task ElementalProtectionPlayerType25RelaysScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "ELEMENTAL_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "ELEMENTAL_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "ELEMENTAL_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Protecao Elemental player loopback peers could not be registered.");

        var casterMob = CreateMob("ELEMENTAL_CASTER", 700, 1, skillBar: false);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 3;
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Ac = 100,
            Mp = 250,
            Special2 = 50,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineMob = baseline.Mob;
        var baselineScore = LegacyMobCombatState.Read(baselineMob).CurrentScore;

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("ELEMENTAL_VIEW", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("ELEMENTAL_OUT", 1_000, 3, skillBar: false)),
            "Protecao Elemental player loopback state could not be initialized.");

        var protection = new LegacySkillDefinition(
            53, 78, 0, 105, 15, 0, 0, 0, 0, 0, 25, 10, 15,
            [9, 0, 0, 8, 0, 0, 0, 0], [9, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Protecao Elemental");
        Check(world.TryApplySkillAttack(
                  1, 1, protection, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 25 && affect[1] == 10 &&
              BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(2)) == 50 &&
              BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(4)) == 24,
            "Protecao Elemental did not apply its source-derived Type 25 affect to its caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var expectedProjection = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                targetAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var expectedScore = LegacyMobCombatState.Read(expectedProjection.Mob).CurrentScore;
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        var projectedResist = outcome.TargetMobSnapshot
            .AsSpan(LegacyAccountSnapshot.MobResistOffset, 4).ToArray();
        var expectedResist = expectedProjection.Mob
            .AsSpan(LegacyAccountSnapshot.MobResistOffset, 4).ToArray();
        Check(projectedScore.Ac == expectedScore.Ac && projectedScore.Ac > baselineScore.Ac &&
              projectedResist.SequenceEqual(expectedResist) && expectedResist.SequenceEqual(new byte[] { 2, 2, 2, 2 }) &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Protecao Elemental did not project 7.69 Type 25 AC/resistances while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 26, keywordIndex: 27, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 26, keywordIndex: 28);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[4..]) == projectedScore.Ac &&
              receivedScore.Payload.Span.Slice(UpdateScoreConfirmationV769.ResistOffset, 4).SequenceEqual(expectedResist) &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 25,
            "The caster did not receive Protecao Elemental's Type 25 UpdateScore with projected AC/resistances and preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 25 &&
              receivedAffect.Payload.Span[1] == 50 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 10 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 24,
            "The caster did not receive Protecao Elemental's Type 25 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("PROTECAO_ELEMENTAL_PLAYER_TYPE25_SMOKE_PASS score-affect-caster-only=true");
    }

    private static async Task GoldenShieldPlayerType31RelaysScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "GOLDEN_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "GOLDEN_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "GOLDEN_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Golden Shield player loopback peers could not be registered.");

        var casterMob = CreateMob("GOLDEN_CASTER", 700, 1, skillBar: false);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 3;
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Ac = 100,
            Mp = 250,
            Special3 = 50,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("GOLDEN_VIEW", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("GOLDEN_OUT", 1_000, 3, skillBar: false)),
            "Golden Shield player loopback state could not be initialized.");

        var goldenShield = new LegacySkillDefinition(
            85, 90, 0, 120, 5, 0, 0, 0, 0, 0, 31, 150, 7,
            [20, 0, 0, 24, 0, 0, 0, 0], [20, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Explosao Eterea");
        Check(world.TryApplySkillAttack(
                  1, 1, goldenShield, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 31 && affect[1] == 150 &&
              BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(2)) == 50 &&
              BinaryPrimitives.ReadUInt32LittleEndian(affect.AsSpan(4)) == 12,
            "Explosao Eterea did not apply its source-derived Type 31 affect to its caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var expectedProjection = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                targetAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var expectedScore = LegacyMobCombatState.Read(expectedProjection.Mob).CurrentScore;
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check(projectedScore.Ac == expectedScore.Ac && projectedScore.Ac == baselineScore.Ac + 175 &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Explosao Eterea did not project 7.69 Type 31 AC while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 26, keywordIndex: 27, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 26, keywordIndex: 28);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[4..]) == projectedScore.Ac &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 31,
            "The caster did not receive the Type 31 UpdateScore with projected AC and preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 31 &&
              receivedAffect.Payload.Span[1] == 50 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 150 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 12,
            "The caster did not receive Explosao Eterea's Type 31 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("GOLDEN_SHIELD_PLAYER_TYPE31_SMOKE_PASS score-affect-caster-only=true");
    }

    private static async Task ImunidadePlayerType19RelaysScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "IMMUNITY_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "IMMUNITY_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "IMMUNITY_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Imunidade player loopback peers could not be registered.");

        var casterMob = CreateMob("IMMUNITY_CASTER", 700, 1, skillBar: false);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 3;
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Ac = 100,
            Mp = 250,
            Special2 = 50,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineRsv = BinaryPrimitives.ReadUInt16LittleEndian(
            baseline.Mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("IMMUNITY_VIEW", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("IMMUNITY_OUT", 1_000, 3, skillBar: false)),
            "Imunidade player loopback state could not be initialized.");

        var immunity = new LegacySkillDefinition(
            76, 84, 0, 42, 6, 0, 0, 0, 0, 0, 19, 15, 60,
            [20, 0, 0, 9, 0, 0, 0, 0], [19, 0, 0, 8, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Imunidade");
        Check(world.TryApplySkillAttack(
                  1, 1, immunity, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 19 && affect[1] == 15 &&
              BinaryPrimitives.ReadUInt16LittleEndian(affect.AsSpan(2)) == 50,
            "Imunidade did not apply its source-derived Type 19 affect to its caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var projectedRsv = BinaryPrimitives.ReadUInt16LittleEndian(
            outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check((baselineRsv & LegacyCurrentScoreMath.BlockRsvFlag) == 0 &&
              (projectedRsv & LegacyCurrentScoreMath.BlockRsvFlag) != 0 &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Imunidade did not project RSV_BLOCK while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 27, keywordIndex: 29, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 27, keywordIndex: 30);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                   receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 19,
            "The caster did not receive Imunidade's Type 19 UpdateScore with preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 19 &&
              receivedAffect.Payload.Span[1] == 50 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 15 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) > 0,
            "The caster did not receive Imunidade's Type 19 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("IMUNIDADE_PLAYER_TYPE19_SMOKE_PASS score-affect-caster-only=true");
    }

    private static async Task SpiritExchangePlayerType38RelaysScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "SPIRIT_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "SPIRIT_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "SPIRIT_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Troca de Espiritos player loopback peers could not be registered.");

        var casterMob = CreateMob("SPIRIT_CASTER", 700, 1, skillBar: false);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 3;
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            MaxHp = 1_000,
            MaxMp = 500,
            Hp = 700,
            Mp = 250,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineScore = LegacyMobCombatState.Read(baseline.Mob).CurrentScore;

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("SPIRIT_VIEW", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("SPIRIT_OUT", 1_000, 3, skillBar: false)),
            "Troca de Espiritos player loopback state could not be initialized.");

        var spiritExchange = new LegacySkillDefinition(
            87, 242, 0, 90, 10, 0, 0, 160, 0, 0, 38, 0, 3,
            [8, 0, 0, 7, 0, 0, 0, 0], [9, 0, 0, 7, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Troca de Espiritos");
        Check(world.TryApplySkillAttack(
                  1, 1, spiritExchange, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 38,
            "Troca de Espiritos did not apply its Type 38 affect to its player caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        var manaTransferred = baselineScore.MaxMp / 2;
        Check(projectedScore.MaxHp == baselineScore.MaxHp + manaTransferred &&
              projectedScore.MaxMp == baselineScore.MaxMp - manaTransferred &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Troca de Espiritos did not project the Type 38 MaxHp/MaxMp transfer while preserving current HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize))
            .ToFrame(codec, clientTick: 28, keywordIndex: 31, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 28, keywordIndex: 32);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[16..]) == projectedScore.MaxHp &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[20..]) == projectedScore.MaxMp &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                   receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 38,
            "The caster did not receive Type 38 UpdateScore with transferred MaxHp/MaxMp and preserved current resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 38 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) > 0,
            "The caster did not receive Troca de Espiritos' Type 38 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("SPIRIT_EXCHANGE_PLAYER_TYPE38_SMOKE_PASS score-affect-caster-only=true");
    }

    private static async Task IceEnchantPlayerType27RelaysScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = CreateItemDataWithWeaponType(101);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "ICE_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "ICE_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "ICE_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Encantar Gelo player loopback peers could not be registered.");

        var casterMob = CreateMob("ICE_CASTER", 700, 1, skillBar: false);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 3;
        new LegacyItem(1, 0, 0, 0, 0, 0, 0).Write(
            casterMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + 6 * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            MaxHp = 1_000,
            MaxMp = 500,
            Hp = 700,
            Mp = 250,
            Special2 = 20,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineRsv = BinaryPrimitives.ReadUInt16LittleEndian(
            baseline.Mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("ICE_VIEW", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("ICE_OUT", 1_000, 3, skillBar: false)),
            "Encantar Gelo player loopback state could not be initialized.");

        var iceEnchant = new LegacySkillDefinition(
            75, 51, 0, 30, 6, 0, 0, 0, 0, 0, 27, 1, 15,
            [8, 0, 0, 7, 0, 0, 0, 0], [9, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 0, 1, 0, 2, 0, 0, "Encantar Gelo");
        Check(world.TryApplySkillAttack(
                  1, 1, iceEnchant, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 27 && affect[1] == 1,
            "Encantar Gelo did not apply its Type 27 affect to its player caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var projectedRsv = BinaryPrimitives.ReadUInt16LittleEndian(
            outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check((baselineRsv & LegacyCurrentScoreMath.FrostRsvFlag) == 0 &&
              (projectedRsv & LegacyCurrentScoreMath.FrostRsvFlag) != 0 &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Encantar Gelo did not project RSV_FROST while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                LegacyMobCombatState.Read(outcome.TargetMobSnapshot).Rsv)
            .ToFrame(codec, clientTick: 29, keywordIndex: 33, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 29, keywordIndex: 34);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.RsvOffset..]) & LegacyCurrentScoreMath.FrostRsvFlag) != 0 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 27,
            "The caster did not receive Encantar Gelo's Type 27 UpdateScore with RSV_FROST and preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 27 &&
              receivedAffect.Payload.Span[1] == 20 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 1 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 19,
            "The caster did not receive Encantar Gelo's Type 27 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("ICE_ENCHANT_PLAYER_TYPE27_SMOKE_PASS score-affect-caster-only=true");
    }

    private static async Task InvisibilityPlayerType28RelaysHideScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = CreateItemDataWithWeaponType(101);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "HIDE_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "HIDE_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "HIDE_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Invisibility player loopback peers could not be registered.");

        var casterMob = CreateMob("HIDE_CASTER", 700, 1, skillBar: false);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 3;
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            MaxHp = 1_000,
            MaxMp = 500,
            Hp = 700,
            Mp = 250,
            Special4 = 20,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineRsv = BinaryPrimitives.ReadUInt16LittleEndian(
            baseline.Mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("HIDE_VIEW", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("HIDE_OUT", 1_000, 3, skillBar: false)),
            "Invisibility player loopback state could not be initialized.");

        var invisibility = new LegacySkillDefinition(
            95, 233, 0, 90, 35, 0, 0, 160, 0, 0, 28, 1, 0,
            [8, 0, 0, 7, 0, 0, 0, 0], [9, 0, 0, 7, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Invisibilidade");
        Check(world.TryApplySkillAttack(
                  1, 1, invisibility, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 28 && affect[1] == 1,
            "Invisibilidade did not apply its Type 28 affect to its player caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var projectedRsv = BinaryPrimitives.ReadUInt16LittleEndian(
            outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check((baselineRsv & LegacyCurrentScoreMath.HideRsvFlag) == 0 &&
              (projectedRsv & LegacyCurrentScoreMath.HideRsvFlag) != 0 &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Invisibilidade did not project RSV_HIDE while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                LegacyMobCombatState.Read(outcome.TargetMobSnapshot).Rsv)
            .ToFrame(codec, clientTick: 30, keywordIndex: 35, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 30, keywordIndex: 36);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.RsvOffset..]) & LegacyCurrentScoreMath.HideRsvFlag) != 0 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 28,
            "The caster did not receive Type 28 UpdateScore with RSV_HIDE and preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 28 &&
              receivedAffect.Payload.Span[1] == 20 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 1 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 1,
            "The caster did not receive Invisibilidade's Type 28 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("INVISIBILITY_PLAYER_TYPE28_SMOKE_PASS hide-affect-caster-only=true");
    }

    private static async Task EvasionPlayerType26RelaysParryScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = CreateItemDataWithWeaponType(101);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "PARRY_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "PARRY_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "PARRY_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Evasao Aprimorada player loopback peers could not be registered.");

        var casterMob = CreateMob("PARRY_CASTER", 700, 1, skillBar: false);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 3;
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            MaxHp = 1_000,
            MaxMp = 500,
            Hp = 700,
            Mp = 250,
            Special4 = 20,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineRsv = BinaryPrimitives.ReadUInt16LittleEndian(
            baseline.Mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("PARRY_VIEW", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("PARRY_OUT", 1_000, 3, skillBar: false)),
            "Evasao Aprimorada player loopback state could not be initialized.");

        var evasion = new LegacySkillDefinition(
            89, 42, 0, 24, 6, 0, 0, 0, 0, 0, 26, 1, 7,
            [8, 0, 0, 7, 0, 0, 0, 0], [9, 0, 0, 7, 0, 0, 0, 0],
            0, 0, 0, 1, 0, 0, 0, 0, "Evasao Aprimorada");
        Check(world.TryApplySkillAttack(
                  1, 1, evasion, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 26 && affect[1] == 1,
            "Evasao Aprimorada did not apply its Type 26 affect to its player caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var projectedRsv = BinaryPrimitives.ReadUInt16LittleEndian(
            outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check((baselineRsv & LegacyCurrentScoreMath.ParryRsvFlag) == 0 &&
              (projectedRsv & LegacyCurrentScoreMath.ParryRsvFlag) != 0 &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Evasao Aprimorada did not project RSV_PARRY while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                LegacyMobCombatState.Read(outcome.TargetMobSnapshot).Rsv)
            .ToFrame(codec, clientTick: 31, keywordIndex: 37, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 31, keywordIndex: 38);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.RsvOffset..]) & LegacyCurrentScoreMath.ParryRsvFlag) != 0 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 26,
            "The caster did not receive Type 26 UpdateScore with RSV_PARRY and preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 26 &&
              receivedAffect.Payload.Span[1] == 20 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 1 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 9,
            "The caster did not receive Evasao Aprimorada's Type 26 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("EVASION_PLAYER_TYPE26_SMOKE_PASS parry-affect-caster-only=true");
    }

    private static async Task EagleEyesPlayerType36RelaysDrainScoreToCasterAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = CreateItemDataWithWeaponType(41);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "DRAIN_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "DRAIN_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "DRAIN_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Olhos de Aguia player loopback peers could not be registered.");

        var casterMob = CreateMob("DRAIN_CASTER", 700, 1, skillBar: false);
        casterMob[LegacyAccountSnapshot.MobClassOffset] = 3;
        new LegacyItem(1, 0, 0, 0, 0, 0, 0).Write(
            casterMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + 6 * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            MaxHp = 1_000,
            MaxMp = 500,
            Hp = 700,
            Mp = 250,
            Special4 = 20,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var classMaster = (short)LegacyAccountSnapshot.ClassMasterMortal;
        var mobExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            mobExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset), classMaster);
        var emptyAffect = new byte[LegacyAccountSnapshot.AffectStride];
        var baseline = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                casterMob.ToArray(),
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                emptyAffect,
                mobExtra,
                0,
                0,
                0),
            itemData);
        var baselineRsv = BinaryPrimitives.ReadUInt16LittleEndian(
            baseline.Mob.AsSpan(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob, classMaster, emptyAffect, mobExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_001, positionY: 2_000, CreateMob("DRAIN_VIEW", 1_000, 2, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("DRAIN_OUT", 1_000, 3, skillBar: false)),
            "Olhos de Aguia player loopback state could not be initialized.");

        var eagleEyes = new LegacySkillDefinition(
            92, 75, 0, 30, 6, 0, 0, 0, 0, 0, 36, 1, 3,
            [8, 0, 0, 7, 0, 0, 0, 0], [9, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 0, 1, 0, 2, 0, 0, "Olhos de Aguia");
        Check(world.TryApplySkillAttack(
                  1, 1, eagleEyes, itemData, weather: 0, randomFactor: 100, out var outcome) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 36 && affect[1] == 1,
            "Olhos de Aguia did not apply its Type 36 affect to its player caster.");

        var targetAffect = outcome!.TargetAffectSnapshot!;
        var projectedRsv = BinaryPrimitives.ReadUInt16LittleEndian(
            outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobRsvOffset, sizeof(ushort)));
        var projectedScore = LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore;
        Check((baselineRsv & LegacyCurrentScoreMath.DrainRsvFlag) == 0 &&
              (projectedRsv & LegacyCurrentScoreMath.DrainRsvFlag) != 0 &&
              projectedScore.Hp == 700 && projectedScore.Mp == 250,
            "Olhos de Aguia did not project RSV_DRAIN with weapon type 41 while preserving caster HP/MP.");

        var scorePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, targetAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                LegacyMobCombatState.Read(outcome.TargetMobSnapshot).Rsv)
            .ToFrame(codec, clientTick: 32, keywordIndex: 39, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(targetAffect)
            .ToFrame(codec, mobId: 1, clientTick: 32, keywordIndex: 40);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
              receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.RsvOffset..]) & LegacyCurrentScoreMath.DrainRsvFlag) != 0 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[24..]) == 700 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 250 &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 36,
            "The caster did not receive Type 36 UpdateScore with RSV_DRAIN and preserved resources.");

        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.Header.Size == UpdateAffectConfirmationV769.PacketSize &&
              receivedAffect.IsChecksumValid && receivedAffect.Payload.Span[0] == 36 &&
              receivedAffect.Payload.Span[1] == 20 &&
              BinaryPrimitives.ReadInt16LittleEndian(receivedAffect.Payload.Span[2..]) == 1 &&
              BinaryPrimitives.ReadInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 4,
            "The caster did not receive Olhos de Aguia's Type 36 UpdateAffect.");

        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("EAGLE_EYES_PLAYER_TYPE36_SMOKE_PASS drain-affect-caster-only=true");
    }

    private static async Task SpectralLinkPlayerType37AddsForceDamageAndRelaysToViewAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        using var skillReader = new StringReader(
            "81,39,0,108,6,0,0,0,0,0,37,0,60,8.0.0.7.0.0.0.0,9.0.0.7.0.0.0.0,0,0,0,1,0,0,0,0,Ligacao_Espectral");
        var skillData = LegacySkillDataTable.Load(skillReader);
        var skill = skillData[81] ?? throw new InvalidOperationException("Skill 81 was not loaded for the loopback test.");
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData, skillData: skillData);
        Check(world.Enter(1, "SPECTRAL_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "SPECTRAL_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "SPECTRAL_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Ligacao Espectral loopback peers could not be registered.");

        var casterMob = CreateMob("SPECTRAL_CASTER", 10_000, 5, skillBar: false);
        var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Special3 = 20,
        };
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, casterMob) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_001, CreateMob("SPECTRAL_VIEW", 10_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("SPECTRAL_OUT", 10_000, 5, skillBar: false)),
            "Ligacao Espectral loopback player state could not be initialized.");

        const int npcId = 20_001;
        var npcMob = CreateMob("SPECTRAL_NPC", 50_000, 1, skillBar: false);
        var npcScore = LegacyScore.Read(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Intelligence = 100,
        };
        npcScore.Write(npcMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        npcScore.Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.EnterNpc(
                  npcMob,
                  2_001,
                  2_000,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  requestedConnectionId: npcId) == npcId,
            "Ligacao Espectral loopback NPC could not be registered.");

        Check(world.TryApplySkillAttack(
                  1, 1, skill, itemData, weather: 0, randomFactor: 100, out var skillOutcome) == LegacySkillAttackResult.Accepted &&
              skillOutcome is { Damage: 0, TargetAffectSnapshot: { } affect } && affect[0] == 37,
            "Skill 81 did not apply Type 37 to its caster in the loopback world.");

        var appliedAffect = skillOutcome!.TargetAffectSnapshot!;
        var scorePayload = new UpdateScoreConfirmation(skillOutcome.TargetMobSnapshot, appliedAffect).ToPayload();
        var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                scorePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                LegacyMobCombatState.Read(skillOutcome.TargetMobSnapshot).Rsv)
            .ToFrame(codec, clientTick: 32, keywordIndex: 39, id: 1);
        var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(appliedAffect)
            .ToFrame(codec, mobId: 1, clientTick: 32, keywordIndex: 40);
        await world.SendAsync(1, scoreFrame, CancellationToken.None);
        await world.SendAsync(1, affectFrame, CancellationToken.None);

        var receivedScore = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
              receivedScore.Header.Id == 1 && receivedScore.IsChecksumValid &&
              (BinaryPrimitives.ReadUInt16LittleEndian(
                  receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 37,
            "The caster did not receive the Type 37 score refresh.");
        var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
        Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
              receivedAffect.Header.Id == 1 && receivedAffect.IsChecksumValid &&
              receivedAffect.Payload.Span[0] == 37 &&
              BinaryPrimitives.ReadUInt32LittleEndian(receivedAffect.Payload.Span[4..]) == 19,
            "The caster did not receive the 19-tick Type 37 UpdateAffect.");
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);

        var physicalResult = world.TryApplyPhysicalAttack(
            1, npcId, randomFactor: 99, out var attackOutcome,
            attackMissRoll: 99, itemData: itemData);
        Check(physicalResult == LegacyPhysicalAttackResult.Accepted &&
              attackOutcome is { Damage: > 0, ForceDamage: 20 },
            $"The active Type 37 affect did not produce an authoritative +20 ForceDamage hit; result={physicalResult}, damage={attackOutcome?.Damage}, force={attackOutcome?.ForceDamage}.");
        Check(world.TryGetCharacterSnapshot(1, out var casterSnapshot, out _, out _) && casterSnapshot is not null,
            "The Type 37 caster snapshot was unavailable after the physical hit.");
        Check(AttackConfirmation.TryCreateSyntheticOneTarget(
                  casterSnapshot!,
                  actorId: 1,
                  targetX: 2_001,
                  targetY: 2_000,
                  wireTargetId: checked((ushort)npcId),
                  wireDamage: attackOutcome!.Damage,
                  skillIndex: -1,
                  motion: 4,
                  out var attackConfirmation),
            "The Type 37 physical hit could not be encoded as an AttackOne frame.");

        var attackFrame = attackConfirmation!.ToFrame(codec, clientTick: 33, keywordIndex: 41);
        await world.BroadcastToParticipantsAsync([1, 2], attackFrame, CancellationToken.None);
        foreach (var viewerStream in new[] { casterReadStream, observerReadStream })
        {
            var receivedAttack = await ReadFrameAsync(viewerStream, codec);
            Check(receivedAttack.Header.Type == AttackRequest.OneTargetMessageType &&
                  receivedAttack.Header.Size == LegacyMobAttackConfirmation.PacketSize &&
                  receivedAttack.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(receivedAttack.Payload.Span[52..]) == attackOutcome!.Damage,
                "An in-view client did not receive the Type 37 ForceDamage in the AttackOne frame.");
        }

        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("SPECTRAL_LINK_PLAYER_TYPE37_SMOKE_PASS force-damage=20 caster-affect-only=true attack=view-scoped=true");
    }

    private static async Task EquipmentGradeCombatBonusesRelayToViewAsync(
        NetworkStream attackerSendStream,
        NetworkStream targetSendStream,
        NetworkStream outsideSendStream,
        NetworkStream attackerReadStream,
        NetworkStream targetReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var decodedItemData = new byte[LegacyItemDataTable.BodySizeInBytes];
        WriteItemGrade(decodedItemData, itemIndex: 1, grade: 9);
        WriteItemGrade(decodedItemData, itemIndex: 2, grade: 10);
        WriteItemGrade(decodedItemData, itemIndex: 3, grade: 6);
        var encodedItemData = new byte[decodedItemData.Length];
        for (var index = 0; index < decodedItemData.Length; index++)
            encodedItemData[index] = (byte)(decodedItemData[index] ^ 0x5A);
        var itemData = LegacyItemDataTable.Load(encodedItemData);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "GRADE_ATTACKER", (bytes, token) => attackerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "GRADE_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "GRADE_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Equipment grade loopback peers could not be registered.");

        var attackerMob = CreateMob("GRADE_ATTACKER", 10_000, clan: 1, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var attackerEquipment = new LegacyItem[CharacterMobV769.EquipmentCount];
        attackerEquipment[0] = new LegacyItem(1, LegacyItemEffect.Sanctuary, 230, 0, 0, 0, 0);
        attackerEquipment[1] = new LegacyItem(3, LegacyItemEffect.Sanctuary, 235, 0, 0, 0, 0);

        var targetMob = CreateMob("GRADE_TARGET", 10_000, clan: 2, skillBar: false);
        var targetEquipment = new LegacyItem[CharacterMobV769.EquipmentCount];
        targetEquipment[0] = new LegacyItem(2, LegacyItemEffect.Sanctuary, 230, 0, 0, 0, 0);
        Check(world.SetCharacterState(
                  1, 0, 0, clan: 1, guildLevel: 0, coin: 0, positionX: 2_000, positionY: 2_000,
                  mob: attackerMob, classMaster: LegacyAccountSnapshot.ClassMasterMortal,
                  affect: new byte[LegacyAccountSnapshot.AffectStride],
                  mobExtra: new byte[LegacyAccountSnapshot.MobExtraStride], clientEquipment: attackerEquipment) &&
              world.SetCharacterState(
                  2, 0, 0, clan: 2, guildLevel: 0, coin: 0, positionX: 2_005, positionY: 2_000,
                  mob: targetMob, classMaster: LegacyAccountSnapshot.ClassMasterMortal,
                  affect: new byte[LegacyAccountSnapshot.AffectStride],
                  mobExtra: new byte[LegacyAccountSnapshot.MobExtraStride], clientEquipment: targetEquipment) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_020, positionY: 2_000,
                  mob: CreateMob("GRADE_OUTSIDE", 10_000, clan: 3, skillBar: false)),
            "Equipment grade loopback player/equipment state could not be initialized.");

        var attackResult = world.TryApplyPhysicalAttack(
            1, 2, randomFactor: 99, out var attackOutcome,
            attackMissRoll: 99, itemData: itemData);
        Check(attackResult == LegacyPhysicalAttackResult.Accepted &&
              attackOutcome is { Damage: > 0, ForceDamage: 200, ReflectDamage: 40 },
            $"The 7.69 grade/gem combat bonuses did not reach the physical attack result; result={attackResult}, force={attackOutcome?.ForceDamage}, reflect={attackOutcome?.ReflectDamage}.");
        Check(world.TryGetCharacterSnapshot(1, out var attackerSnapshot, out _, out _) && attackerSnapshot is not null,
            "The equipment grade attacker snapshot was unavailable for the AttackOne relay.");
        Check(AttackConfirmation.TryCreateSyntheticOneTarget(
                  attackerSnapshot!, actorId: 1, targetX: 2_005, targetY: 2_000,
                  wireTargetId: 2, wireDamage: attackOutcome!.Damage, skillIndex: -1, motion: 4,
                  out var confirmation),
            "The equipment grade player hit could not be encoded for the AttackOne relay.");

        var frame = confirmation!.ToFrame(codec, clientTick: 43, keywordIndex: 53);
        await world.BroadcastToParticipantsAsync([1, 2], frame, CancellationToken.None);
        foreach (var viewer in new[] { attackerReadStream, targetReadStream })
        {
            var received = await ReadFrameAsync(viewer, codec);
            Check(received.Header.Type == AttackRequest.OneTargetMessageType &&
                  received.Header.Size == LegacyMobAttackConfirmation.PacketSize && received.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(received.Payload.Span[52..]) == attackOutcome!.Damage,
                "An in-view client did not receive the authoritative damage containing equipment grade bonuses.");
        }

        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("EQUIPMENT_GRADE_COMBAT_SMOKE_PASS force-damage=200 reflect-damage=40 attack=view-scoped=true");
    }

    private static void WriteItemGrade(Span<byte> decoded, int itemIndex, short grade)
    {
        var offset = itemIndex * LegacyItemDataTable.RecordSizeInBytes;
        BinaryPrimitives.WriteInt16LittleEndian(decoded.Slice(offset + 142, sizeof(short)), grade);
    }

    private static async Task SoulPlayerType29Skill102RebuildScoreAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        foreach (var (skillId, skillRow, expectedDuration, special2) in new[]
        {
            (102, "102,0,0,75,1200,1,0,0,0,0,29,5,60,24.0.0.24.0.0.0.0,24.0.0.24.0.0.0.0,0,0,0,1,0,0,0,0,Limite_da_Alma", 64, 0),
        })
        {
            var skillData = LegacySkillDataTable.Load(new StringReader(skillRow));
            var skill = skillData[skillId] ?? throw new InvalidOperationException($"Skill {skillId} was not loaded.");
            var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
            var world = new WorldHub(itemData: itemData, skillData: skillData);
            Check(world.Enter(1, $"SOUL_CASTER_{skillId}", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
                  world.Enter(2, $"SOUL_VIEW_{skillId}", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
                  world.Enter(3, $"SOUL_OUT_{skillId}", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
                $"Skill {skillId} Soul loopback peers could not be registered.");

            var casterMob = CreateMob($"SOUL{skillId}", 333, 5, skillBar: false);
            var casterScore = LegacyScore.Read(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
            {
                Level = 400,
                AttackRun = 0x96,
                MaxHp = 1_000,
                MaxMp = 500,
                Hp = 333,
                Mp = 222,
                Intelligence = 100,
                Constitution = 50,
                Special2 = checked((short)special2),
            };
            casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
            casterScore.Write(casterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            var casterExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
            casterExtra[LegacyAccountSnapshot.MobExtraSoulOffset] = 3;
            BinaryPrimitives.WriteInt16LittleEndian(
                casterExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset),
                checked((short)LegacyExperienceMath.ClassMasterCelestial));
            Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                      positionX: 2_000, positionY: 2_000, casterMob,
                      classMaster: checked((short)LegacyExperienceMath.ClassMasterCelestial),
                      new byte[LegacyAccountSnapshot.AffectStride], casterExtra) &&
                  world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                      positionX: 2_001, positionY: 2_000, CreateMob($"SOULV{skillId}", 10_000, 5, skillBar: false)) &&
                  world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                      positionX: 2_018, positionY: 2_000, CreateMob($"SOULO{skillId}", 10_000, 5, skillBar: false)),
                $"Skill {skillId} Soul loopback player state could not be initialized.");

            var result = world.TryApplySkillAttack(
                1, 1, skill, itemData, weather: 0, randomFactor: 100, out var outcome);
            Check(result == LegacySkillAttackResult.Accepted &&
                  outcome is { TargetAffectSnapshot: { } targetAffect } && targetAffect[0] == 29 &&
                  LegacyMobCombatState.Read(outcome.TargetMobSnapshot).CurrentScore is
                      { MaxMp: 940, Hp: 333, Mp: 222 },
                $"Skill {skillId} did not project Type 29 Soul MaxMp while preserving current HP/MP.");

            var appliedAffect = outcome!.TargetAffectSnapshot!;
            var sourcePayload = new UpdateScoreConfirmation(outcome.TargetMobSnapshot, appliedAffect).ToPayload();
            var scoreFrame = W2ppUpdateScoreV1Adapter.AdaptPayload(
                    sourcePayload.AsSpan(0, W2ppUpdateScoreV1Adapter.SourcePayloadSize),
                    LegacyMobCombatState.Read(outcome.TargetMobSnapshot).Rsv)
                .ToFrame(codec, clientTick: 34, keywordIndex: 42, id: 1);
            var affectFrame = W2ppUpdateAffectV1Adapter.AdaptPayload(appliedAffect)
                .ToFrame(codec, mobId: 1, clientTick: 34, keywordIndex: 43);
            await world.SendAsync(1, scoreFrame, CancellationToken.None);
            await world.SendAsync(1, affectFrame, CancellationToken.None);

            var receivedScore = await ReadFrameAsync(casterReadStream, codec);
            Check(receivedScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  receivedScore.Header.Id == 1 && receivedScore.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[20..]) == 940 &&
                  BinaryPrimitives.ReadInt32LittleEndian(receivedScore.Payload.Span[28..]) == 222 &&
                  (BinaryPrimitives.ReadUInt16LittleEndian(
                      receivedScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 29,
                $"Skill {skillId} did not send its Type 29 MaxMp score only to the caster.");
            var receivedAffect = await ReadFrameAsync(casterReadStream, codec);
            Check(receivedAffect.Header.Type == UpdateAffectConfirmationV769.MessageType &&
                  receivedAffect.Header.Id == 1 && receivedAffect.IsChecksumValid &&
                  receivedAffect.Payload.Span[0] == 29 &&
                  BinaryPrimitives.ReadUInt32LittleEndian(receivedAffect.Payload.Span[4..]) == expectedDuration,
                $"Skill {skillId} did not send the source Type 29 duration.");
            await AssertNoFrameAsync(observerReadStream);
            await AssertNoFrameAsync(outsideReadStream);

            LegacyPlayerAffectTimerOutcome? expiry = null;
            for (var tick = 0; tick < expectedDuration; tick++)
                expiry = world.TryProcessPlayerTimerAffects(1);
            Check(expiry is { ScoreRefreshRequired: true } &&
                  !expiry.AffectSnapshot.Where((_, index) => index % 8 == 0).Contains((byte)29) &&
                  LegacyMobCombatState.Read(expiry.MobSnapshot).CurrentScore is { MaxMp: 500, Mp: 222 },
                $"Skill {skillId} did not restore the baseline Soul score on Type 29 expiry.");

            await LegacyPlayerAffectRelay.RelayAsync(world, expiry!, codec, CancellationToken.None);
            foreach (var viewerStream in new[] { casterReadStream, observerReadStream })
            {
                var expiredScore = await ReadFrameAsync(viewerStream, codec);
                Check(expiredScore.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                      expiredScore.Header.Id == 1 && expiredScore.IsChecksumValid &&
                      BinaryPrimitives.ReadInt32LittleEndian(expiredScore.Payload.Span[20..]) == 500 &&
                      BinaryPrimitives.ReadInt32LittleEndian(expiredScore.Payload.Span[28..]) == 222 &&
                      (BinaryPrimitives.ReadUInt16LittleEndian(
                          expiredScore.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 0,
                    $"Skill {skillId} Type 29 expiry score was not relayed to the captured player view.");
            }
            await AssertNoFrameAsync(outsideReadStream);
        }

        Console.WriteLine("PLAYER_SOUL_TYPE29_SKILL102_SMOKE_PASS score-affect-caster-only=true expiry-view-relayed=true");
    }

    private static async Task HunterProficiencySkill82DamageRelaysToViewAsync(
        NetworkStream casterSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream casterReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        const int hunterLearnedBit = 10;
        const int hunterWeaponIndex = 500;
        const int npcId = 20_082;
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "HUNTER_PROFICIENCY_CASTER", (bytes, token) => casterSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "HUNTER_PROFICIENCY_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "HUNTER_PROFICIENCY_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Hunter Proficiency loopback peers could not be registered.");

        var hunterMob = CreateMob("HUNTER_CASTER", 10_000, 3, skillBar: false);
        hunterMob[LegacyAccountSnapshot.MobClassOffset] = 3;
        BinaryPrimitives.WriteUInt32LittleEndian(
            hunterMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset),
            1u << hunterLearnedBit);
        var hunterScore = LegacyScore.Read(hunterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Special2 = 20,
        };
        hunterScore.Write(hunterMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        hunterScore.Write(hunterMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(hunterWeaponIndex, 0, 0, 0, 0, 0, 0).Write(
            hunterMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset + 6 * LegacyItem.SizeInBytes, LegacyItem.SizeInBytes));
        var hunterExtra = new byte[LegacyAccountSnapshot.MobExtraStride];
        BinaryPrimitives.WriteInt16LittleEndian(
            hunterExtra.AsSpan(LegacyAccountSnapshot.MobExtraClassMasterOffset),
            LegacyAccountSnapshot.ClassMasterMortal);
        var projectedHunter = LegacyCurrentScoreLoginProjector.Project(
            new LegacyCharacterLoginData(
                hunterMob,
                new byte[LegacyAccountSnapshot.ShortSkillStride],
                new byte[LegacyAccountSnapshot.AffectStride],
                hunterExtra,
                Donate: 0,
                SavedPositionX: 0,
                SavedPositionY: 0),
            itemData);
        var projectedDamage = LegacyMobCombatState.Read(projectedHunter.Mob).CurrentScore.Damage;
        Check(projectedDamage == 1_132,
            $"Hunter Proficiency did not add Special2+12 to the projected login damage: damage={projectedDamage}.");

        Check(world.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, projectedHunter.Mob,
                  classMaster: LegacyAccountSnapshot.ClassMasterMortal,
                  projectedHunter.Affect, hunterExtra) &&
              world.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_001, CreateMob("HUNTER_VIEW", 10_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("HUNTER_OUT", 10_000, 5, skillBar: false)),
            "Hunter Proficiency loopback player state could not be initialized.");

        var npcMob = CreateMob("HUNTER_NPC", 50_000, 1, skillBar: false);
        Check(world.EnterNpc(npcMob, 2_001, 2_000,
                  new byte[LegacyAccountSnapshot.AffectStride], requestedConnectionId: npcId) == npcId,
            "Hunter Proficiency loopback NPC could not be registered.");
        var physicalResult = world.TryApplyPhysicalAttack(
            1, npcId, randomFactor: 99, out var attackOutcome,
            attackMissRoll: 99, itemData: itemData);
        Check(physicalResult == LegacyPhysicalAttackResult.Accepted && attackOutcome is { Damage: > 0 },
            $"The Hunter Proficiency physical attack did not resolve: result={physicalResult}, damage={attackOutcome?.Damage}.");
        Check(world.TryGetCharacterSnapshot(1, out var casterSnapshot, out _, out _) && casterSnapshot is not null,
            "The Hunter Proficiency caster snapshot was unavailable after the physical hit.");
        Check(AttackConfirmation.TryCreateSyntheticOneTarget(
                  casterSnapshot!, actorId: 1, targetX: 2_001, targetY: 2_000,
                  wireTargetId: checked((ushort)npcId), wireDamage: attackOutcome!.Damage,
                  skillIndex: -1, motion: 4, out var attackConfirmation),
            "The Hunter Proficiency physical hit could not be encoded as an AttackOne frame.");

        var attackFrame = attackConfirmation!.ToFrame(codec, clientTick: 35, keywordIndex: 44);
        await world.BroadcastToParticipantsAsync([1, 2], attackFrame, CancellationToken.None);
        foreach (var viewerStream in new[] { casterReadStream, observerReadStream })
        {
            var receivedAttack = await ReadFrameAsync(viewerStream, codec);
            Check(receivedAttack.Header.Type == AttackRequest.OneTargetMessageType &&
                  receivedAttack.Header.Size == LegacyMobAttackConfirmation.PacketSize &&
                  receivedAttack.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(receivedAttack.Payload.Span[52..]) == attackOutcome.Damage,
                "An in-view client did not receive the Hunter Proficiency AttackOne damage.");
        }

        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("HUNTER_PROFICIENCY_SKILL82_SMOKE_PASS learned-bit=10 damage-score=relayed view-scoped=true");
    }

    private static async Task Type1DamageSkillNpcScoreRelaysToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec,
        LegacySkillDefinition skill)
    {
        var world = new WorldHub();
        Check(world.Enter(1, "ICE_LANCE_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "ICE_LANCE_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "ICE_LANCE_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            $"Skill {skill.Id} loopback peers could not be registered.");

        var attackerMob = CreateMob("ICE_ATK", 1_000, 5, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 1_500,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_017, positionY: 2_016, CreateMob("ICE_VIEW", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("ICE_OUT", 1_000, 5, skillBar: false)),
            $"Skill {skill.Id} loopback player state could not be initialized.");

        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var npcMob = CreateMob("ICE_NPC", 100_000, 1, skillBar: false);
        var npcScore = LegacyScore.Read(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0x96,
            Intelligence = 100,
        };
        npcScore.Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(51, 0, 0, 0, 0, 0, 0).Write(
            npcMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        Check(world.EnterNpc(
                  npcMob,
                  2_001,
                  2_000,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  requestedConnectionId: npcId) == npcId,
            $"Skill {skill.Id} loopback NPC 1000 could not be registered.");

        var itemData = LegacyItemDataTable.Load(
            Enumerable.Repeat((byte)0x5A, LegacyItemDataTable.BodySizeInBytes).ToArray());
        Check(world.TryApplySkillAttack(
                  1, npcId, skill, itemData, weather: 0, randomFactor: 100, out var outcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: > 0, TargetAffectSnapshot: { } updatedAffect } &&
              updatedAffect[0] == 1 && updatedAffect[1] == 2 &&
              LegacyScore.Read(outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x64, Intelligence: 60 },
            $"Skill {skill.Id} did not apply its Type 1 affect in the damaging NPC skill path.");

        await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
            world, outcome!, codec, clientTick: 20, keywordIndex: 6, CancellationToken.None);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  (BinaryPrimitives.ReadUInt16LittleEndian(
                      scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 1 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.ScoreOffset + 13] == 0x64 &&
                  BinaryPrimitives.ReadInt16LittleEndian(
                      scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 34)..]) == 60,
                $"An in-view peer did not receive skill {skill.Id}'s Type 1 NPC UpdateScore.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine($"{skill.Name.ToUpperInvariant()}_NPC_TYPE1_SMOKE_PASS attackrun-view-scoped=true");
    }

    // Rows 23, 34, and 36 from TMProject 7.69 Servidor/Server/Common/SkillData.csv.
    private static LegacySkillDefinition TempestadeDeGeloSkill() =>
        new(23, 227, 6, 150, 6, 5, 3, 210, 0, 0, 1, 2, 3,
            [8, 0, 0, 7, 0, 0, 0, 0], [8, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 13, 0, 2, 0, 1, "Tempestade_de_Gelo");

    private static LegacySkillDefinition NevascaSkill() =>
        new(36, 72, 1, 35, 5, 6, 3, 200, 0, 0, 1, 2, 1,
            [6, 0, 0, 7, 0, 0, 0, 0], [6, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 1, 0, 2, 0, 1, "Nevasca");

    private static LegacySkillDefinition LancaDeGeloSkill() =>
        new(34, 69, 1, 15, 3, 6, 3, 95, 0, 0, 1, 2, 1,
            [5, 0, 0, 7, 0, 0, 0, 0], [5, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 1, 0, 2, 0, 1, "Lanca_de_Gelo");

    private static LegacySkillDefinition PursuicaoSkill() =>
        new(16, 24, 3, 25, 2, 5, 0, 0, 0, 0, 3, 60, 0,
            [10, 0, 0, 0, 0, 0, 0, 0], [10, 0, 0, 0, 0, 0, 0, 0],
            2, 0, 1, 13, 0, 2, 0, 0, "Perseguicao");

    private static async Task NonGeneratedNpcLifeAuraRelaysHpThenScoreOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "STATIC_LIFE_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "STATIC_LIFE_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "STATIC_LIFE_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The non-generated Life Aura loopback peers could not be registered.");

        var healedNpc = CreateMob("STATIC_LIFE_NPC", 1_000, 1, skillBar: true);
        var npcScore = LegacyScore.Read(healedNpc.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        (npcScore with { Hp = 500 }).Write(healedNpc.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 17;
        affect[1] = 20;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        const int npcId = 20_002;
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("ST17_TARGET", 1_000, 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("ST17_VIEW", 1_000, 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 28, positionY: 10, CreateMob("ST17_OUT", 1_000, 5, skillBar: false)) &&
              hub.EnterNpc(healedNpc, 10, 10, affect, requestedConnectionId: npcId) == npcId,
            "The non-generated Life Aura NPC, affect, or peer positions could not be initialized.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        LegacyMobAiTickResult? visitedTick = null;
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = await runtime.TickNextShardAsync();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                visitedTick = tick;
                break;
            }
        }

        Check(visitedTick is not null && hub.TryGetNpcSnapshot(npcId, out var updatedNpc) && updatedNpc is not null &&
              LegacyMobCombatState.Read(updatedNpc.MobSnapshot).CurrentScore.Hp == 570 &&
              updatedNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The non-generated Peace Life Aura did not heal and expire on its assigned shard visit.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var hpFrame = await ReadFrameAsync(viewerStream, codec);
            Check(hpFrame.Header.Type == SetHpDamConfirmation.MessageType && hpFrame.Header.Id == npcId &&
                  hpFrame.Header.Size == SetHpDamConfirmation.PacketSize && hpFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span) == 570 &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span[4..]) == 70,
                "An in-view peer did not receive non-generated NPC SetHpDam before UpdateScore.");

            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType && scoreFrame.Header.Id == npcId &&
                  scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize && scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 570 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0,
                "An in-view peer did not receive the non-generated NPC final score after SetHpDam.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("NON_GENERATED_NPC_TYPE17_SMOKE_PASS hp-score=true view-scoped=true");
    }

    private static async Task LaminaCongelanteNpcType7RelaysScoreToNpcViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "FROZEN_BLADE_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "FROZEN_BLADE_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "FROZEN_BLADE_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Lamina Congelante loopback peers could not be registered.");

        var attackerMob = CreateMob("FROZEN_ATK", 1_000, 5, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 1_500,
            Special4 = 50,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.SetCharacterState(1, LegacyAccountSnapshot.ClassMasterMortal, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_017, positionY: 2_016, CreateMob("FROZEN_VIEW", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("FROZEN_OUT", 1_000, 5, skillBar: false)),
            "Lamina Congelante loopback player state could not be initialized.");

        const int npcId = LegacyMobAiPlanner.MaximumPlayerId;
        var npcMob = CreateMob("FROZEN_NPC", 100_000, 1, skillBar: false);
        var npcScore = LegacyScore.Read(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            AttackRun = 0xA6,
            Intelligence = 100,
            Dexterity = 40,
        };
        npcScore.Write(npcMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        npcScore.Write(npcMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        new LegacyItem(51, 0, 0, 0, 0, 0, 0).Write(
            npcMob.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        Check(world.EnterNpc(
                  npcMob,
                  2_001,
                  2_000,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  requestedConnectionId: npcId) == npcId,
            "Lamina Congelante loopback NPC 1000 could not be registered.");

        var laminaCongelante = new LegacySkillDefinition(
            19, 78, 1, 22, 4, 5, 3, 50, 0, 0, 7, 1, 0,
            [8, 0, 0, 7, 0, 0, 0, 0], [8, 0, 0, 7, 0, 0, 0, 0],
            2, 0, 1, 1, 0, 2, 0, 1, "Lamina_Congelante");
        Check(world.TryApplySkillAttack(
                  1, npcId, laminaCongelante, itemData, weather: 0, randomFactor: 100, out var outcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: > 0, TargetAffectSnapshot: { } updatedAffect } &&
              updatedAffect[0] == 7 && updatedAffect[1] == 1 &&
              LegacyScore.Read(outcome.TargetMobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset))
                  is { AttackRun: 0x96, Intelligence: 75 },
            "Lamina Congelante did not apply and project its Type 7 affect in the damaging NPC skill path.");

        await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
            world, outcome!, codec, clientTick: 20, keywordIndex: 6, CancellationToken.None);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  (BinaryPrimitives.ReadUInt16LittleEndian(
                      scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]) >> 8) == 7 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.ScoreOffset + 13] == 0x96 &&
                  BinaryPrimitives.ReadInt16LittleEndian(
                      scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.ScoreOffset + 34)..]) == 75,
                "An in-view peer did not receive the Lamina Congelante Type 7 NPC UpdateScore.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("LAMINA_CONGELANTE_NPC_TYPE7_SMOKE_PASS attackrun-view-scoped=true");
    }

    private static async Task PoisonTickNpcType20RelaysScoreToNpcViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var itemData = LegacyItemDataTable.Load(new byte[LegacyItemDataTable.BodySizeInBytes]);
        var world = new WorldHub(itemData: itemData);
        Check(world.Enter(1, "POISON_TICK_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              world.Enter(2, "POISON_TICK_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              world.Enter(3, "POISON_TICK_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Poison tick loopback peers could not be registered.");

        var attackerMob = CreateMob("POISON_ATK", 1_000, 5, skillBar: false);
        var attackerScore = LegacyScore.Read(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with
        {
            Level = 1_500,
            Special4 = 50,
        };
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        attackerScore.Write(attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(world.SetCharacterState(1, LegacyAccountSnapshot.ClassMasterMortal, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_000, positionY: 2_000, attackerMob) &&
              world.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_017, positionY: 2_016, CreateMob("POISON_VIEW", 1_000, 5, skillBar: false)) &&
              world.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 2_018, positionY: 2_000, CreateMob("POISON_OUT", 1_000, 5, skillBar: false)),
            "Poison tick loopback player state could not be initialized.");

        const int npcId = LegacyMobAiPlanner.MaximumPlayerId + 1;
        var npcMob = CreateMob("POISON_NPC", 100_000, 1, skillBar: false);
        Check(world.EnterNpc(
                  npcMob,
                  2_001,
                  2_000,
                  new byte[LegacyAccountSnapshot.AffectStride],
                  requestedConnectionId: npcId) == npcId,
            "Poison tick loopback NPC 1001 could not be registered.");

        var poisonSkill = new LegacySkillDefinition(
            21, 87, 1, 38, 4, 5, 1, 80, 20, 10, 0, 0, 0,
            [9, 0, 0, 5, 0, 0, 0, 0], [9, 0, 0, 5, 0, 0, 0, 0],
            4, 0, 1, 1, 0, 3, 0, 1, "Punhalada_Venenosa");
        Check(world.TryApplySkillAttack(
                  1, npcId, poisonSkill, itemData, weather: 0, randomFactor: 100, out var outcome,
                  affectRandomFactor: 0) == LegacySkillAttackResult.Accepted &&
              outcome is { Damage: > 0, TargetAffectSnapshot: { } updatedAffect } &&
              updatedAffect[0] == 20 && updatedAffect[1] == 10 &&
              BinaryPrimitives.ReadUInt16LittleEndian(updatedAffect.AsSpan(2)) == 50 &&
              BinaryPrimitives.ReadUInt32LittleEndian(updatedAffect.AsSpan(4)) == 1,
            "Punhalada Venenosa did not apply its Type 20 tick to NPC 1001.");

        await LegacyNpcAffectScoreRelay.RelayNpcAffectScoreAsync(
            world, outcome!, codec, clientTick: 21, keywordIndex: 7, CancellationToken.None);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            var wireAffect = BinaryPrimitives.ReadUInt16LittleEndian(
                scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset..]);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  wireAffect == 0x1401,
                $"An in-view peer did not receive the NPC Type 20 UpdateScore affect: type={scoreFrame.Header.Type}, id={scoreFrame.Header.Id}, size={scoreFrame.Header.Size}, checksum={scoreFrame.IsChecksumValid}, affect=0x{wireAffect:X4}.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("POISON_TICK_NPC_TYPE20_SMOKE_PASS view-scoped=true");
    }

    private static async Task NonGeneratedNpcPoisonRelaysHpThenScoreOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "P20_TARG", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "P20_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "P20_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The non-generated Poison loopback peers could not be registered.");

        var poisonedMob = CreateMob("P20_NPC", 10_000, 2, true);
        BinaryPrimitives.WriteInt32LittleEndian(
            poisonedMob.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), 0);
        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 20;
        affect[1] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        const int npcId = 20_002;
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("P20_TARG", 1_000, 5, false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("P20_VIEW", 1_000, 5, false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 28, positionY: 10, CreateMob("P20_OUT", 1_000, 5, false)) &&
              hub.EnterNpc(poisonedMob, 10, 10, affect, requestedConnectionId: npcId) == npcId,
            "The non-generated Poison NPC, affect, or peer positions could not be initialized.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        LegacyMobAiTickResult? visitedTick = null;
        for (var tickIndex = 0; tickIndex < LegacyMobAiRuntime.PeaceShardCount; tickIndex++)
        {
            var tick = await runtime.TickNextShardAsync();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                visitedTick = tick;
                break;
            }
        }

        Check(visitedTick is not null && hub.TryGetNpcSnapshot(npcId, out var updatedNpc) && updatedNpc is not null &&
              LegacyMobCombatState.Read(updatedNpc.MobSnapshot).CurrentScore.Hp == 9_000 &&
              updatedNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The non-generated Peace Poison did not apply capped damage and expire on its assigned shard visit.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var hpFrame = await ReadFrameAsync(viewerStream, codec);
            Check(hpFrame.Header.Type == SetHpDamConfirmation.MessageType && hpFrame.Header.Id == npcId &&
                  hpFrame.Header.Size == SetHpDamConfirmation.PacketSize && hpFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span) == 9_000 &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span[4..]) == -1_000,
                "An in-view peer did not receive the non-generated Poison SetHpDam with the signed delta.");

            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType && scoreFrame.Header.Id == npcId &&
                  scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize && scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 9_000 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0,
                "An in-view peer did not receive the final Poison score after SetHpDam.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
        Console.WriteLine("NON_GENERATED_NPC_TYPE20_SMOKE_PASS hp-score=true view-scoped=true");
    }

    private static async Task CombatBestialAuraExpiryRelaysOnlyScoreToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "BESTIAL_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "BESTIAL_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "BESTIAL_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The Combat Bestial Aura loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("BESTIAL_TARGET", 1_000, 5, false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("BESTIAL_VIEW", 1_000, 5, false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 27, positionY: 26, CreateMob("BESTIAL_OUTSIDE", 1_000, 5, false)),
            "The Combat Bestial Aura loopback peer positions could not be initialized.");

        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 23;
        affect[1] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 2);
        const int npcId = 20_002;
        Check(hub.EnterNpc(
                  CreateMob("COMBAT_AURA", 1_000, 1, true), 10, 10, affect,
                  requestedConnectionId: npcId, generateIndex: 42) == npcId &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0,
                  new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Combat Bestial Aura NPC could not be registered without a target.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        for (var tickIndex = 0; tickIndex < 32; tickIndex++)
        {
            var tick = await runtime.TickNextShardAsync();
            Check(tick.Attacks.Count == 0,
                "Type 23 on a generated Combat NPC must not synthesize an area attack.");
        }

        Check(hub.TryGetNpcSnapshot(npcId, out var expiredNpc) && expiredNpc is not null &&
              LegacyMobCombatState.Read(expiredNpc.MobSnapshot).CurrentScore.Hp == 1_000 &&
              expiredNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The Combat Type 23 timer did not expire without changing the generated NPC's HP.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 1_000 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0,
                "Type 23 expiry must relay only UpdateScore to in-view peers.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task PeaceSummonBestialAuraExpiryRelaysOnlyScoreToCapturedViewAsync(
        NetworkStream leaderSendStream,
        NetworkStream ownerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream leaderReadStream,
        NetworkStream ownerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        Check(hub.Enter(1, "AURAPEACE_LEAD", (bytes, token) => leaderSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "AURAPEACE_OWNER", (bytes, token) => ownerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "AURAPEACE_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The peaceful summon Bestial Aura loopback peers could not be registered.");

        var leaderMob = CreateMob("AURAPEACE_LEAD", 1_000, 5, skillBar: false);
        var ownerMob = CreateMob("AURAPEACE_OWNER", 1_000, 5, skillBar: false);
        SetSpecial3(leaderMob, 30);
        SetSpecial3(ownerMob, 30);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 99, 100, leaderMob) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, 100, 100, ownerMob) &&
              hub.TrySetParty(1, [1, 2]),
            "The peaceful summon Bestial Aura party could not be initialized.");

        var summon = CreateRouteTypeFiveSummon(hub, casterConnectionId: 2);
        SetSummonBestialAura(summon, time: 2);
        var expectedSummonHp = LegacyMobCombatState.Read(summon.MobSnapshot).CurrentScore.Hp;
        Check(hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(summon.PositionX + 40)), positionY: summon.PositionY,
                  CreateMob("AURAPEACE_OUT", 1_000, 5, skillBar: false)),
            "The outside peaceful summon Bestial Aura peer could not be positioned.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = new LegacyMobAiRuntime(hub, itemData: null, async (outcome, cancellationToken) =>
        {
            outcomes.Add(outcome);
            if (outcome.AffectTick is null)
                return;
            await LegacyMobAiRelay.RelayAsync(
                hub,
                outcome,
                codec,
                sendResurrectionRefresh: null,
                cancellationToken,
                clientTick: 1,
                keywordIndex: 0);
        }, nextRoll: static _ => 0);
        for (var tick = 0; tick < LegacyMobAiRuntime.PeaceShardCount * 4 &&
             outcomes.All(static outcome => outcome.AffectTick is null); tick++)
            await runtime.TickNextShardAsync();

        var expiryTick = outcomes.SingleOrDefault(static outcome => outcome.AffectTick is not null)?.AffectTick;
        Check(expiryTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } &&
              expiryTick.ConnectionId == summon.ConnectionId && expiryTick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "The peaceful summon Type 23 expiry did not produce one score-only timer outcome.");

        foreach (var viewerStream in new[] { leaderReadStream, ownerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == summon.ConnectionId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == expectedSummonHp &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset + 8] == 0,
                "Peace Type 23 expiry must relay only UpdateScore with unchanged HP to the captured summon view.");
        }

        await AssertNoFrameAsync(leaderReadStream);
        await AssertNoFrameAsync(ownerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task CombatSummonBestialAuraExpiryRelaysOnlyScoreToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddRouteFiveOwner(hub, 4, "AURACOMBAT_LEAD", 90, 100, 0);
        AddRouteFiveOwner(hub, 5, "AURACOMBAT_OWNER", 100, 100, 30);
        Check(hub.TrySetParty(4, [4, 5]), "The Combat summon Bestial Aura party could not be formed.");

        var summon = CreateRouteTypeFiveSummon(hub, casterConnectionId: 5);
        SetSummonBestialAura(summon, time: 2);
        var expectedSummonHp = LegacyMobCombatState.Read(summon.MobSnapshot).CurrentScore.Hp;
        var summonClan = summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset];
        Check(hub.SetCharacterState(4, 0, 0, clan: 5, guildLevel: 0, coin: 0, 90, 100,
                  CreateMob("AURACB_LEAD", 0, 5, skillBar: false)) &&
              hub.Enter(1, "AURACB_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "AURACB_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "AURACB_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)) &&
              hub.SetCharacterState(1, 0, 0, summonClan, 0, 0,
                  checked((short)(summon.PositionX + 1)), summon.PositionY,
                  CreateMob("AURACB_TARGET", 0, summonClan, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, summonClan, 0, 0,
                  checked((short)(summon.PositionX + 5)), summon.PositionY,
                  CreateMob("AURACB_VIEW", 0, summonClan, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, summonClan, 0, 0,
                  checked((short)(summon.PositionX + 40)), summon.PositionY,
                  CreateMob("AURACB_OUT", 0, summonClan, skillBar: false)),
            "The Combat summon Bestial Aura view peers could not be registered.");

        LegacyNpcAiApplicationOutcome? expiry = null;
        for (var opportunity = 0; opportunity < 12 && expiry is null; opportunity++)
        {
            Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes),
                "The Combat summon Bestial Aura timer opportunity could not be processed.");
            expiry = outcomes.Select(static outcome => outcome.AffectUpdate)
                .FirstOrDefault(static update => update?.AffectTick is not null);
            Check(outcomes.All(static outcome => outcome.Attack is null && outcome.Despawn is null),
                $"The Combat summon Bestial Aura timer must not synthesize an attack or despawn; opportunity={opportunity + 1}, outcomes=[{string.Join(';', outcomes.Select(static outcome => $"attack={outcome.Attack?.TargetConnectionId}:{outcome.Attack?.Damage},despawn={outcome.Despawn?.Removal?.ConnectionId},affect={outcome.AffectUpdate?.AffectTick is not null}"))}].");
        }

        Check(expiry?.AffectTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } affectTick &&
              affectTick.ConnectionId == summon.ConnectionId &&
              affectTick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "The Combat summon Type 23 expiry did not produce one score-only timer outcome.");
        await LegacyMobAiRelay.RelayAsync(
            hub,
            expiry!,
            codec,
            sendResurrectionRefresh: null,
            CancellationToken.None,
            clientTick: 1,
            keywordIndex: 0);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == summon.ConnectionId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == expectedSummonHp &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset + 8] == 0,
                "Combat Type 23 expiry must relay only UpdateScore to in-view summon peers.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task PeaceSummonThunderExpiryRelaysOnlyScoreToCapturedViewAsync(
        NetworkStream leaderSendStream,
        NetworkStream ownerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream leaderReadStream,
        NetworkStream ownerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        Check(hub.Enter(1, "THUNDER_PEACE_LEAD", (bytes, token) => leaderSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "THUNDER_PEACE_OWNER", (bytes, token) => ownerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "THUNDER_PEACE_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The Peace summon Thunder loopback peers could not be registered.");

        var leaderMob = CreateMob("THP_LEAD", 1_000, 5, skillBar: false);
        var ownerMob = CreateMob("THP_OWNER", 1_000, 5, skillBar: false);
        SetSpecial3(leaderMob, 30);
        SetSpecial3(ownerMob, 30);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 99, 100, leaderMob) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, 100, 100, ownerMob) &&
              hub.TrySetParty(1, [1, 2]),
            "The Peace summon Thunder party could not be initialized.");

        var summon = CreateRouteTypeFiveSummon(hub, casterConnectionId: 2);
        SetSummonThunder(summon, time: 2);
        var expectedSummonHp = LegacyMobCombatState.Read(summon.MobSnapshot).CurrentScore.Hp;
        var summonClan = summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset];
        Check(hub.SetCharacterState(3, 0, 0, summonClan, 0, 0,
                  checked((short)(summon.PositionX + 40)), summon.PositionY,
                  CreateMob("THP_OUT", 1_000, summonClan, skillBar: false)),
            "The outside Peace summon Thunder peer could not be positioned.");

        var outcomes = new List<LegacyNpcAiApplicationOutcome>();
        var runtime = new LegacyMobAiRuntime(hub, itemData: null, async (outcome, cancellationToken) =>
        {
            outcomes.Add(outcome);
            if (outcome.AffectTick is null)
                return;
            await LegacyMobAiRelay.RelayAsync(
                hub,
                outcome,
                codec,
                sendResurrectionRefresh: null,
                cancellationToken,
                clientTick: 1,
                keywordIndex: 0);
        }, nextRoll: static _ => 0);
        for (var tick = 0; tick < LegacyMobAiRuntime.PeaceShardCount * 4 &&
             outcomes.All(static outcome => outcome.AffectTick is null); tick++)
            await runtime.TickNextShardAsync();

        var expiryTick = outcomes.SingleOrDefault(static outcome => outcome.AffectTick is not null)?.AffectTick;
        Check(expiryTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } &&
              expiryTick.ConnectionId == summon.ConnectionId &&
              expiryTick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "The Peace summon Type 22 expiry did not produce one score-only timer outcome.");

        foreach (var viewerStream in new[] { leaderReadStream, ownerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == summon.ConnectionId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == expectedSummonHp &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset + 8] == 0,
                "Peace Type 22 expiry must relay only UpdateScore to in-view summon peers.");
        }

        await AssertNoFrameAsync(leaderReadStream);
        await AssertNoFrameAsync(ownerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task CombatSummonThunderExpiryRelaysOnlyScoreToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddRouteFiveOwner(hub, 4, "THC_LEAD", 90, 100, 0);
        AddRouteFiveOwner(hub, 5, "THC_OWNER", 100, 100, 30);
        Check(hub.TrySetParty(4, [4, 5]), "The Combat summon Thunder party could not be formed.");

        var summon = CreateRouteTypeFiveSummon(hub, casterConnectionId: 5);
        SetSummonThunder(summon, time: 2);
        var summonClan = summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset];
        var summonX = summon.PositionX;
        var summonY = summon.PositionY;
        Check(hub.SetCharacterState(4, 0, 0, clan: 5, guildLevel: 0, coin: 0, 90, 100,
                  CreateMob("THC_LEAD", 0, 5, skillBar: false)) &&
              hub.Enter(1, "THUNDERCOMB_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "THUNDERCOMB_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "THUNDERCOMB_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)) &&
              hub.SetCharacterState(1, 0, 0, summonClan, 0, 0,
                  checked((short)(summonX + 1)), summonY, CreateMob("THC_TARGET", 0, summonClan, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, summonClan, 0, 0,
                  checked((short)(summonX + 5)), summonY, CreateMob("THC_VIEW", 0, summonClan, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, summonClan, 0, 0,
                  checked((short)(summonX + 40)), summonY, CreateMob("THC_OUT", 0, summonClan, skillBar: false)),
            "The Combat summon Thunder view peers could not be registered.");

        var expectedSummonHp = LegacyMobCombatState.Read(summon.MobSnapshot).CurrentScore.Hp;
        LegacyNpcAiApplicationOutcome? expiry = null;
        var opportunities = 0;
        while (opportunities < 12 && expiry is null)
        {
            Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes),
                "The Combat summon Thunder timer opportunity could not be processed.");
            expiry = outcomes.Select(static outcome => outcome.AffectUpdate)
                .FirstOrDefault(static update => update?.AffectTick is not null);
            Check(outcomes.All(static outcome => outcome.Attack is null && outcome.Despawn is null),
                $"Combat Type 22 on a summon must not synthesize a lightning attack or despawn; opportunity={opportunities + 1}, outcomes=[{string.Join(';', outcomes.Select(static outcome => $"attack={outcome.Attack?.TargetConnectionId}:{outcome.Attack?.Damage},despawn={outcome.Despawn?.Removal?.ConnectionId},affect={outcome.AffectUpdate?.AffectTick is not null}"))}].");
            opportunities++;
        }

        Check(opportunities == 8 && expiry?.AffectTick is { Damage: 0, ScoreRefreshRequired: true, HpUpdateRequired: false } affectTick &&
              affectTick.ConnectionId == summon.ConnectionId &&
              affectTick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]),
            "The Combat summon Type 22 expiry did not produce one score-only timer outcome after two affect visits.");
        await LegacyMobAiRelay.RelayAsync(
            hub,
            expiry!,
            codec,
            sendResurrectionRefresh: null,
            CancellationToken.None,
            clientTick: 1,
            keywordIndex: 0);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == summon.ConnectionId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == expectedSummonHp &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset + 8] == 0,
                "Combat Type 22 expiry must relay only UpdateScore to in-view summon peers.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static LegacySummonedMob CreateRouteTypeFiveSummon(WorldHub hub, int casterConnectionId)
    {
        var summonSkill = new LegacySkillDefinition(
            50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "AURA_SUMMON");
        Check(hub.TryApplySkillAttack(casterConnectionId, casterConnectionId, summonSkill,
                  itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The Type 23 loopback fixture could not create its RouteType 5 summon.");
        return summonOutcome!.SummonedMobs!.Single();
    }

    private static void SetSummonBestialAura(LegacySummonedMob summon, uint time)
    {
        summon.AffectSnapshot[8] = 23;
        summon.AffectSnapshot[9] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(summon.AffectSnapshot.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(summon.AffectSnapshot.AsSpan(12), time);
    }

    private static void SetSummonThunder(LegacySummonedMob summon, uint time)
    {
        summon.AffectSnapshot[8] = 22;
        summon.AffectSnapshot[9] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(summon.AffectSnapshot.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(summon.AffectSnapshot.AsSpan(12), time);
    }

    private static void SetSummonPoison(LegacySummonedMob summon, int hp, int maxHp, int learnedSkill, uint time)
    {
        var currentScore = LegacyMobCombatState.Read(summon.MobSnapshot).CurrentScore;
        (currentScore with { Hp = hp, MaxHp = maxHp }).Write(
            summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        BinaryPrimitives.WriteInt32LittleEndian(
            summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobLearnedSkillOffset), learnedSkill);
        summon.AffectSnapshot[8] = 20;
        summon.AffectSnapshot[9] = 100;
        BinaryPrimitives.WriteUInt16LittleEndian(summon.AffectSnapshot.AsSpan(10), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(summon.AffectSnapshot.AsSpan(12), time);
    }

    private static async Task PeaceSummonLifeAuraRelaysHealExpiryAndMovementAsync(
        NetworkStream leaderSendStream,
        NetworkStream ownerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream leaderReadStream,
        NetworkStream ownerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        Check(hub.Enter(1, "LIFE_PEACE_LEAD", (bytes, token) => leaderSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "LIFE_PEACE_OWNER", (bytes, token) => ownerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "LIFE_PEACE_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The peaceful summon Life Aura loopback peers could not be registered.");

        var leaderMob = CreateMob("LP_LEAD", 2_000, 5, skillBar: false);
        var ownerMob = CreateMob("LP_OWNER", 2_000, 5, skillBar: false);
        SetSpecial3(leaderMob, 30);
        SetSpecial3(ownerMob, 30);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 99, 100, leaderMob) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, 100, 100, ownerMob) &&
              hub.TrySetParty(1, [1, 2]),
            "The peaceful summon Life Aura party could not be initialized.");

        var summon = CreateRouteTypeFiveSummon(hub, casterConnectionId: 2);
        SetSummonLifeAura(summon, hp: 980, maxHp: 1_000, time: 2);
        Check(hub.UpdatePosition(2, checked((short)(summon.PositionX + 8)), summon.PositionY) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(summon.PositionX + 40)), positionY: summon.PositionY,
                  CreateMob("LP_OUT", 1_000, 5, skillBar: false)),
            "The peaceful summon Life Aura follower or outside-view peer could not be positioned.");

        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var healOutcome) &&
              healOutcome.AffectTick is { Hp: 1_000, Damage: 20, HpUpdateRequired: true } healTick &&
              healTick.ViewRecipientConnectionIds.Contains(1) && healTick.ViewRecipientConnectionIds.Contains(2) &&
              !healTick.ViewRecipientConnectionIds.Contains(3) &&
              healOutcome.Movement is { ConnectionId: var movementSummonId } && movementSummonId == summon.ConnectionId,
            "Peace Type 17 must combine the heal and follower movement in one captured-view outcome.");
        var movement = healOutcome.Movement!;
        await LegacyMobAiRelay.RelayAsync(
            hub, healOutcome, codec, sendResurrectionRefresh: null, CancellationToken.None,
            clientTick: 1, keywordIndex: 0);

        foreach (var viewerStream in new[] { leaderReadStream, ownerReadStream })
        {
            var hpFrame = await ReadFrameAsync(viewerStream, codec);
            Check(hpFrame.Header.Type == SetHpDamConfirmation.MessageType &&
                  hpFrame.Header.Id == summon.ConnectionId && hpFrame.Header.Size == SetHpDamConfirmation.PacketSize &&
                  hpFrame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span) == 1_000 &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span[4..]) == 20,
                "An in-view peer did not receive summon Life Aura SetHpDam before UpdateScore.");

            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            var peaceScoreHp = BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]);
            var peaceLifeAura = BinaryPrimitives.ReadUInt16LittleEndian(
                scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.AffectOffset + 2)..]);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == summon.ConnectionId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid && peaceScoreHp == 1_000 &&
                  peaceLifeAura == checked((ushort)((17 << 8) | 1)),
                $"The Peace summon score frame did not follow SetHpDam with the decremented Type 17 affect; type={scoreFrame.Header.Type}, id={scoreFrame.Header.Id}, size={scoreFrame.Header.Size}, checksum={scoreFrame.IsChecksumValid}, hp={peaceScoreHp}, slot1=0x{peaceLifeAura:X4}, snapshot-type={healOutcome.AffectTick!.AffectSnapshot[8]}, snapshot-time={BinaryPrimitives.ReadUInt32LittleEndian(healOutcome.AffectTick.AffectSnapshot.AsSpan(12))}, wire-affects={Convert.ToHexString(scoreFrame.Payload.Span.Slice(UpdateScoreConfirmationV769.AffectOffset, 16))}.");

            var actionFrame = await ReadFrameAsync(viewerStream, codec);
            Check(ActionRequest.TryParse(actionFrame, out var action) && action is not null &&
                  action.PositionX == movement.FromX && action.PositionY == movement.FromY &&
                  action.TargetX == movement.ToX && action.TargetY == movement.ToY,
                "The Peace summon movement Action did not follow the Life Aura frames.");
        }

        Check(hub.UpdatePosition(2, checked((short)(movement.ToX + 1)), movement.ToY),
            "The Peace Life Aura owner could not be repositioned beside the summon for expiry.");
        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var expiryOutcome) &&
              expiryOutcome.AffectTick is { Damage: 0, HpUpdateRequired: false, ScoreRefreshRequired: true } expiryTick &&
              expiryTick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]) &&
              expiryOutcome.Movement is null,
            "Peace Type 17 expiry must return a score-only affect outcome when the summon does not move.");
        await LegacyMobAiRelay.RelayAsync(
            hub, expiryOutcome, codec, sendResurrectionRefresh: null, CancellationToken.None,
            clientTick: 2, keywordIndex: 0);

        foreach (var viewerStream in new[] { leaderReadStream, ownerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == summon.ConnectionId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 1_000 &&
                  BinaryPrimitives.ReadUInt16LittleEndian(scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.AffectOffset + 2)..]) == 0,
                "Peace summon expiry at capped HP must relay only UpdateScore.");
        }

        await AssertNoFrameAsync(leaderReadStream);
        await AssertNoFrameAsync(ownerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task CombatSummonLifeAuraRelaysHealAndExpiryAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        AddRouteFiveOwner(hub, 4, "LC_LEAD", 90, 100, 0);
        AddRouteFiveOwner(hub, 5, "LC_OWNER", 100, 100, 30);
        Check(hub.TrySetParty(4, [4, 5]), "The Combat summon Life Aura party could not be formed.");

        var summon = CreateRouteTypeFiveSummon(hub, casterConnectionId: 5);
        SetSummonLifeAura(summon, hp: 980, maxHp: 1_000, time: 2);
        Check(hub.UpdatePosition(4, checked((short)(summon.PositionX + 30)), summon.PositionY),
            "The Combat summon Life Aura leader could not be moved outside the target-selection view.");
        var summonClan = summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset];
        Check(hub.Enter(1, "LC_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "LC_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "LC_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)) &&
              hub.SetCharacterState(1, 0, 0, summonClan, 0, 0,
                  checked((short)(summon.PositionX + 1)), summon.PositionY,
                  CreateMob("LC_TARGET", 0, summonClan, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, summonClan, 0, 0,
                  checked((short)(summon.PositionX + 5)), summon.PositionY,
                  CreateMob("LC_VIEW", 0, summonClan, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, summonClan, 0, 0,
                  checked((short)(summon.PositionX + 40)), summon.PositionY,
                  CreateMob("LC_OUT", 0, summonClan, skillBar: false)),
            "The Combat summon Life Aura view peers could not be registered.");

        for (var opportunity = 0; opportunity < 3; opportunity++)
        {
            Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes) &&
                  outcomes.All(static outcome => outcome.Attack is null && outcome.Despawn is null && outcome.AffectUpdate is null),
                $"Combat Type 17 must wait for the fourth summon affect opportunity without attacking or relaying early; opportunity={opportunity + 1}, attacks={outcomes.Count(static outcome => outcome.Attack is not null)}, despawns={outcomes.Count(static outcome => outcome.Despawn is not null)}, affect-updates={outcomes.Count(static outcome => outcome.AffectUpdate is not null)}.");
        }
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);

        Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var healOutcomes),
            "The fourth Combat summon Life Aura opportunity could not be processed.");
        var healOutcome = healOutcomes.Select(static value => value.AffectUpdate)
            .FirstOrDefault(static update => update?.AffectTick is not null);
        Check(healOutcome?.AffectTick is { Hp: 1_000, Damage: 20, HpUpdateRequired: true } healTick &&
              healTick.ViewRecipientConnectionIds.Contains(1) && healTick.ViewRecipientConnectionIds.Contains(2) &&
              !healTick.ViewRecipientConnectionIds.Contains(3) &&
              healOutcomes.All(static outcome => outcome.Attack is null && outcome.Despawn is null),
            "The fourth Combat opportunity must heal the summon and capture only its current view without attack.");
        await LegacyMobAiRelay.RelayAsync(
            hub, healOutcome!, codec, sendResurrectionRefresh: null, CancellationToken.None,
            clientTick: 1, keywordIndex: 0);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var hpFrame = await ReadFrameAsync(viewerStream, codec);
            Check(hpFrame.Header.Type == SetHpDamConfirmation.MessageType &&
                  hpFrame.Header.Id == summon.ConnectionId && hpFrame.Header.Size == SetHpDamConfirmation.PacketSize &&
                  hpFrame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span) == 1_000 &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span[4..]) == 20,
                "An in-view Combat peer did not receive Life Aura SetHpDam before UpdateScore.");

            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == summon.ConnectionId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 1_000 &&
                  BinaryPrimitives.ReadUInt16LittleEndian(scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.AffectOffset + 2)..]) ==
                  checked((ushort)((17 << 8) | 1)),
                "The Combat summon score frame did not follow SetHpDam with the decremented Type 17 affect.");
        }

        for (var opportunity = 0; opportunity < 3; opportunity++)
        {
            Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes) &&
                  outcomes.All(static outcome => outcome.Attack is null && outcome.Despawn is null && outcome.AffectUpdate is null),
                "Combat Type 17 must wait four opportunities between timer updates without attacking.");
        }
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);

        Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var expiryOutcomes),
            "The eighth Combat summon Life Aura opportunity could not be processed.");
        var expiryOutcome = expiryOutcomes.Select(static value => value.AffectUpdate)
            .FirstOrDefault(static update => update?.AffectTick is not null);
        Check(expiryOutcome?.AffectTick is { Hp: 1_000, Damage: 0, HpUpdateRequired: false, ScoreRefreshRequired: true } expiryTick &&
              expiryTick.AffectSnapshot.AsSpan(8, 8).SequenceEqual(new byte[8]) &&
              expiryOutcomes.All(static outcome => outcome.Attack is null && outcome.Despawn is null),
            "The eighth Combat opportunity must expire Type 17 at capped HP without an HP frame or attack.");
        await LegacyMobAiRelay.RelayAsync(
            hub, expiryOutcome!, codec, sendResurrectionRefresh: null, CancellationToken.None,
            clientTick: 2, keywordIndex: 0);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == summon.ConnectionId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 1_000 &&
                  BinaryPrimitives.ReadUInt16LittleEndian(scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.AffectOffset + 2)..]) == 0,
                "Combat summon expiry at capped HP must relay only UpdateScore.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task PeaceSummonPoisonRelaysMountHpThenAffectFramesAsync(
        NetworkStream leaderSendStream,
        NetworkStream ownerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream leaderReadStream,
        NetworkStream ownerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        Check(hub.Enter(1, "PZ_LEADER", (bytes, token) => leaderSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "PZ_OWNER", (bytes, token) => ownerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "PZ_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The Poison mount loopback peers could not be registered.");

        var leaderMob = CreateMob("PZ_LEADER", 2_000, 5, skillBar: false);
        var ownerMob = CreateMob("PZ_OWNER", 2_000, 5, skillBar: false);
        SetSpecial3(leaderMob, 30);
        SetSpecial3(ownerMob, 30);
        var mountOffset = LegacyAccountSnapshot.MobEquipmentOffset + (14 * LegacyItem.SizeInBytes);
        BinaryPrimitives.WriteInt16LittleEndian(ownerMob.AsSpan(mountOffset), 2_330);
        BinaryPrimitives.WriteInt16LittleEndian(ownerMob.AsSpan(mountOffset + 2), 100);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 99, 100, leaderMob) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, 100, 100, ownerMob) &&
              hub.TrySetParty(1, [1, 2]),
            "The Poison mount party could not be initialized.");

        var summon = CreateRouteTypeFiveSummon(hub, casterConnectionId: 2);
        summon.MobSnapshot[LegacyAccountSnapshot.MobClanOffset] = 4;
        BinaryPrimitives.WriteInt16LittleEndian(
            summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset), 315);
        SetSummonPoison(summon, hp: 10_000, maxHp: 10_000, learnedSkill: 0, time: 2);
        Check(hub.UpdatePosition(2, summon.PositionX, summon.PositionY) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(summon.PositionX + 40)), positionY: summon.PositionY,
                  CreateMob("PZ_OUT", 1_000, 5, skillBar: false)),
            "The Poison mount owner or outside-view peer could not be positioned.");

        Check(hub.TryApplyRouteTypeFiveSummonPeaceStep(summon.ConnectionId, static () => 0, out var outcome) &&
              outcome.Movement is null &&
              outcome.AffectTick is { Hp: 9_000, Damage: -1_000, HpUpdateRequired: true, MountOwnerConnectionId: 2, UpdatedMountItem: not null } tick &&
              tick.ViewRecipientConnectionIds.Contains(1) && tick.ViewRecipientConnectionIds.Contains(2) &&
              !tick.ViewRecipientConnectionIds.Contains(3),
            "Peace Type 20 must update the linked mount and capture only the summon view before relay.");
        await LegacyMobAiRelay.RelayAsync(
            hub, outcome, codec, sendResurrectionRefresh: null, CancellationToken.None,
            clientTick: 1, keywordIndex: 0);

        var itemFrame = await ReadFrameAsync(ownerReadStream, codec);
        Check(SendItemConfirmation.TryParse(itemFrame, out var mountItem) && mountItem is not null &&
              itemFrame.Header.Id == 2 && itemFrame.Header.Size == SendItemConfirmation.PacketSize &&
              mountItem.InventoryType == 0 && mountItem.Slot == 14 && mountItem.Item.Index == 2_330,
            "The Clan 4 Poison owner did not receive the equipment SendItem before summon damage frames.");
        var mountBytes = new byte[LegacyItem.SizeInBytes];
        mountItem!.Item.Write(mountBytes);
        Check(BinaryPrimitives.ReadInt16LittleEndian(mountBytes.AsSpan(2)) == 9_000,
            "The equipment SendItem did not contain the poisoned summon HP in the linked mount item.");

        foreach (var viewerStream in new[] { leaderReadStream, ownerReadStream })
        {
            var hpFrame = await ReadFrameAsync(viewerStream, codec);
            Check(hpFrame.Header.Type == SetHpDamConfirmation.MessageType &&
                  hpFrame.Header.Id == summon.ConnectionId && hpFrame.Header.Size == SetHpDamConfirmation.PacketSize &&
                  hpFrame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span) == 9_000 &&
                  BinaryPrimitives.ReadInt32LittleEndian(hpFrame.Payload.Span[4..]) == -1_000,
                "An in-view peer did not receive Poison SetHpDam after the owner mount item.");

            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == summon.ConnectionId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 9_000 &&
                  BinaryPrimitives.ReadUInt16LittleEndian(scoreFrame.Payload.Span[(UpdateScoreConfirmationV769.AffectOffset + 2)..]) ==
                  checked((ushort)((20 << 8) | 1)),
                "An in-view peer did not receive the Type 20 score after SetHpDam.");
        }

        await AssertNoFrameAsync(leaderReadStream);
        await AssertNoFrameAsync(ownerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static void SetSummonLifeAura(LegacySummonedMob summon, int hp, int maxHp, uint time)
    {
        var currentScore = LegacyScore.Read(summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        (currentScore with { Hp = hp, MaxHp = maxHp }).Write(summon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        summon.AffectSnapshot[8] = 17;
        summon.AffectSnapshot[9] = 20;
        BinaryPrimitives.WriteUInt16LittleEndian(summon.AffectSnapshot.AsSpan(10), 40);
        BinaryPrimitives.WriteUInt32LittleEndian(summon.AffectSnapshot.AsSpan(12), time);
    }

    private static void SetSpecial3(byte[] mob, int value)
    {
        var baseScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        (baseScore with { Special3 = checked((short)value) }).Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        var currentScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        (currentScore with { Special3 = checked((short)value) }).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
    }

    private static async Task PeaceSummonExpiryRelaysRemoveMobOnlyToCapturedViewAsync(
        NetworkStream leaderSendStream,
        NetworkStream ownerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream leaderReadStream,
        NetworkStream ownerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        Check(hub.Enter(1, "EXP_LEAD", (bytes, token) => leaderSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "SUMEXP", (bytes, token) => ownerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "EXP_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The summon-expiry loopback peers could not be registered.");

        var leaderMob = CreateMob("EXP_LEAD", 1_000, 5, skillBar: false);
        var ownerMob = CreateMob("SUMEXP", 1_000, 5, skillBar: false);
        foreach (var mob in new[] { leaderMob, ownerMob })
        {
            var baseScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
            (baseScore with { Special3 = 30 }).Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
            var currentScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            (currentScore with { Special3 = 30 }).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        }

        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 1_990, 2_000, leaderMob) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, 2_000, 2_000, ownerMob) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0, 2_017, 2_000,
                  CreateMob("EXP_OUT", 1_000, 5, skillBar: false)) &&
              hub.TrySetParty(1, [1, 2]),
            "The summon-expiry party and view positions could not be initialized.");

        var summonSkill = new LegacySkillDefinition(
            50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        Check(hub.TryApplySkillAttack(2, 2, summonSkill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The loopback Type 24 timer could not create its RouteType 5 summon.");
        var summon = summonOutcome!.SummonedMobs!.Single();

        var runtime = new LegacyMobAiRuntime(hub, itemData: null, async (outcome, cancellationToken) =>
        {
            await LegacyMobAiRelay.RelayAsync(
                hub,
                outcome,
                codec,
                sendResurrectionRefresh: null,
                cancellationToken,
                clientTick: 1,
                keywordIndex: 0);
        }, nextRoll: static _ => 0);
        for (var tick = 0; tick < LegacyMobAiRuntime.PeaceShardCount * 20; tick++)
            await runtime.TickNextShardAsync();

        Check(!hub.TryGetSummonEnemyList(summon.ConnectionId, out _),
            "The twentieth peaceful Type 24 timer opportunity did not remove the summon from WorldHub.");
        foreach (var viewerStream in new[] { leaderReadStream, ownerReadStream })
        {
            var removal = await ReadFrameAsync(viewerStream, codec);
            Check(removal.Header.Type == RemoveMobConfirmation.MessageType && removal.Header.Id == summon.ConnectionId &&
                  removal.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(removal.Payload.Span) == 3,
                "A captured-view peer did not receive Type 24 expiry as RemoveMob(type=3).");
        }

        await AssertNoFrameAsync(leaderReadStream);
        await AssertNoFrameAsync(ownerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task CombatSummonExpiryRelaysRemoveMobOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        Check(hub.Enter(1, "CBEXP_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "CBEXP_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "CBEXP_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The Combat summon-expiry loopback peers could not be registered.");
        AddRouteFiveOwner(hub, 4, "CBEXP_LEAD", 90, 100, 0);
        AddRouteFiveOwner(hub, 5, "CBEXP_CASTER", 100, 100, 30);
        Check(hub.TrySetParty(4, [4, 5]), "The Combat summon-expiry loopback party could not be formed.");

        var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        Check(hub.TryApplySkillAttack(5, 5, skill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The Combat Type 24 loopback fixture could not create its RouteType 5 summon.");
        var summon = summonOutcome!.SummonedMobs!.Single();

        var targetMob = CreateMob("CBEXP_TARGET", 100_000, clan: 5, skillBar: false);
        var observerMob = CreateMob("CBEXP_VIEW", 1_000, clan: 5, skillBar: false);
        var outsideMob = CreateMob("CBEXP_OUT", 1_000, clan: 5, skillBar: false);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(summon.PositionX + 1)), positionY: summon.PositionY, targetMob) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(summon.PositionX + 5)), positionY: summon.PositionY, observerMob) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(summon.PositionX + 40)), positionY: summon.PositionY, outsideMob),
            "The Combat summon-expiry target/view positions could not be initialized.");

        for (var affectOpportunity = 0; affectOpportunity < 19; affectOpportunity++)
        for (var battleOpportunity = 0; battleOpportunity < 4; battleOpportunity++)
        {
            Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes) &&
                  outcomes is [{ Attack: { Damage: > 0 }, Despawn: null }],
                $"The loopback summon must remain in Combat through Type 24 opportunity {affectOpportunity + 1}, battle step {battleOpportunity + 1}.");
        }

        for (var battleOpportunity = 0; battleOpportunity < 3; battleOpportunity++)
        {
            Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes) &&
                  outcomes is [{ Attack: { Damage: > 0 }, Despawn: null }],
                $"The twentieth loopback Type 24 opportunity must wait for battle step {battleOpportunity + 1} of four.");
        }

        Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var expiry) &&
              expiry is [{ Movement: null, Attack: null, Despawn: { DecisionKind: LegacyMobAiDecisionKind.Despawn,
                  Removal: { RemoveType: 3 } expiryRemoval } }] &&
              expiryRemoval.ConnectionId == summon.ConnectionId &&
              !hub.TryGetSummonEnemyList(summon.ConnectionId, out _),
            "Combat Type 24 expiry must remove the summon before its twentieth opportunity's attack.");
        await LegacyMobAiRelay.RelayAsync(
            hub,
            expiry.Single().Despawn!,
            codec,
            sendResurrectionRefresh: null,
            CancellationToken.None,
            clientTick: 1,
            keywordIndex: 0);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var removalFrame = await ReadFrameAsync(viewerStream, codec);
            Check(removalFrame.Header.Type == RemoveMobConfirmation.MessageType && removalFrame.Header.Id == summon.ConnectionId &&
                  removalFrame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(removalFrame.Payload.Span) == 3,
                "An in-view peer did not receive Combat Type 24 expiry as RemoveMob(type=3).");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);

        Check(hub.TryApplySkillAttack(5, 5, skill, itemData: null, weather: 0, randomFactor: 90, out var targetlessOutcome) ==
              LegacySkillAttackResult.Accepted && targetlessOutcome?.SummonedMobs is { Count: 1 },
            "The targetless Combat loopback fixture could not create its second RouteType 5 summon.");
        var targetlessSummon = targetlessOutcome!.SummonedMobs!.Single();
        Check(targetlessSummon.AffectSnapshot[0] == 24 &&
              BinaryPrimitives.ReadUInt32LittleEndian(targetlessSummon.AffectSnapshot.AsSpan(4)) == 20 &&
              hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(targetlessSummon.PositionX + 1)), positionY: targetlessSummon.PositionY,
                  CreateMob("CBEXP_TARGET", 0, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(targetlessSummon.PositionX + 5)), positionY: targetlessSummon.PositionY,
                  CreateMob("CBEXP_VIEW", 0, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(4, 0, 0, clan: 5, guildLevel: 0, coin: 0, positionX: 90, positionY: 100,
                  CreateMob("CBEXP_LEAD", 0, clan: 5, skillBar: false)),
            "The targetless Combat loopback timer or dead in-view players could not be initialized.");
        Check(hub.TrySelectSummonTarget(targetlessSummon.ConnectionId, out var targetlessSelection) &&
              targetlessSelection is { TargetConnectionId: 0 },
            "The Combat Type 24 loopback summon must have no live target during its timer run.");

        for (var affectOpportunity = 0; affectOpportunity < 19; affectOpportunity++)
        for (var battleOpportunity = 0; battleOpportunity < 4; battleOpportunity++)
        {
            Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes) && outcomes.Count == 0,
                $"A targetless Combat loopback summon must remain idle through Type 24 opportunity {affectOpportunity + 1}, battle step {battleOpportunity + 1}.");
        }

        Check(hub.TryGetSummonEnemyList(targetlessSummon.ConnectionId, out _),
            "The targetless Combat loopback summon must remain present after nineteen Type 24 opportunities.");
        for (var battleOpportunity = 0; battleOpportunity < 3; battleOpportunity++)
        {
            Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var outcomes) && outcomes.Count == 0,
                $"The twentieth targetless loopback Type 24 opportunity must wait for step {battleOpportunity + 1} of four.");
        }

        Check(hub.TryGetSummonEnemyList(targetlessSummon.ConnectionId, out _),
            "The targetless Combat loopback summon must remain present before its twentieth Type 24 opportunity.");
        Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var targetlessExpiry) &&
              targetlessExpiry is [{ Movement: null, Attack: null, Despawn: { DecisionKind: LegacyMobAiDecisionKind.Despawn,
                  Removal: { RemoveType: 3 } targetlessRemoval } }] &&
              targetlessRemoval.ConnectionId == targetlessSummon.ConnectionId &&
              !hub.TryGetSummonEnemyList(targetlessSummon.ConnectionId, out _),
            "The targetless Combat summon must expire before any attack on its twentieth Type 24 timer opportunity.");
        await LegacyMobAiRelay.RelayAsync(
            hub,
            targetlessExpiry.Single().Despawn!,
            codec,
            sendResurrectionRefresh: null,
            CancellationToken.None,
            clientTick: 2,
            keywordIndex: 0);

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var removalFrame = await ReadFrameAsync(viewerStream, codec);
            Check(removalFrame.Header.Type == RemoveMobConfirmation.MessageType && removalFrame.Header.Id == targetlessSummon.ConnectionId &&
                  removalFrame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(removalFrame.Payload.Span) == 3,
                "An in-view peer did not receive targetless Combat Type 24 expiry as RemoveMob(type=3).");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task DeadPeaceAndIdleGeneratedNpcsRelayTypeOneRemovalAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "DEAD_NPC_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "DEAD_NPC_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "DEAD_NPC_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The dead generated-NPC cleanup loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("DEAD_NPC_TARGET", 1_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("DEAD_NPC_VIEW", 1_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 27, positionY: 26, CreateMob("DEAD_NPC_OUTSIDE", 1_000, clan: 5, skillBar: false)),
            "The dead generated-NPC cleanup loopback player positions could not be initialized.");

        var peaceId = hub.EnterNpc(CreateMob("DEAD_PEACE", 0, clan: 2, skillBar: false),
            10, 10, requestedConnectionId: 20_002, generateIndex: 42);
        var idleId = hub.EnterNpc(CreateMob("DEAD_IDLE", 0, clan: 2, skillBar: false),
            10, 10, requestedConnectionId: 20_008, generateIndex: 42);
        Check(peaceId == 20_002 && idleId == 20_008 &&
              hub.SetNpcCombatState(idleId, LegacyNpcMode.Idle, currentTarget: 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The loopback dead generated Peace/Idle NPCs could not be registered.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickNextShardAsync();
        Check(tick.PeaceShardIndex == 0 && tick.VisitedPeaceNpcIds.Contains(peaceId) &&
              tick.VisitedPeaceNpcIds.Contains(idleId) &&
              !hub.TryGetNpcSnapshot(peaceId, out _) && !hub.TryGetNpcSnapshot(idleId, out _),
            "The Peace shard did not remove both dead generated NPC modes before the Combat stage.");

        await AssertTypeOneRemovalAsync(targetReadStream, peaceId, codec, "hostile target");
        await AssertTypeOneRemovalAsync(targetReadStream, idleId, codec, "hostile target");
        await AssertTypeOneRemovalAsync(observerReadStream, peaceId, codec, "in-view observer");
        await AssertTypeOneRemovalAsync(observerReadStream, idleId, codec, "in-view observer");
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task GeneratedNpcTypeTwentyFourExpiryRelaysTypeThreeRemovalAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "TYPE24_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "TYPE24_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "TYPE24_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The generated-NPC Type 24 loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("TYPE24_TARGET", 1_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("TYPE24_VIEW", 1_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 27, positionY: 26, CreateMob("TYPE24_OUTSIDE", 1_000, clan: 5, skillBar: false)),
            "The generated-NPC Type 24 loopback peer positions could not be initialized.");

        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 24;
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        var npcId = hub.EnterNpc(CreateMob("TYPE24_NPC", 1_000, clan: 2, skillBar: false),
            10, 10, affect, requestedConnectionId: 20_002, generateIndex: 42);
        Check(npcId == 20_002 && hub.SetNpcCombatState(
                  npcId, LegacyNpcMode.Idle, currentTarget: 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated-NPC Type 24 loopback fixture could not be registered as an Idle mob.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        for (var tick = 0; tick < LegacyMobAiRuntime.PeaceShardCount; tick++)
            await runtime.TickNextShardAsync();

        Check(!hub.TryGetNpcSnapshot(npcId, out _),
            "The first eligible Idle/Peace Type 24 timer visit did not remove the generated NPC.");
        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var removal = await ReadFrameAsync(viewerStream, codec);
            Check(removal.Header.Type == RemoveMobConfirmation.MessageType && removal.Header.Id == npcId &&
                  removal.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(removal.Payload.Span) == 3,
                "An in-view peer did not receive generated-NPC Type 24 expiry as RemoveMob(type=3).");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task GeneratedNpcTypeThirtyTwoExpiryRelaysScoreToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "TYPE32_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "TYPE32_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "TYPE32_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The Type 32 loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("TYPE32_TARGET", 1_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 26, positionY: 26, CreateMob("TYPE32_VIEW", 1_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: 27, positionY: 26, CreateMob("TYPE32_OUTSIDE", 1_000, clan: 5, skillBar: false)),
            "The Type 32 loopback peer positions could not be initialized.");

        var affect = new byte[LegacyAccountSnapshot.AffectStride];
        affect[0] = 32;
        affect[1] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(affect.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(affect.AsSpan(4), 1);
        const int npcId = 20_002;
        Check(hub.EnterNpc(
                  CreateMob("TYPE32_NPC", 1_000, clan: 2, skillBar: false), 10, 10, affect,
                  requestedConnectionId: npcId, generateIndex: 42) == npcId &&
              hub.SetNpcCombatState(npcId, LegacyNpcMode.Idle, 0, new int[LegacyMobAiPlanner.EnemyCapacity]),
            "The generated Idle Type 32 NPC could not be registered.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        for (var tick = 0; tick < LegacyMobAiRuntime.PeaceShardCount; tick++)
            await runtime.TickNextShardAsync();

        Check(hub.TryGetNpcSnapshot(npcId, out var expiredNpc) && expiredNpc is not null &&
              LegacyMobCombatState.Read(expiredNpc.MobSnapshot).CurrentScore.Hp == 1_000 &&
              expiredNpc.AffectSnapshot.AsSpan(0, 8).SequenceEqual(new byte[8]),
            "The first eligible Idle/Peace Type 32 timer visit did not clear the affect without changing HP.");
        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var scoreFrame = await ReadFrameAsync(viewerStream, codec);
            Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType &&
                  scoreFrame.Header.Id == npcId && scoreFrame.Header.Size == UpdateScoreConfirmationV769.PacketSize &&
                  scoreFrame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(scoreFrame.Payload.Span[24..]) == 1_000 &&
                  scoreFrame.Payload.Span[UpdateScoreConfirmationV769.AffectOffset] == 0,
                "Type 32 expiry must relay only UpdateScore to in-view peers.");
        }

        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task RunequestLv6AreaDespawnRelaysRemoveMobToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        const short npcX = 3423;
        const short npcY = 1492;
        var template = CreateMob("PSTA_MOB", 1_000, clan: 4, skillBar: false);
        var catalog = LegacyNpcGenerationCatalog.Parse(
            """
            #[4284]
            MaxNumMob: 10
            MinGroup: 0
            MaxGroup: 0
            Leader: PSTA_MOB
            RouteType: 5
            StartX: 10
            StartY: 10
            StartWait: 0
            Segment1X: 20
            Segment1Y: 10
            DestX: 30
            DestY: 10
            """,
            new Dictionary<string, byte[]> { ["PSTA_MOB"] = template });
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        Check(hub.Enter(1, "PSTA_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "PSTA_BOUNDARY", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "PSTA_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The Runequest Lv6 loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: npcX, positionY: checked((short)(npcY + 1)), CreateMob("TARGET", 100_000, 5, false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(npcX + 16)), positionY: checked((short)(npcY + 16)), CreateMob("BOUNDARY", 1_000, 5, false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(npcX + 17)), positionY: checked((short)(npcY + 16)), CreateMob("OUTSIDE", 1_000, 5, false)) &&
              hub.ConfigurePistaLv6State(1, mobCount: 1),
            "The Runequest Lv6 loopback world state could not be initialized.");
        Check(hub.TrySpawnGeneratedNpc(4284, out var npc, randomRoll: static _ => 0,
                  positionX: npcX, positionY: npcY, forceGenerationRoutePosition: true) && npc is not null &&
              hub.SetNpcCombatState(npc.ConnectionId, LegacyNpcMode.Combat, 0, [0]),
            "The Runequest Lv6 loopback NPC could not enter targetless Combat.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npc!.ConnectionId % LegacyMobAiRuntime.ShardCount);
        Check(tick.VisitedNpcIds.Contains(npc.ConnectionId) &&
              hub.TryGetPistaLv6State(out var pistaState) && pistaState?.MobCount == 2 &&
              !hub.TryGetNpcSnapshot(npc.ConnectionId, out _),
            "The Combat timer did not remove the in-area Runequest Lv6 mob and increment its second-wave count.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var frame = await ReadFrameAsync(viewerStream, codec);
            Check(frame.Header.Type == RemoveMobConfirmation.MessageType &&
                  frame.Header.Id == npc.ConnectionId && frame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 3,
                "An in-view loopback peer did not receive the Runequest Lv6 RemoveMob(type=3) frame.");
        }
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task RunequestLv2ExitMovementRelaysActionBeforeRemoveAsync(
        NetworkStream sharedSendStream,
        NetworkStream enteringSendStream,
        NetworkStream outsideSendStream,
        NetworkStream sharedReadStream,
        NetworkStream enteringReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        const short fromX = 3372;
        const short moveY = 1420;
        const short toX = 3373;
        var template = CreateMob("PSTA_LV2_MOB", 1_000, clan: 2, skillBar: false);
        var baseScore = LegacyScore.Read(template.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset)) with
        {
            AttackRun = 1,
            Strength = 10
        };
        baseScore.Write(template.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        var currentScore = LegacyScore.Read(template.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset)) with { AttackRun = 1 };
        currentScore.Write(template.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        var catalog = LegacyNpcGenerationCatalog.Parse(
            """
            #[4306]
            MaxNumMob: 1
            Leader: PSTA_LV2_MOB
            RouteType: 0
            StartX: 3373
            StartY: 1420
            StartRange: 0
            StartWait: 0
            Segment1X: 3375
            Segment1Y: 1420
            DestX: 3377
            DestY: 1420
            """,
            new Dictionary<string, byte[]> { ["PSTA_LV2_MOB"] = template });
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        Check(hub.Enter(1, "LV2_SHARED", (bytes, token) => sharedSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "LV2_ENTER", (bytes, token) => enteringSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "LV2_OUT", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The Pista Lv2 movement loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: toX, positionY: checked((short)(moveY + 1)), CreateMob("LV2_SHARED", 1_000, 2, false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: checked((short)(toX + 16)), positionY: moveY, CreateMob("LV2_ENTER", 1_000, 2, false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: checked((short)(toX + 17)), positionY: moveY, CreateMob("LV2_OUT", 1_000, 2, false)),
            "The Pista Lv2 movement loopback views could not be initialized.");
        Check(hub.TrySpawnGeneratedNpc(4306, out var npc, randomRoll: static _ => 0,
                  positionX: fromX, positionY: moveY) && npc is not null,
            "The Pista Lv2 movement loopback NPC could not be spawned before its exit step.");
        var spawnedNpc = npc!;

        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: static _ => 2);
        LegacyMobAiTickResult? visitedTick = null;
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = await runtime.TickNextShardAsync();
            if (tick.VisitedPeaceNpcIds.Contains(spawnedNpc.ConnectionId))
            {
                visitedTick = tick;
                break;
            }
        }
        Check(visitedTick is not null && !hub.TryGetNpcSnapshot(spawnedNpc.ConnectionId, out _),
            "The peaceful Pista Lv2 route did not remove its NPC after moving into the exit rectangle.");

        var sharedActionFrame = await ReadFrameAsync(sharedReadStream, codec);
        Check(sharedActionFrame.Header.Type == ActionRequest.MessageType &&
              sharedActionFrame.Header.Id == spawnedNpc.ConnectionId &&
              ActionRequest.TryParse(sharedActionFrame, out var sharedAction) &&
              sharedAction is { PositionX: fromX, PositionY: moveY, TargetX: toX, TargetY: moveY },
            "The existing in-view peer did not receive the Pista Lv2 movement Action first.");
        var sharedRemoval = await ReadFrameAsync(sharedReadStream, codec);
        Check(sharedRemoval.Header.Type == RemoveMobConfirmation.MessageType &&
              sharedRemoval.Header.Id == spawnedNpc.ConnectionId && sharedRemoval.IsChecksumValid &&
              BinaryPrimitives.ReadInt32LittleEndian(sharedRemoval.Payload.Span) == 3,
            "The shared post-move viewer did not receive RemoveMob(type=3) after the Action.");

        var enteringCreateFrame = await ReadFrameAsync(enteringReadStream, codec);
        Check(enteringCreateFrame.Header.Type == CreateMobConfirmation.MessageType &&
              BinaryPrimitives.ReadUInt16LittleEndian(enteringCreateFrame.Payload.Span.Slice(4, sizeof(ushort))) == spawnedNpc.ConnectionId,
            "The entering viewer did not receive the moved NPC CreateMob frame before its removal.");
        var enteringActionFrame = await ReadFrameAsync(enteringReadStream, codec);
        Check(enteringActionFrame.Header.Type == ActionRequest.MessageType &&
              ActionRequest.TryParse(enteringActionFrame, out var enteringAction) &&
              enteringAction is { PositionX: fromX, PositionY: moveY, TargetX: toX, TargetY: moveY },
            "The entering viewer did not receive the movement Action after CreateMob.");
        var enteringRemoval = await ReadFrameAsync(enteringReadStream, codec);
        Check(enteringRemoval.Header.Type == RemoveMobConfirmation.MessageType &&
              enteringRemoval.Header.Id == spawnedNpc.ConnectionId && enteringRemoval.IsChecksumValid &&
              BinaryPrimitives.ReadInt32LittleEndian(enteringRemoval.Payload.Span) == 3,
            "The entering post-move viewer did not receive RemoveMob(type=3) after movement.");
        await AssertNoFrameAsync(sharedReadStream);
        await AssertNoFrameAsync(enteringReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task GeneratedNpcGroupCreateFramesUseEachNpcViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var leaderTemplate = CreateMob("GROUP_LEADER", 1000, clan: 1, skillBar: false);
        var followerTemplate = CreateMob("GROUP_FOLLOWER@", 500, clan: 1, skillBar: false);
        var catalog = LegacyNpcGenerationCatalog.Parse(
            """
            #[701]
            MinuteGenerate: 500
            MaxNumMob: 10
            MinGroup: 2
            MaxGroup: 2
            Leader: GROUP_LEADER
            Follower: GROUP_FOLLOWER@
            RouteType: 1
            StartX: 703
            StartY: 702

            #[702]
            MinuteGenerate: 0
            MaxNumMob: 10
            MinGroup: 0
            MaxGroup: 0
            Leader: ROUTE_LEADER
            Follower: ROUTE_FOLLOWER@
            RouteType: 2
            StartX: 100
            StartY: 100
            StartWait: 13
            """,
            new Dictionary<string, byte[]>
            {
                ["GROUP_LEADER"] = leaderTemplate,
                ["GROUP_FOLLOWER@"] = followerTemplate,
                ["ROUTE_LEADER"] = CreateMob("ROUTE_LEADER", 1000, clan: 1, skillBar: false),
                ["ROUTE_FOLLOWER@"] = CreateMob("ROUTE_FOLLOWER@", 500, clan: 1, skillBar: false),
            });
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        Check(hub.Enter(1, "GROUP_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "GROUP_VIEWER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "GROUP_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "Could not register the generated-group loopback peers.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 100, positionY: 100, CreateMob("GROUP_TARGET", 1000, clan: 1, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 115, positionY: 115, CreateMob("GROUP_VIEWER", 1000, clan: 1, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 1, guildLevel: 0, coin: 0,
                  positionX: 118, positionY: 100, CreateMob("GROUP_OUTSIDE", 1000, clan: 1, skillBar: false)),
            "Could not set generated-group loopback peer positions.");
        var runtime = new LegacyNpcGenerationRuntime(
            hub,
            catalog,
            (generatedGroup, cancellationToken) => new ValueTask(LegacyNpcGenerationRelay.RelayGroupAsync(
                hub,
                generatedGroup,
                npc =>
                {
                    var sourcePayload = new CreateMobConfirmation(
                        checked((ushort)npc.ConnectionId), npc.PositionX, npc.PositionY,
                        npc.MobSnapshot, npc.AffectSnapshot, npc: true).ToPayload();
                    return W2ppCreateMobV1Adapter.AdaptPayload(sourcePayload).ToFrame(codec, 1, 0);
                },
                cancellationToken)),
            static _ => 0,
            initialMinuteCounter: 201);
        var minute = await runtime.TickMinuteAsync(CancellationToken.None);
        Check(minute.MinuteCounter == 201 && minute.DueGenerateIndices.SequenceEqual([701]) &&
              minute.SpawnedGroups is [{ GenerateIndex: 701, RouteGenerateIndex: 702, Members.Count: 3 }] &&
              minute.SpawnedGroups[0].Members.All(static npc => npc.GenerateIndex == 702 && npc.GenerationOwnerIndex == 701),
            "The scheduled runtime did not relocate the source generator's leader and two-follower group onto the selected route.");

        var generatedGroup = minute.SpawnedGroups[0].Members;
        var expectedNpcIds = generatedGroup.Select(static npc => npc.ConnectionId).ToArray();

        async Task AssertGroupReceivedInOrderAsync(NetworkStream stream, string peerName)
        {
            var receivedNpcIds = new int[expectedNpcIds.Length];
            for (var index = 0; index < receivedNpcIds.Length; index++)
            {
                var frame = await ReadFrameAsync(stream, codec);
                Check(frame.Header.Type == CreateMobConfirmationV769.MessageType && frame.IsChecksumValid &&
                      frame.Payload.Length == CreateMobConfirmationV769.PayloadSize &&
                      BinaryPrimitives.ReadUInt16LittleEndian(frame.Payload.Span[CreateMobConfirmationV769.CreateTypeOffset..]) == 2,
                    $"{peerName} did not receive a valid generated-NPC CreateMob frame with create type 2.");
                receivedNpcIds[index] = BinaryPrimitives.ReadUInt16LittleEndian(
                    frame.Payload.Span[CreateMobConfirmationV769.MobIdOffset..]);
            }

            Check(receivedNpcIds.SequenceEqual(expectedNpcIds),
                $"{peerName} did not receive the generated group in leader/follower order.");
        }

        await AssertGroupReceivedInOrderAsync(targetReadStream, "the group target");
        await AssertGroupReceivedInOrderAsync(observerReadStream, "the in-view peer at the inclusive 33x33 boundary");
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task RouteTypeThreeDespawnRelaysRemoveMobToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var catalog = LegacyNpcGenerationCatalog.Parse(
            """
            #[4291]
            MaxNumMob: 10
            MinGroup: 0
            MaxGroup: 0
            Leader: ROUTE3_MOB
            RouteType: 3
            StartX: 10
            StartY: 10
            StartWait: 0
            Segment1X: 10
            Segment1Y: 10
            Segment2X: 10
            Segment2Y: 10
            Segment3X: 10
            Segment3Y: 10
            DestX: 10
            DestY: 10
            """,
            new Dictionary<string, byte[]> { ["ROUTE3_MOB"] = CreateMob("ROUTE3_MOB", 1_000, clan: 2, skillBar: false) });
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        Check(hub.Enter(1, "ROUTE3_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "ROUTE3_BOUNDARY_VIEWER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "ROUTE3_OUTSIDE_VIEWER", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The RouteType 3 loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, positionX: 10, positionY: 11,
                  CreateMob("ROUTE3_TARGET", 1_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, positionX: 26, positionY: 10,
                  CreateMob("ROUTE3_BOUNDARY", 1_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0, positionX: 27, positionY: 10,
                  CreateMob("ROUTE3_OUTSIDE", 1_000, clan: 5, skillBar: false)) &&
              hub.ConfigurePistaLv6State(1, mobCount: 1),
            "The RouteType 3 loopback state could not be initialized.");
        Check(hub.TrySpawnGeneratedNpc(4291, out var npc, randomRoll: static _ => 0,
                  positionX: 10, positionY: 10, forceGenerationRoutePosition: true) && npc is not null,
            "The RouteType 3 loopback NPC could not be spawned.");
        var spawnedNpc = npc!;

        for (var expectedProgress = 1; expectedProgress <= 4; expectedProgress++)
        {
            Check(hub.TryCreateGeneratedNpcAiSnapshot(spawnedNpc.ConnectionId, 0, null, 0, 0, out var advanceSnapshot) &&
                  advanceSnapshot is not null,
                "The RouteType 3 loopback route transition could not be captured.");
            var advance = LegacyMobAiPlanner.Plan(advanceSnapshot!.PlannerInput);
            Check(advance.Kind == LegacyMobAiDecisionKind.Idle && advance.SegmentProgress == expectedProgress &&
                  hub.TryApplyGeneratedNpcAiDecision(advanceSnapshot, advance, out _) == LegacyNpcAiApplyStatus.Applied,
                $"The RouteType 3 loopback route did not reach segment {expectedProgress}.");
        }

        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        LegacyMobAiTickResult? terminalTick = null;
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = await runtime.TickNextShardAsync();
            if (tick.VisitedPeaceNpcIds.Contains(spawnedNpc.ConnectionId))
            {
                terminalTick = tick;
                break;
            }
        }
        Check(terminalTick is { PeaceShardIndex: >= 0 } &&
              hub.TryGetPistaLv6State(out var pistaState) && pistaState?.MobCount == 2 &&
              !hub.TryGetNpcSnapshot(spawnedNpc.ConnectionId, out _),
            "The RouteType 3 loopback terminal timer did not despawn the NPC and increment Pista's count.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var frame = await ReadFrameAsync(viewerStream, codec);
            Check(frame.Header.Type == RemoveMobConfirmation.MessageType &&
                  frame.Header.Id == spawnedNpc.ConnectionId && frame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 3,
                "An in-view loopback peer did not receive the captured RemoveMob(type=3) frame.");
        }
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task RouteTypeFiveInvalidLeaderDespawnRelaysRemoveMobToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var catalog = LegacyNpcGenerationCatalog.Parse(
            """
            #[42]
            MaxNumMob: 10
            Leader: ROUTE5_INVALID_LEADER
            RouteType: 5
            StartX: 10
            StartY: 10
            StartRange: 4
            StartWait: 0
            Segment1X: 20
            Segment1Y: 10
            DestX: 30
            DestY: 10
            """,
            new Dictionary<string, byte[]> { ["ROUTE5_INVALID_LEADER"] = CreateMob("R5BAD", 1_000, clan: 2, skillBar: false) });
        var hub = new WorldHub(npcGenerationCatalog: catalog);
        Check(hub.Enter(1, "ROUTE5_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "ROUTE5_BOUNDARY_VIEWER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "ROUTE5_OUTSIDE_VIEWER", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The RouteType 5 loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, positionX: 10, positionY: 11,
                  CreateMob("R5TARGET", 1_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, positionX: 26, positionY: 10,
                  CreateMob("R5BOUNDARY", 1_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 5, guildLevel: 0, coin: 0, positionX: 27, positionY: 10,
                  CreateMob("R5OUTSIDE", 1_000, clan: 5, skillBar: false)),
            "The RouteType 5 loopback state could not be initialized.");
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0,
                  positionX: 10, positionY: 10) && npc is not null && npc.LeaderId == 0,
            "The generated RouteType 5 loopback NPC did not preserve its invalid Leader value.");
        var spawnedNpc = npc!;
        Check(hub.SetNpcCombatState(spawnedNpc.ConnectionId, LegacyNpcMode.Combat, 0, [1]),
            "The RouteType 5 loopback NPC could not enter combat with its selected target.");

        var runtime = CreateRuntime(hub, codec, onRefresh: null,
            nextRoll: static _ => throw new InvalidOperationException("An invalid RouteType 5 Leader branch must not consume combat RNG."));
        var tick = await runtime.TickShardAsync(spawnedNpc.ConnectionId % LegacyMobAiRuntime.ShardCount);
        Check(tick.VisitedNpcIds.Contains(spawnedNpc.ConnectionId) && !hub.TryGetNpcSnapshot(spawnedNpc.ConnectionId, out _),
            "The Combat shard did not process and remove the generated RouteType 5 NPC with invalid Leader.");

        foreach (var viewerStream in new[] { targetReadStream, observerReadStream })
        {
            var frame = await ReadFrameAsync(viewerStream, codec);
            Check(frame.Header.Type == RemoveMobConfirmation.MessageType &&
                  frame.Header.Id == spawnedNpc.ConnectionId && frame.IsChecksumValid &&
                  BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 3,
                "An in-view loopback peer did not receive the captured RouteType 5 RemoveMob(type=3) frame.");
        }
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task PassiveLeaderAggroRemovesDeadGeneratedFollowersAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var leaderTemplate = CreateMob("WOLF_LEADER", 1_000, clan: 2, skillBar: false);
        var followerTemplate = CreateMob("WOLF_FOLLOWER@", 1_000, clan: 2, skillBar: false);
        var catalog = LegacyNpcGenerationCatalog.Parse(
            """
            #[742]
            MaxNumMob: 10
            MinGroup: 1
            MaxGroup: 1
            Leader: WOLF_LEADER
            Follower: WOLF_FOLLOWER@
            RouteType: 0
            StartX: 10
            StartY: 10
            StartRange: 0
            StartWait: 0
            Segment1X: 20
            Segment1Y: 10
            DestX: 30
            DestY: 10
            """,
            new Dictionary<string, byte[]>
            {
                ["WOLF_LEADER"] = leaderTemplate,
                ["WOLF_FOLLOWER@"] = followerTemplate,
            });
        var hub = new WorldHub(npcGenerationCatalog: catalog, skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "PASSIVE_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "PASSIVE_NEAR_VIEW", (bytes, token) => observerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(3, "PASSIVE_FAR_VIEW", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The passive group cleanup loopback peers could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 11, positionY: 10, CreateMob("TARGET", 100_000, clan: 3, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 13, positionY: 10, CreateMob("NEARVIEW", 1_000, clan: 2, skillBar: false)) &&
              hub.SetCharacterState(3, 0, 0, clan: 2, guildLevel: 0, coin: 0,
                  positionX: 101, positionY: 100, CreateMob("FARVIEW", 1_000, clan: 2, skillBar: false)),
            "The passive group cleanup loopback player positions could not be initialized.");
        Check(hub.TrySpawnGeneratedNpcGroup(742, out var group, static _ => 0,
                  positionX: 10, positionY: 10) && group is { Count: 2 },
            "The passive group cleanup loopback generated group could not be spawned.");

        var leader = group![0];
        var nearDeadId = hub.EnterNpc(CreateMob("DEAD_NEAR", 0, clan: 2, skillBar: false), 12, 10,
            requestedConnectionId: 20_061, generateIndex: 742, leaderConnectionId: leader.ConnectionId);
        var farDeadId = hub.EnterNpc(CreateMob("DEAD_FAR", 0, clan: 2, skillBar: false), 100, 100,
            requestedConnectionId: 20_062, generateIndex: 742, leaderConnectionId: leader.ConnectionId);
        Check(nearDeadId == 20_061 && farDeadId == 20_062,
            "The passive group cleanup loopback dead followers could not be registered.");

        Check(hub.TryCreateGeneratedNpcAiPeaceSnapshot(leader.ConnectionId, static () => 0, out var snapshot) &&
              snapshot is not null,
            "The passive group cleanup loopback leader snapshot could not be captured.");
        var decision = LegacyMobAiPlanner.Plan(snapshot!.PlannerInput);
        Check(decision.Kind == LegacyMobAiDecisionKind.Aggro && decision.AggroTargetId == 1,
            "The passive group cleanup loopback leader did not select the hostile target.");
        Check(hub.TryApplyGeneratedNpcAiDecision(snapshot, decision, out var outcome) == LegacyNpcAiApplyStatus.Applied &&
              outcome.AdditionalRemovals.Count == 2 &&
              !hub.TryGetNpcSnapshot(nearDeadId, out _) && !hub.TryGetNpcSnapshot(farDeadId, out _),
            "The passive leader application did not remove both dead followers before relay.");
        await LegacyMobAiRelay.RelayAsync(
            hub, outcome, codec, sendResurrectionRefresh: null, cancellationToken: CancellationToken.None,
            clientTick: 1, keywordIndex: 0);
        await AssertTypeOneRemovalAsync(targetReadStream, nearDeadId, codec, "hostile target");
        await AssertTypeOneRemovalAsync(observerReadStream, nearDeadId, codec, "near-view observer");
        await AssertTypeOneRemovalAsync(outsideReadStream, farDeadId, codec, "far-view observer");
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task AssertTypeOneRemovalAsync(
        NetworkStream stream,
        int npcConnectionId,
        LegacyFrameCodec codec,
        string viewerDescription)
    {
        var frame = await ReadFrameAsync(stream, codec);
        Check(frame.Header.Type == RemoveMobConfirmation.MessageType && frame.Header.Id == npcConnectionId &&
              frame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(frame.Payload.Span) == 1,
            $"The {viewerDescription} did not receive the captured RemoveMob(type=1) follower frame.");
    }

    private static async Task PassiveAcquisitionAttacksThroughCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 11,
            targetY: 10,
            observerX: 27,
            observerY: 27,
            npcX: 10,
            npcY: 10,
            targetClan: 3,
            observerClan: 5,
            seedCombat: false,
            npcHasRangeWeapon: true);
        var runtime = CreateRuntime(hub, codec, onRefresh: null, itemData: CreateItemDataWithWeaponRange(1));
        LegacyMobAiTickResult? aggroTick = null;
        var peaceTickDetails = new List<string>();
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = await runtime.TickNextShardAsync();
            peaceTickDetails.Add($"peaceShard={tick.PeaceShardIndex}, peaceVisited=[{string.Join(',', tick.VisitedPeaceNpcIds)}], combatVisited=[{string.Join(',', tick.VisitedNpcIds)}]");
            if (!tick.VisitedPeaceNpcIds.Contains(npcId))
                continue;

            if (hub.TryGetNpcCombatState(npcId, out var mode, out var targetId, out var enemies) &&
                mode == LegacyNpcMode.Combat && targetId == 1 && enemies.Contains(1))
            {
                aggroTick = tick;
                break;
            }
        }

        if (aggroTick is null)
        {
            var hasDebugSnapshot = hub.TryCreateGeneratedNpcAiPeaceSnapshot(npcId, static () => 0, out var debugSnapshot);
            var debugInput = debugSnapshot?.PlannerInput;
            var debugDecision = debugInput is null ? "none" : LegacyMobAiPlanner.Plan(debugInput).ToString();
            var targets = debugInput is null ? "none" : string.Join(";", debugInput.Targets.Select(static candidate =>
                $"{candidate.Id}@{candidate.X},{candidate.Y}:npc={candidate.IsNpc}:active={candidate.IsActive}:hp={candidate.Hp}:valid={candidate.IsValid}:visible={candidate.IsVisible}:hostile={candidate.IsHostile}:hidden={candidate.Hidden}"));
            var stateExists = hub.TryGetNpcCombatState(npcId, out var passiveMode, out var passiveTarget, out var passiveEnemies);
            throw new InvalidOperationException(
                $"The Peace shard did not passively acquire the visible hostile target before Combat processing. " +
                $"npcId={npcId}; ticks=[{string.Join(" | ", peaceTickDetails)}]; npcState={stateExists}:{passiveMode}/{passiveTarget}/[{string.Join(',', passiveEnemies)}]; " +
                $"snapshot={hasDebugSnapshot}; plannerMob={debugInput?.Mob}; decision={debugDecision}; targets=[{targets}].");
        }
        var attackTick = aggroTick!;
        if (attackTick.Attacks.All(attack => attack.AttackerConnectionId != npcId))
            attackTick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(attackTick.Attacks is [{ Damage: > 0, Missed: false } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { } recipients && recipients.SequenceEqual([1]),
            $"Passive aggro did not lead to a positive attack relayed only to the captured target view. Attack count={attackTick.Attacks.Count}; outcomes={string.Join(";", attackTick.Attacks)}; combat state={hub.TryGetNpcCombatState(npcId, out var debugMode, out var debugTarget, out var debugEnemies)}:{debugMode}/{debugTarget}/[{string.Join(",", debugEnemies)}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10, targetX: 11, targetY: 10);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The passively acquired target did not receive its authoritative HP/MP refresh after the attack.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task CombatRouteTypeFiveTeleportRelaysAcrossViewsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        Check(hub.Enter(1, "ROUTE5_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "ROUTE5_VIEWER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)),
            "The RouteType 5 loopback target/viewer connections could not be registered.");
        AddRouteFiveOwner(hub, 3, "ROUTE5_LEADER", 90, 100, 0);
        AddRouteFiveOwner(hub, 4, "ROUTE5_CASTER", 100, 100, 30);
        Check(hub.TrySetParty(3, [3, 4]), "The RouteType 5 loopback party could not be formed.");

        var skill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 1, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        Check(hub.TryApplySkillAttack(4, 4, skill, itemData: null, weather: 0, randomFactor: 90, out var summonOutcome) ==
              LegacySkillAttackResult.Accepted && summonOutcome?.SummonedMobs is { Count: 1 },
            "The RouteType 5 loopback summon could not be created.");
        var summon = summonOutcome!.SummonedMobs!.Single();
        var summonerX = checked((short)(summon.PositionX + 19));
        Check(hub.UpdatePosition(4, summonerX, summon.PositionY),
            "The RouteType 5 loopback summoner could not be placed at combat distance 20.");

        var targetMob = CreateMob("R5_TARGET", 100_000, clan: 5, skillBar: false);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(summon.PositionX + 1)), positionY: summon.PositionY, targetMob) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0,
                  positionX: checked((short)(summonerX + 15)), positionY: summon.PositionY,
                  CreateMob("R5_VIEW", 1_000, clan: 5, skillBar: false)),
            "The RouteType 5 loopback target and destination viewer could not be positioned.");

        Check(hub.TryApplyAllSummonBattleOpportunities(randomFactor: 100, out var opportunities) &&
              opportunities is [{ Movement: { Status: LegacyNpcAiApplyStatus.Applied, Movement: { Effect: 1, Speed: 2 } }, Attack: null }],
            "The loopback Combat opportunity did not choose a teleport instead of an attack at distance 20.");
        var movementOutcome = opportunities.Single().Movement!;
        var movement = movementOutcome.Movement!;
        Check(hub.TryGetCharacterSnapshot(1, out var unchangedTarget, out _, out _) && unchangedTarget is not null &&
              LegacyMobCombatState.Read(unchangedTarget).CurrentScore.Hp == 100_000,
            "The RouteType 5 loopback teleport also applied attack damage during the same opportunity.");

        await LegacyMobAiRelay.RelayAsync(
            hub,
            movementOutcome,
            codec,
            sendResurrectionRefresh: null,
            CancellationToken.None,
            clientTick: 1,
            keywordIndex: 0);

        var leavingAction = await ReadFrameAsync(targetReadStream, codec);
        var leavingRemoval = await ReadFrameAsync(targetReadStream, codec);
        Check(ActionRequest.TryParse(leavingAction, out var oldViewAction) &&
              oldViewAction is { Effect: 1, Speed: 2 } &&
              oldViewAction.PositionX == movement.FromX && oldViewAction.PositionY == movement.FromY &&
              oldViewAction.TargetX == movement.ToX && oldViewAction.TargetY == movement.ToY &&
              leavingRemoval.Header.Type == RemoveMobConfirmation.MessageType && leavingRemoval.Header.Id == summon.ConnectionId,
            "The loopback origin viewer did not receive teleport Action followed by RemoveMob.");

        var enteringCreate = await ReadFrameAsync(observerReadStream, codec);
        var enteringAction = await ReadFrameAsync(observerReadStream, codec);
        Check(enteringCreate.Header.Type == CreateMobConfirmationV769.MessageType && enteringCreate.IsChecksumValid &&
              BinaryPrimitives.ReadUInt16LittleEndian(enteringCreate.Payload.Span[CreateMobConfirmationV769.MobIdOffset..]) == summon.ConnectionId &&
              BinaryPrimitives.ReadInt16LittleEndian(enteringCreate.Payload.Span[CreateMobConfirmationV769.PositionXOffset..]) == movement.ToX &&
              BinaryPrimitives.ReadInt16LittleEndian(enteringCreate.Payload.Span[CreateMobConfirmationV769.PositionYOffset..]) == movement.ToY &&
              ActionRequest.TryParse(enteringAction, out var newViewAction) &&
              newViewAction is { Effect: 1, Speed: 2 } && newViewAction.TargetX == movement.ToX && newViewAction.TargetY == movement.ToY,
            "The loopback destination viewer did not receive CreateMob followed by the teleport Action.");
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task UnlistedMixedSkillBarAttackUsesGenericGetAttackAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcSkillBar: [17, byte.MaxValue, 40, byte.MaxValue],
            targetFireResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},view=[{string.Join(",", attack.ViewRecipientConnectionIds ?? Array.Empty<int>())}]"));
        var hasCombat = hub.TryGetNpcCombatState(npcId, out var mode, out var targetId, out var enemies);
        var hasTarget = hub.TryGetCharacterSnapshot(1, out var targetMob, out _, out _);
        var targetHp = hasTarget && targetMob is not null ? LegacyMobCombatState.Read(targetMob).CurrentScore.Hp : -1;
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 17, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The unlisted mixed SkillBar did not use generic GetAttack data and capture only the target viewer. attacks=[{attackSummary}], combat={hasCombat}:{mode}/{targetId}/[{string.Join(",", enemies)}], targetHp={targetHp}.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 17, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Carbunkle Fire-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task CarbunkleFireRepeatedFourthSlotAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasCarbunkleFireRepeatedBar: true,
            targetFireResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 32, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            "The repeated Carbunkle Fire fourth-slot attack did not apply Resist[0] and capture only the target viewer.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 32, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The repeated Carbunkle Fire fourth-slot target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task InfernoAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasInfernoBar: true,
            targetFireResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 39, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            "The Inferno-bar attack did not apply Resist[0] and capture only the target viewer.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 39, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Inferno-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task PhoenixFireAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasPhoenixFireBar: true,
            targetFireResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},view=[{string.Join(",", attack.ViewRecipientConnectionIds ?? Array.Empty<int>())}]"));
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 38, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Phoenix of Fire-bar attack did not apply Resist[0] and capture only the target viewer. attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 38, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Phoenix of Fire-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task FeraFlamejanteAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasFeraFlamejanteBar: true,
            targetFireResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 48, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            "The Fera Flamejante-bar attack did not apply Resist[0] and capture only the target viewer.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 48, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Fera Flamejante-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task ExterminarAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasExterminarBar: true,
            targetFireResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 22, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            "The Exterminar-bar attack did not apply Resist[0] and capture only the target viewer.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 22, motion: 4, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Exterminar-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task ExterminarMixedBarUsesThirdSlotSelectionAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasMixedExterminarBar: true,
            targetFireResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 85, // GetAttack SkillBar selector
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 22, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The mixed Exterminar bar did not select SkillBar[2], apply Resist[0], and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 22, motion: 4, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The mixed Exterminar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task FlashMixedBarUsesThirdSlotSelectionAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasFlashBar: true,
            targetFireResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 85, // GetAttack SkillBar selector
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 26, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The mixed Flash bar did not select SkillBar[2], use Motion=4, skip Resist=5, and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 26, motion: 4, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The mixed Flash target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task EnfraquecerMixedBarUsesThirdSlotSelectionAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasMixedEnfraquecerBar: true,
            targetFireResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 85, // GetAttack SkillBar selector
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 51, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The mixed Enfraquecer bar did not select SkillBar[2], skip out-of-range Resist[-2], and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 51, motion: 4, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The mixed Enfraquecer target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task ToqueSagradoMixedBarUsesThirdSlotSelectionAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasToqueSagradoBar: true,
            targetFireResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 85, // GetAttack SkillBar selector
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 1, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The mixed Toque Sagrado bar did not select SkillBar[2], preserve physical damage, and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 1, motion: 4, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The mixed Toque Sagrado target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task GolpeDuploMixedBarUsesThirdSlotSelectionAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasGolpeDuploBar: true,
            targetFireResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 85, // GetAttack SkillBar selector
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 2, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The mixed Golpe Duplo bar did not select SkillBar[2], preserve physical damage, and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 2, motion: 4, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The mixed Golpe Duplo target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task EspadaFenixMixedBarUsesThirdSlotSelectionAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasEspadaFenixBar: true,
            targetFireResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 85, // GetAttack SkillBar selector
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 12, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The mixed Espada da Fênix bar did not select SkillBar[2], preserve physical damage, and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 12, motion: 4, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The mixed Espada da Fênix target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task ArqueiroElfoMixedBarUsesThirdSlotSelectionAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasArqueiroElfoBar: true,
            targetFireResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 85, // GetAttack SkillBar selector
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 79, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The mixed Arqueiro_Elfo bar did not select SkillBar[2], preserve physical damage, and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 79, motion: 4, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The mixed Arqueiro_Elfo target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task TrollDualSkillBarUsesMiddleSelectorAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasTrollDualSkillBar: true,
            targetFireResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 50, // GetAttack SkillBar selector: SkillBar[1]
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 35, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Troll dual-skill bar did not select SkillBar[1], apply Resist[0], and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 35, motion: 4, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Troll dual-skill target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task KalitzMagoMixedBarUsesThirdSlotSelectionAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasKalitzMagoBar: true,
            targetLightningResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 85, // GetAttack SkillBar selector: SkillBar[2]
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 37, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The mixed Kalitz_Mago bar did not select row 37 without an elemental modifier and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 37, motion: 4, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The mixed Kalitz_Mago target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task MagoInfMixedBarUsesSelectorAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasMagoInfMixedBar: true,
            targetIceResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 50, // GetAttack SkillBar selector: SkillBar[1]
            5 => 99, // miss roll: make the expected hit deterministic
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        var hasNpc = hub.TryGetNpcSnapshot(npcId, out var npcSnapshot);
        var actualSkillBar = hasNpc && npcSnapshot is not null
            ? string.Join(',', npcSnapshot.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4).ToArray())
            : "missing";
        var hasCombat = hub.TryGetNpcCombatState(npcId, out var mode, out var currentTarget, out var enemies);
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 34, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Mago_Inf bar did not select SkillBar[1], apply row 34 Resist[1], and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}], npc={hasNpc}:{actualSkillBar}, combat={hasCombat}:{mode}/{currentTarget}/[{string.Join(',', enemies)}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 34, motion: 4, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Mago_Inf target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task ClanOneMixedBarsShareSelectorAndRelayFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        await RunClanOneMixedBarCaseAsync(
            targetSendStream, observerSendStream, targetReadStream, observerReadStream, codec,
            "Mago_Ciclope", npcHasMagoCiclopeBar: true, npcHasBruxaNegraBar: false,
            expectedSkill: 36, targetFireResist: 0, targetIceResist: 50);
        await RunClanOneMixedBarCaseAsync(
            targetSendStream, observerSendStream, targetReadStream, observerReadStream, codec,
            "Bruxa_Negra", npcHasMagoCiclopeBar: false, npcHasBruxaNegraBar: true,
            expectedSkill: 39, targetFireResist: 50, targetIceResist: 0);
    }

    private static async Task RunClanOneMixedBarCaseAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec,
        string name,
        bool npcHasMagoCiclopeBar,
        bool npcHasBruxaNegraBar,
        short expectedSkill,
        byte targetFireResist,
        byte targetIceResist)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasMagoCiclopeBar: npcHasMagoCiclopeBar,
            npcHasBruxaNegraBar: npcHasBruxaNegraBar,
            targetFireResist: targetFireResist,
            targetIceResist: targetIceResist);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 50, // SkillBar[1], inside the fourth-slot roll window
            5 => 99, // miss roll: make the expected hit deterministic
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var hasNpc = hub.TryGetNpcSnapshot(npcId, out var npcSnapshot);
        var mobHp = hasNpc && npcSnapshot is not null
            ? LegacyMobCombatState.Read(npcSnapshot.MobSnapshot).CurrentScore.Hp
            : -1;
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: var skillIndex, SkillParm: 1, Motion: 4 } attack] &&
              skillIndex == expectedSkill &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]) &&
              mobHp == 10_000,
            $"{name} must use the 0..49/50..84/85..99 three-slot selector, apply the selected skill's resistance, and leave the full-HP generated NPC unchanged because slot 3 is not an InstanceType 6 effect. calls={randomCall}, mobHp={mobHp}.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: expectedSkill, motion: 4, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            $"The {name} target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task MagoAmaldMixedBarUsesFlashSelectorAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasMagoAmaldMixedBar: true,
            targetIceResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 85, // GetAttack SkillBar selector: SkillBar[2] Flash
            5 => 99, // miss roll: make the expected hit deterministic
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 26, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Mago_Amald bar did not select Flash with Resist=5 outside the elemental range and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 26, motion: 4, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Mago_Amald target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task RangerInfMixedBarUsesSelectorAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasRangerInfMixedBar: true,
            targetIceResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 85, // GetAttack SkillBar selector: SkillBar[2] row 73
            5 => 99, // miss roll: make the expected hit deterministic
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 73, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Ranger_Inf bar did not select row 73 with physical damage unchanged and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 73, motion: 4, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Ranger_Inf target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task ShamanInfMixedBarUsesSelectorAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasShamanInfMixedBar: true,
            targetGaiaResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 50, // GetAttack SkillBar selector: SkillBar[1] row 55
            5 => 99, // miss roll: make the expected hit deterministic
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 55, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Shaman_Inf bar did not select row 55, apply Resist[2], and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 55, motion: 4, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Shaman_Inf target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task OrcMedicoFourthSlotSelfDamageRelaysScoreAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub();
        Check(hub.Enter(1, "ORC_MEDICO_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "ORC_MEDICO_OBSERVER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)),
            "The Orc_Medico loopback participants could not be registered.");
        var targetMob = CreateMob("TARGET", hp: 100_000, clan: 5, skillBar: false);
        var observerMob = CreateMob("OBSERVER", hp: 1_000, clan: 5, skillBar: false);
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 10, 10, targetMob) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, 40, 40, observerMob),
            "The Orc_Medico loopback characters could not be initialized.");

        var medicoMob = CreateMob("ORC_MEDICO", hp: 400, clan: 2, skillBar: false);
        new byte[] { 40, 40, 51, 29 }.CopyTo(medicoMob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        var npcId = hub.EnterNpc(medicoMob, 10, 10, requestedConnectionId: 20_042, generateIndex: 42);
        Check(npcId == 20_042 && hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, [1]),
            "The active low-HP Orc_Medico loopback mob could not be initialized.");

        var rollIndex = 0;
        int NextRoll(int _) => ++rollIndex switch
        {
            1 => 0,
            2 => 0,
            3 => 25,
            4 => 0,
            5 => 99,
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ TargetConnectionId: 20_042, Damage: 100, RemainingHp: 300,
                  TargetIsNpc: true, SkillIndex: 6, Motion: 4, Missed: false } attack] &&
              attack.AttackerConnectionId == npcId && attack.ViewRecipientConnectionIds is { Count: 1 } recipients &&
              recipients.SequenceEqual([1]) && hub.TryGetNpcSnapshot(npcId, out var updatedNpc) && updatedNpc is not null &&
              LegacyMobCombatState.Read(updatedNpc.MobSnapshot).CurrentScore.Hp == 300,
            "The row 29 loopback attack did not apply its self-targeted MaxHp/10 damage to the NPC.");

        AssertAttackOne(await ReadFrameAsync(targetReadStream, codec), npcId, targetId: npcId,
            positionX: 10, positionY: 10, skillIndex: 6, motion: 4, damage: 100);
        var scoreFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType && scoreFrame.Header.Id == npcId &&
              scoreFrame.IsChecksumValid,
            "The living Orc_Medico target did not receive a 7.69 UpdateScore refresh in its captured view.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task OrcMedicoFourthSlotDeathRelaysRemoveAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub();
        Check(hub.Enter(1, "ORC_MEDICO_DEATH_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "ORC_MEDICO_DEATH_OBSERVER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)),
            "The Orc_Medico death-loopback participants could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 10, 10,
                  CreateMob("TARGET", hp: 100_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, 40, 40,
                  CreateMob("OBSERVER", hp: 1_000, clan: 5, skillBar: false)),
            "The Orc_Medico death-loopback characters could not be initialized.");
        var medicoMob = CreateMob("ORC_MEDICO", hp: 50, clan: 2, skillBar: false);
        new byte[] { 40, 40, 51, 29 }.CopyTo(medicoMob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        var npcId = hub.EnterNpc(medicoMob, 10, 10, requestedConnectionId: 20_042, generateIndex: 42);
        Check(npcId == 20_042 && hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, [1]),
            "The lethal low-HP Orc_Medico loopback mob could not be initialized.");

        var rollIndex = 0;
        int NextRoll(int _) => ++rollIndex switch
        {
            1 => 0,
            2 => 0,
            3 => 25,
            4 => 0,
            5 => 99,
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ TargetDied: true, TargetConnectionId: 20_042, Damage: 100,
                  TargetIsNpc: true, SkillIndex: 6, Motion: 4, Missed: false } attack] &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]) &&
              !hub.TryGetNpcSnapshot(npcId, out _),
            "The lethal row 29 branch did not remove the selected generated NPC and capture only its viewer.");

        AssertAttackOne(await ReadFrameAsync(targetReadStream, codec), npcId, targetId: npcId,
            positionX: 10, positionY: 10, skillIndex: 6, motion: 4, damage: 100);
        var removeFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(removeFrame.Header.Type == RemoveMobConfirmation.MessageType && removeFrame.Header.Id == npcId &&
              removeFrame.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(removeFrame.Payload.Span) == 1,
            "The lethal row 29 target did not receive the 7.69 type-1 RemoveMob frame.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task FeiticeiraFourthSlotSelfDamageRelaysMotionAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub();
        Check(hub.Enter(1, "FEIT_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "FEIT_OBSERVER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)),
            "The Feiticeira loopback participants could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 12, 10,
                  CreateMob("TARGET", hp: 100_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, 40, 40,
                  CreateMob("OBSERVER", hp: 1_000, clan: 5, skillBar: false)),
            "The Feiticeira loopback characters could not be initialized.");

        var feiticeira = CreateMob("FEITICEIRA", hp: 900, clan: 7, skillBar: false);
        new byte[] { 33, 36, 38, 27 }.CopyTo(feiticeira.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        new LegacyItem(1, 0, 0, 0, 0, 0, 0).Write(
            feiticeira.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        var npcId = hub.EnterNpc(feiticeira, 10, 10, requestedConnectionId: 20_045, generateIndex: 45);
        Check(npcId == 20_045 && hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, [1]),
            "The active low-HP Feiticeira loopback mob could not be initialized.");

        var randomCall = 0;
        int NextRoll(int maximum) => ++randomCall switch
        {
            1 => 0, // attack chance
            2 => 0, // dexterity
            3 when maximum == 4 => 2, // special2=27 Motion roll
            3 => 25, // SkillBar selector when the motion draw is absent
            4 when maximum == 100 => 25, // SkillBar selector after Motion
            4 => 0, // damage factor when no motion draw is consumed
            5 when maximum == 12 => 0, // damage factor
            5 => 99, // miss roll when no motion draw is consumed
            6 => 99, // miss roll after Motion and selector
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null,
            itemData: CreateItemDataWithWeaponRange(2), nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ TargetConnectionId: 20_045, Damage: 100, RemainingHp: 800,
                  TargetIsNpc: true, SkillIndex: 6, Motion: 7, Missed: false } attack] &&
              randomCall == 6 && attack.AttackerConnectionId == npcId &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]) &&
              hub.TryGetNpcSnapshot(npcId, out var updatedNpc) && updatedNpc is not null &&
              LegacyMobCombatState.Read(updatedNpc.MobSnapshot).CurrentScore.Hp == 800,
            $"The Feiticeira row-27 loopback did not retain its ranged Motion roll and self-damage. calls={randomCall}, attacks={tick.Attacks.Count}.");

        AssertAttackOne(await ReadFrameAsync(targetReadStream, codec), npcId, targetId: npcId,
            positionX: 10, positionY: 10, skillIndex: 6, motion: 7, damage: 100);
        var scoreFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType && scoreFrame.Header.Id == npcId &&
              scoreFrame.IsChecksumValid,
            "The living Feiticeira did not receive its 7.69 UpdateScore refresh in the captured view.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task BruxaFourthSlotSelfDamageRelaysMotionAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub();
        Check(hub.Enter(1, "BRUXA_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "BRUXA_OBSERVER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)),
            "The Bruxa loopback participants could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 12, 10,
                  CreateMob("TARGET", hp: 100_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, 40, 40,
                  CreateMob("OBSERVER", hp: 1_000, clan: 5, skillBar: false)),
            "The Bruxa loopback characters could not be initialized.");

        var bruxa = CreateMob("BRUXA", hp: 900, clan: 7, skillBar: false);
        new byte[] { 24, 27, 28, 27 }.CopyTo(bruxa.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        new LegacyItem(1, 0, 0, 0, 0, 0, 0).Write(
            bruxa.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        var npcId = hub.EnterNpc(bruxa, 10, 10, requestedConnectionId: 20_049, generateIndex: 49);
        Check(npcId == 20_049 && hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, [1]),
            "The active low-HP Bruxa loopback mob could not be initialized.");

        var randomCall = 0;
        int NextRoll(int maximum) => ++randomCall switch
        {
            1 => 0, // attack chance
            2 => 0, // dexterity
            3 when maximum == 4 => 2, // special2=27 Motion roll
            3 => 25, // SkillBar selector if no Motion roll was consumed
            4 when maximum == 100 => 25, // SkillBar selector after Motion
            4 => 0, // damage factor if no Motion roll was consumed
            5 when maximum == 12 => 0, // damage factor
            5 => 99, // miss roll if no Motion roll was consumed
            6 => 99, // miss roll after Motion and selector
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null,
            itemData: CreateItemDataWithWeaponRange(2), nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ TargetConnectionId: 20_049, Damage: 100, RemainingHp: 800,
                  TargetIsNpc: true, SkillIndex: 6, Motion: 7, Missed: false } attack] &&
              randomCall == 6 && attack.AttackerConnectionId == npcId &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]) &&
              hub.TryGetNpcSnapshot(npcId, out var updatedNpc) && updatedNpc is not null &&
              LegacyMobCombatState.Read(updatedNpc.MobSnapshot).CurrentScore.Hp == 800,
            $"The Bruxa row-27 loopback did not retain its ranged Motion roll and self-damage. calls={randomCall}, attacks={tick.Attacks.Count}.");

        AssertAttackOne(await ReadFrameAsync(targetReadStream, codec), npcId, targetId: npcId,
            positionX: 10, positionY: 10, skillIndex: 6, motion: 7, damage: 100);
        var scoreFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType && scoreFrame.Header.Id == npcId &&
              scoreFrame.IsChecksumValid,
            "The living Bruxa did not receive its 7.69 UpdateScore refresh in the captured view.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task TorreRunicaFourthSlotSelfDamageRelaysPhysicalMotionAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub();
        Check(hub.Enter(1, "TORRE_TARGET", (bytes, token) => targetSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "TORRE_OBSERVER", (bytes, token) => observerSendStream.WriteAsync(bytes, token)),
            "The Torre_Runica loopback participants could not be registered.");
        Check(hub.SetCharacterState(1, 0, 0, clan: 5, guildLevel: 0, coin: 0, 12, 10,
                  CreateMob("TARGET", hp: 100_000, clan: 5, skillBar: false)) &&
              hub.SetCharacterState(2, 0, 0, clan: 5, guildLevel: 0, coin: 0, 40, 40,
                  CreateMob("OBSERVER", hp: 1_000, clan: 5, skillBar: false)),
            "The Torre_Runica loopback characters could not be initialized.");

        var torreRunica = CreateMob("TORRE_RUNICA", hp: 900, clan: 1, skillBar: false);
        new byte[] { 255, 255, 255, 27 }.CopyTo(torreRunica.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        new LegacyItem(1, 0, 0, 0, 0, 0, 0).Write(
            torreRunica.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        var npcId = hub.EnterNpc(torreRunica, 10, 10, requestedConnectionId: 20_051, generateIndex: 51);
        Check(npcId == 20_051 && hub.SetNpcCombatState(npcId, LegacyNpcMode.Combat, 0, [1]),
            "The active low-HP Torre_Runica loopback mob could not be initialized.");

        var randomCall = 0;
        int NextRoll(int maximum) => ++randomCall switch
        {
            1 => 0, // attack chance
            2 => 0, // dexterity
            3 when maximum == 4 => 2, // ranged special2=27 consumes rand()%4 and maps to Motion 7
            3 => 25, // SkillBar selector if no special2 draw was consumed
            4 when maximum == 100 => 25, // slot-3 selector after special2
            4 => 0, // damage factor if no special2 draw was consumed
            5 when maximum == 12 => 0, // damage factor
            5 => 99, // miss roll if no special2 draw was consumed
            6 => 99, // miss roll after special2 and selector
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null,
            itemData: CreateItemDataWithWeaponRange(2), nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ TargetConnectionId: 20_051, Damage: 100, RemainingHp: 800,
                  TargetIsNpc: true, SkillIndex: 6, Motion: 7, Missed: false } attack] &&
              randomCall == 6 && attack.AttackerConnectionId == npcId &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]) &&
              hub.TryGetNpcSnapshot(npcId, out var updatedNpc) && updatedNpc is not null &&
              LegacyMobCombatState.Read(updatedNpc.MobSnapshot).CurrentScore.Hp == 800,
            $"The Torre_Runica row-27 loopback did not consume special2 and apply self-damage. calls={randomCall}, attacks={tick.Attacks.Count}.");

        AssertAttackOne(await ReadFrameAsync(targetReadStream, codec), npcId, targetId: npcId,
            positionX: 10, positionY: 10, skillIndex: 6, motion: 7, damage: 100);
        var scoreFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(scoreFrame.Header.Type == UpdateScoreConfirmationV769.MessageType && scoreFrame.Header.Id == npcId &&
              scoreFrame.IsChecksumValid,
            "The living Torre_Runica did not receive its 7.69 UpdateScore refresh in the captured view.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task DarkShadowPrimaryOnlyBarUsesFlashAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasDarkShadowPrimaryOnlyBar: true,
            targetIceResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 0,  // GetAttack selector: primary Flash / Resist 5
            5 => 99, // miss roll: make the expected hit deterministic
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 26, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Dark_Shadow_ single-slot bar did not preserve its primary Flash attack and physical damage with Resist=5. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 26, motion: 4, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Dark_Shadow_ target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task ShamaAmaldMixedBarUsesSelectorAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            targetClan: 5,
            npcHasShamaAmaldMixedBar: true,
            targetLightningResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 50, // GetAttack SkillBar selector: SkillBar[1]
            5 => 99, // miss roll: make the expected hit deterministic
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        var hasNpc = hub.TryGetNpcSnapshot(npcId, out var npcSnapshot);
        var actualSkillBar = hasNpc && npcSnapshot is not null
            ? string.Join(',', npcSnapshot.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4).ToArray())
            : "missing";
        var hasCombat = hub.TryGetNpcCombatState(npcId, out var mode, out var currentTarget, out var enemies);
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 33, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Shama_Amald bar did not select row 33, apply Resist[3], and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}], npc={hasNpc}:{actualSkillBar}, combat={hasCombat}:{mode}/{currentTarget}/[{string.Join(',', enemies)}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 33, motion: 4, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Shama_Amald target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task BruxaElficaMixedBarUsesMiddleSelectorAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            npcHasBruxaElficaMixedBar: true,
            targetIceResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 50, // GetAttack SkillBar selector: SkillBar[1]
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 34, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The mixed Bruxa_Elfica bar did not select SkillBar[1], apply Resist[1], and capture only the target viewer. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 34, motion: 4, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The mixed Bruxa_Elfica target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task GaiaFuryAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasGaiaFuryBar: true,
            targetGaiaResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},view=[{string.Join(",", attack.ViewRecipientConnectionIds ?? Array.Empty<int>())}]"));
        var hasCombat = hub.TryGetNpcCombatState(npcId, out var mode, out var targetId, out var enemies);
        var hasTarget = hub.TryGetCharacterSnapshot(1, out var targetMob, out _, out _);
        var targetHp = hasTarget && targetMob is not null ? LegacyMobCombatState.Read(targetMob).CurrentScore.Hp : -1;
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 52, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Gaia Fury-bar attack did not apply Resist[2] and capture only the target viewer. attacks=[{attackSummary}], combat={hasCombat}:{mode}/{targetId}/[{string.Join(",", enemies)}], targetHp={targetHp}.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 52, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Gaia Fury-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task LichDemoMixedBarUsesGaiaSelectorAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 10,
            targetY: 10,
            npcX: 10,
            npcY: 10,
            npcHasLichDemoMixedBar: true,
            targetGaiaResist: 50);
        var randomCall = 0;
        int NextRoll(int max) => ++randomCall switch
        {
            1 => 0,  // attack chance
            2 => 0,  // dexterity
            3 => 49, // GetAttack SkillBar selector: SkillBar[0/1]
            _ => 0,
        };
        var runtime = CreateRuntime(hub, codec, onRefresh: null, nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},target={attack.TargetConnectionId}"));
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 52, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The adjacent Lich_Demo bar did not preserve Motion 4, Resist[2], and target-only view. calls={randomCall}, attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 52, motion: 4, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Lich_Demo target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task NevascaAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasNevascaBar: true,
            targetIceResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 36, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            "The Nevasca-bar attack did not apply Resist[1] and capture only the target viewer.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 36, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Nevasca-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task EspadaFlamejanteAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasEspadaFlamejanteBar: true,
            targetFireResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 17, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            "The Espada Flamejante-bar attack did not apply Resist[0] and capture only the target viewer.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 17, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Espada Flamejante-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task PoisonMistAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasPoisonMistBar: true,
            targetPoisonMistResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},view=[{string.Join(",", attack.ViewRecipientConnectionIds ?? Array.Empty<int>())}]"));
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 40, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Poison Mist-bar attack did not apply Resist[2] and capture only the target viewer. attacks=[{attackSummary}].");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 40, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Poison Mist-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task ShadowBladeAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasShadowBladeBar: true,
            targetShadowBladeResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},view=[{string.Join(",", attack.ViewRecipientConnectionIds ?? Array.Empty<int>())}]"));
        var hasCombat = hub.TryGetNpcCombatState(npcId, out var mode, out var targetId, out var enemies);
        var hasTarget = hub.TryGetCharacterSnapshot(1, out var targetMob, out _, out _);
        var targetHp = hasTarget && targetMob is not null ? LegacyMobCombatState.Read(targetMob).CurrentScore.Hp : -1;
        Check(tick.Attacks is [{ Damage: 985, Missed: false, SkillIndex: 88, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Shadow Blade row 88 InstanceType 1 attack changed physical damage or escaped its target view. attacks=[{attackSummary}], combat={hasCombat}:{mode}/{targetId}/[{string.Join(",", enemies)}], targetHp={targetHp}.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 88, damage: 985);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Shadow Blade-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task LightningAttackUsesResistAndAttackOneFieldsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec,
        bool repeatedFourthSlot = false)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetClan: 5,
            npcHasLightningBar: true,
            npcHasLightningFourthSlot: repeatedFourthSlot,
            targetLightningResist: 50);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var attackSummary = string.Join(";", tick.Attacks.Select(static attack =>
            $"damage={attack.Damage},miss={attack.Missed},skill={attack.SkillIndex}/{attack.SkillParm}/{attack.Motion},view=[{string.Join(",", attack.ViewRecipientConnectionIds ?? Array.Empty<int>())}]"));
        var hasCombat = hub.TryGetNpcCombatState(npcId, out var mode, out var targetId, out var enemies);
        var hasTarget = hub.TryGetCharacterSnapshot(1, out var targetMob, out _, out _);
        var targetHp = hasTarget && targetMob is not null ? LegacyMobCombatState.Read(targetMob).CurrentScore.Hp : -1;
        Check(tick.Attacks is [{ Damage: 1477, Missed: false, SkillIndex: 33, SkillParm: 1, Motion: 4 } attack] &&
              attack.AttackerConnectionId == npcId && attack.TargetConnectionId == 1 &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            $"The Lightning-bar attack did not apply Resist[1] and capture only the target viewer. attacks=[{attackSummary}], combat={hasCombat}:{mode}/{targetId}/[{string.Join(",", enemies)}], targetHp={targetHp}.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10,
            skillIndex: 33, damage: 1477);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The Lightning-bar target did not receive the authoritative HP/MP refresh.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task DeadPartySummonCleanupRelaysCapturedViewAsync(
        NetworkStream attackerSendStream,
        NetworkStream fringeSendStream,
        NetworkStream outsideSendStream,
        NetworkStream attackerReadStream,
        NetworkStream fringeReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var hub = new WorldHub(summonCatalog: LoadSummonCatalog());
        Check(hub.Enter(1, "SUMMON_CLEANUP_ATTACKER", (bytes, token) => attackerSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(2, "SUMMON_CLEANUP_PARTY_MEMBER", static (_, _) => ValueTask.CompletedTask) &&
              hub.Enter(3, "SUMMON_CLEANUP_TARGET_OWNER", static (_, _) => ValueTask.CompletedTask) &&
              hub.Enter(4, "SUMMON_CLEANUP_FRINGE_VIEWER", (bytes, token) => fringeSendStream.WriteAsync(bytes, token)) &&
              hub.Enter(5, "SUMMON_CLEANUP_OUTSIDE_VIEWER", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)),
            "The summon-cleanup loopback participants could not be registered.");

        for (var connectionId = 1; connectionId <= 5; connectionId++)
        {
            var mob = CreateMob($"SC{connectionId}", hp: 1_000, clan: 5, skillBar: false);
            if (connectionId == 1)
            {
                var score = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
                (score with { Damage = 120 }).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            }
            Check(hub.SetCharacterState(connectionId, 0, 0, clan: 5, guildLevel: 0, coin: 0, 2000, 2000, mob) &&
                  hub.SetCombatEligibilityState(connectionId, pkMode: false, guilty: false, mapAttribute: 0x40),
                $"Summon-cleanup participant {connectionId} could not be initialized.");
        }
        Check(hub.TrySetParty(1, [1, 2]), "The summon-cleanup attacker party could not be formed.");

        var summonSkill = new LegacySkillDefinition(50, 20, 0, 20, 0, 0, 11, 8, 0, 0, 0, 0, 0, [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Evocar");
        LegacySummonedMob CreateSummon(int summonerConnectionId)
        {
            Check(hub.TryConsumeSkillMana(summonerConnectionId, summonSkill, out _, out _, out _) == LegacySkillManaResult.Accepted,
                $"Summoner {summonerConnectionId} could not pay the loopback summon cost.");
            Check(hub.TryApplySkillAttack(summonerConnectionId, summonerConnectionId, summonSkill, itemData: null, weather: 0, randomFactor: 90, out var outcome) ==
                  LegacySkillAttackResult.Accepted && outcome?.SummonedMobs is { Count: 1 },
                $"Summoner {summonerConnectionId} could not create the loopback summon.");
            return outcome!.SummonedMobs!.Single();
        }

        var deadSummon = CreateSummon(2);
        var attackedSummon = CreateSummon(3);
        BinaryPrimitives.WriteInt32LittleEndian(
            deadSummon.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 0);
        var fringeX = checked((short)(deadSummon.PositionX - 16));
        var fringeY = checked((short)(deadSummon.PositionY - 16));
        var attackerX = checked((short)(deadSummon.PositionX + 15));
        var attackerY = checked((short)(deadSummon.PositionY + 15));
        Check(hub.UpdatePosition(1, attackerX, attackerY) &&
              hub.UpdatePosition(2, checked((short)(deadSummon.PositionX + 100)), checked((short)(deadSummon.PositionY + 100))) &&
              hub.UpdatePosition(3, checked((short)(deadSummon.PositionX + 100)), checked((short)(deadSummon.PositionY + 100))) &&
              hub.UpdatePosition(4, fringeX, fringeY) &&
              hub.UpdatePosition(5, checked((short)(deadSummon.PositionX + 17)), checked((short)(deadSummon.PositionY + 17))),
            "Summon-cleanup viewers could not be positioned around the dead summon tile.");

        var request = new LegacyPhysicalAttackBatchRequest(
            AttackerConnectionId: 1,
            Slots:
            [
                new LegacyPhysicalAttackBatchSlotRequest(
                    0,
                    attackedSummon.ConnectionId,
                    new LegacyPhysicalAttackBatchSlotRolls(RandomFactor: 99, AttackMissRoll: 99)),
            ]);
        Check(hub.TryApplyPhysicalAttackBatch(request, out var batch) && batch is { CompletedNormally: true } &&
              batch.SlotOutcomes.Single().Outcome is { Damage: > 0, RemainingHp: > 0 } &&
              batch.ViewRemovalEffects is [{ TargetConnectionId: var removedId, ViewRecipientConnectionIds: { } recipients }] &&
              removedId == deadSummon.ConnectionId && recipients.Contains(1) && recipients.Contains(4) &&
              !recipients.Contains(2) && !recipients.Contains(3) && !recipients.Contains(5),
            "The positive attack did not capture only the living viewers of its dead party summon.");

        var removalFrame = new RemoveMobConfirmation(checked((ushort)deadSummon.ConnectionId), removeType: 1)
            .ToFrame(codec, clientTick: 1, keywordIndex: 0);
        await attackerSendStream.WriteAsync(removalFrame);
        await LegacyAttackViewRemovalRelay.RelayCapturedViewAsync(
            hub,
            senderConnectionId: 1,
            batch!.ViewRemovalEffects.Single(),
            removalFrame,
            CancellationToken.None);

        var attackerRemoval = await ReadFrameAsync(attackerReadStream, codec);
        var fringeRemoval = await ReadFrameAsync(fringeReadStream, codec);
        foreach (var removal in new[] { attackerRemoval, fringeRemoval })
            Check(removal.Header.Type == RemoveMobConfirmation.MessageType && removal.Header.Id == deadSummon.ConnectionId &&
                  removal.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(removal.Payload.Span) == 1,
                "A captured viewer did not receive the type-1 dead-summon RemoveMob frame.");
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static LegacySummonCatalog LoadSummonCatalog()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Backup", "Tools", "ReferenceSources", "TMProject2GlobalClient", "Servidor", "Server", "TMSrv", "run", "BaseSummon");
            if (Directory.Exists(candidate))
                return LegacySummonCatalog.Load(candidate);
        }

        throw new DirectoryNotFoundException("The TMProject 7.69 BaseSummon fixtures were not found from the E2E output directory.");
    }

    private static void AddRouteFiveOwner(WorldHub hub, int connectionId, string name, short x, short y, short special3)
    {
        var mob = CreateMob(name, hp: 1_000, clan: 5, skillBar: false);
        var baseScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        (baseScore with { Special3 = special3 }).Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        var currentScore = LegacyScore.Read(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        (currentScore with { Special3 = special3 }).Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        Check(hub.Enter(connectionId, name, static (_, _) => ValueTask.CompletedTask) &&
              hub.SetCharacterState(connectionId, 0, 0, clan: 5, guildLevel: 0, coin: 0, x, y, mob),
            "A RouteType 5 loopback owner could not be registered.");
    }

    private static async Task PositiveAttackIsRelayedOnlyToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(targetSendStream, observerSendStream, targetHp: 10_000, scroll: false);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ Damage: > 0, Missed: false } attack] && attack.AttackerConnectionId == npcId &&
              attack.TargetConnectionId == 1 && attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            "The in-range generated NPC did not capture the target-only view and positive attack outcome.");

        var attackFrame = await ReadFrameAsync(targetReadStream, codec);
        AssertAttackOne(attackFrame, npcId, targetId: 1, positionX: 10, positionY: 10);
        var hpMpFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(hpMpFrame.Header.Type == SetHpMpConfirmation.MessageType,
            "The live target did not receive its authoritative HP/MP refresh after the attack.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task DeathWithoutScrollIsRelayedToCapturedViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(targetSendStream, observerSendStream, targetHp: 1, scroll: false);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ Damage: > 0, TargetDied: true, TargetRevived: false } attack] &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]),
            "A lethal hit without a scroll did not retain the authoritative death and view snapshot.");

        AssertAttackOne(await ReadFrameAsync(targetReadStream, codec), npcId, targetId: 1, positionX: 10, positionY: 10);
        var deathFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(deathFrame.Header.Type == LegacyMobDeathConfirmation.MessageType &&
              deathFrame.Header.Size == LegacyMobDeathConfirmation.PacketSize && deathFrame.IsChecksumValid &&
              BinaryPrimitives.ReadUInt16LittleEndian(deathFrame.Payload.Span[4..]) == 1 &&
              BinaryPrimitives.ReadUInt16LittleEndian(deathFrame.Payload.Span[6..]) == npcId,
            "The no-scroll death relay did not encode the authoritative killed player and killer mob IDs.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task ResurrectionScrollRefreshUsesExistingCallerBoundaryAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(targetSendStream, observerSendStream, targetHp: 1, scroll: true);
        var refreshed = new List<(int ConnectionId, int Slot, LegacyItem Item, IReadOnlyList<int> Recipients)>();
        var runtime = CreateRuntime(hub, codec, async (connectionId, _, slot, item, recipients, _) =>
        {
            refreshed.Add((connectionId, slot, item, recipients));
            var itemFrame = new SendItemConfirmation(1, checked((short)slot), item)
                .ToFrame(codec, 1, 0, checked((ushort)connectionId));
            await hub.SendAsync(connectionId, itemFrame, CancellationToken.None);
        });
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks is [{ TargetDied: false, TargetRevived: true, ConsumedItemSlot: 0,
                  ConsumedItem: { Index: 3463, Effect1: 61, Value1: 1 } } attack] &&
              attack.ViewRecipientConnectionIds is { Count: 1 } recipients && recipients.SequenceEqual([1]) &&
              refreshed is [(1, 0, { Index: 3463, Effect1: 61, Value1: 1 }, var refreshRecipients)] &&
              refreshRecipients.SequenceEqual([1]),
            "A lethal hit with a resurrection scroll did not call the refresh boundary with its captured target view.");

        AssertAttackOne(await ReadFrameAsync(targetReadStream, codec), npcId, targetId: 1, positionX: 10, positionY: 10);
        var itemFrameReceived = await ReadFrameAsync(targetReadStream, codec);
        Check(itemFrameReceived.Header.Type == SendItemConfirmation.MessageType && itemFrameReceived.IsChecksumValid,
            "The resurrection refresh boundary did not deliver its consumed-item frame.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task ChaseMovementRelaysItsNewViewAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 26,
            targetY: 20,
            observerX: 41,
            observerY: 20,
            npcX: 20,
            npcY: 20);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        Check(tick.Attacks.Count == 0 && hub.TryGetNpcSnapshot(npcId, out var movedNpc) && movedNpc is not null &&
              movedNpc.PositionX == 25 && movedNpc.PositionY == 19,
            "The loopback Combat shard did not apply the generated-NPC chase step without attacking its still-distant target.");

        var targetAction = await ReadFrameAsync(targetReadStream, codec);
        Check(ActionRequest.TryParse(targetAction, out var targetMove) &&
              targetMove is { PositionX: 20, PositionY: 20, TargetX: 25, TargetY: 19, Effect: 0, Speed: 1 },
            "The player in the overlapping view did not receive the source-shaped chase Action.");
        var entrantCreate = await ReadFrameAsync(observerReadStream, codec);
        var entrantAction = await ReadFrameAsync(observerReadStream, codec);
        Check(entrantCreate.Header.Type == CreateMobConfirmationV769.MessageType && entrantCreate.IsChecksumValid &&
              BinaryPrimitives.ReadInt16LittleEndian(entrantCreate.Payload.Span[CreateMobConfirmationV769.PositionXOffset..]) == 25 &&
              BinaryPrimitives.ReadInt16LittleEndian(entrantCreate.Payload.Span[CreateMobConfirmationV769.PositionYOffset..]) == 19 &&
              BinaryPrimitives.ReadUInt16LittleEndian(entrantCreate.Payload.Span[CreateMobConfirmationV769.MobIdOffset..]) == npcId &&
              BinaryPrimitives.ReadUInt16LittleEndian(entrantCreate.Payload.Span[CreateMobConfirmationV769.CreateTypeOffset..]) == 2 &&
              ActionRequest.TryParse(entrantAction, out var entrantMove) && entrantMove is { TargetX: 25, TargetY: 19 },
            "The player entering the destination view did not receive the NPC CreateMob followed by its chase Action.");
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task LeashReturnRelaysMovementAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 28,
            targetY: 10,
            observerX: 50,
            observerY: 20,
            npcX: 27,
            npcY: 10,
            npcAttackRun: 1);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var hasReturnedNpc = hub.TryGetNpcSnapshot(npcId, out var returnedNpc);
        Check(tick.Attacks.Count == 0 &&
              hub.TryGetNpcCombatState(npcId, out var mode, out var targetId, out var enemies) &&
              mode == LegacyNpcMode.Peace && targetId == 0 && enemies.All(static id => id == 0) &&
              hasReturnedNpc && returnedNpc is not null && returnedNpc.PositionX < 27,
            "The generated NPC did not clear combat and return toward its route after leaving the leash.");

        var targetAction = await ReadFrameAsync(targetReadStream, codec);
        Check(ActionRequest.TryParse(targetAction, out var returnMove) &&
              returnMove is { PositionX: 27, PositionY: 10, Effect: 0, Speed: 1 } &&
              returnMove.TargetX == returnedNpc!.PositionX && returnMove.TargetY == returnedNpc.PositionY,
            "The captured old view did not receive the generated NPC's source-shaped leash-return Action.");
        await AssertNoFrameAsync(observerReadStream);
    }

    private static async Task RangedNpcRetreatRelaysMovementAcrossViewsAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream outsideSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        NetworkStream outsideReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 11,
            targetY: 10,
            observerX: 26,
            observerY: 10,
            npcX: 10,
            npcY: 10,
            npcAttackRun: 4,
            npcHasRangeWeapon: true,
            npcBaseDexterity: 10);
        Check(hub.Enter(3, "LOOPBACK_OUTSIDE", (bytes, token) => outsideSendStream.WriteAsync(bytes, token)) &&
              hub.SetCharacterState(3, 0, 0, clan: 3, guildLevel: 0, coin: 0,
                  positionX: 27, positionY: 10, CreateMob("OUTSIDE", 1000, clan: 3, skillBar: false)),
            "Could not register the outside-view loopback peer.");

        var randomCall = 0;
        int NextRoll(int _) => ++randomCall == 2 ? 11 : 0;
        var runtime = CreateRuntime(hub, codec, onRefresh: null,
            itemData: CreateItemDataWithWeaponRange(4),
            nextRoll: NextRoll);
        var tick = await runtime.TickShardAsync(npcId % LegacyMobAiRuntime.ShardCount);
        var hasNpc = hub.TryGetNpcSnapshot(npcId, out var movedNpc);
        Check(tick.Attacks.Count == 0 && hasNpc &&
              movedNpc is { PositionX: 9, PositionY: 10 },
            $"The generated ranged NPC did not retreat from its close target after the failed DEX roll; visited=[{string.Join(',', tick.VisitedNpcIds)}], position={(hasNpc ? $"{movedNpc!.PositionX},{movedNpc.PositionY}" : "missing")}.");

        var targetActionFrame = await ReadFrameAsync(targetReadStream, codec);
        Check(ActionRequest.TryParse(targetActionFrame, out var targetAction) &&
              targetAction is { PositionX: 10, PositionY: 10, TargetX: 9, TargetY: 10, Effect: 0, Speed: 4 },
            "The target in the old and destination views did not receive the retreat Action.");

        var edgeActionFrame = await ReadFrameAsync(observerReadStream, codec);
        var edgeRemovalFrame = await ReadFrameAsync(observerReadStream, codec);
        Check(ActionRequest.TryParse(edgeActionFrame, out var edgeAction) &&
              edgeAction is { PositionX: 10, PositionY: 10, TargetX: 9, TargetY: 10, Effect: 0, Speed: 4 } &&
              edgeRemovalFrame.Header.Type == RemoveMobConfirmation.MessageType &&
              edgeRemovalFrame.Header.Id == npcId && edgeRemovalFrame.IsChecksumValid &&
              BinaryPrimitives.ReadInt32LittleEndian(edgeRemovalFrame.Payload.Span) == 0,
            "The viewer leaving the destination view did not receive retreat Action followed by RemoveMob(type=0).");
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
        await AssertNoFrameAsync(outsideReadStream);
    }

    private static async Task PeaceRouteMovementRelaysActionAsync(
        NetworkStream targetSendStream,
        NetworkStream observerSendStream,
        NetworkStream targetReadStream,
        NetworkStream observerReadStream,
        LegacyFrameCodec codec)
    {
        var (hub, npcId) = CreateScenario(
            targetSendStream,
            observerSendStream,
            targetHp: 100_000,
            scroll: false,
            targetX: 50,
            targetY: 50,
            observerX: 27,
            observerY: 11,
            npcX: 27,
            npcY: 10,
            npcAttackRun: 1,
            npcStrength: 1,
            targetClan: 2,
            observerClan: 2,
            seedCombat: false);
        var runtime = CreateRuntime(hub, codec, onRefresh: null);
        LegacyMobAiTickResult? visitedTick = null;
        for (var callback = 0; callback < LegacyMobAiRuntime.PeaceShardCount; callback++)
        {
            var tick = await runtime.TickNextShardAsync();
            if (tick.VisitedPeaceNpcIds.Contains(npcId))
            {
                visitedTick = tick;
                break;
            }
        }

        var hasMovedNpc = hub.TryGetNpcSnapshot(npcId, out var movedNpc);
        Check(visitedTick is not null && visitedTick.PeaceShardIndex >= 0 &&
              hasMovedNpc && movedNpc is not null &&
              Math.Max(Math.Abs(movedNpc.PositionX - 10), Math.Abs(movedNpc.PositionY - 10)) <
              Math.Max(Math.Abs(27 - 10), Math.Abs(10 - 10)),
            "The six-shard Peace processor did not move the generated NPC toward its active segment.");

        var actionFrame = await ReadFrameAsync(observerReadStream, codec);
        Check(ActionRequest.TryParse(actionFrame, out var action) &&
              action is { PositionX: 27, PositionY: 10, Effect: 0, Speed: 1 } &&
              action.TargetX == movedNpc!.PositionX && action.TargetY == movedNpc.PositionY,
            "The in-view loopback observer did not receive the Peace route's ordinary Action.");
        await AssertNoFrameAsync(targetReadStream);
        await AssertNoFrameAsync(observerReadStream);
    }

    private static LegacyMobAiRuntime CreateRuntime(
        WorldHub hub,
        LegacyFrameCodec codec,
        Func<int, byte[], int, LegacyItem, IReadOnlyList<int>, CancellationToken, ValueTask>? onRefresh,
        LegacyItemDataTable? itemData = null,
        Func<int, int>? nextRoll = null)
        => new(hub, itemData, (outcome, cancellationToken) => LegacyMobAiRelay.RelayAsync(
            hub, outcome, codec, onRefresh, cancellationToken, clientTick: 1, keywordIndex: 0), nextRoll: nextRoll ?? (static _ => 0));

    private static (WorldHub Hub, int NpcId) CreateScenario(
        NetworkStream targetStream,
        NetworkStream observerStream,
        int targetHp,
        bool scroll,
        short targetX = 10,
        short targetY = 10,
        short observerX = 27,
        short observerY = 27,
        short npcX = 10,
        short npcY = 10,
        byte npcAttackRun = 0,
        short npcStrength = 0,
        byte targetClan = 5,
        byte observerClan = 5,
        bool seedCombat = true,
        bool npcHasRangeWeapon = false,
        bool npcHasCarbunkleFireBar = false,
        bool npcHasCarbunkleFireRepeatedBar = false,
        bool npcHasInfernoBar = false,
        bool npcHasPhoenixFireBar = false,
        bool npcHasFeraFlamejanteBar = false,
        bool npcHasExterminarBar = false,
        bool npcHasMixedExterminarBar = false,
        bool npcHasFlashBar = false,
        bool npcHasMixedEnfraquecerBar = false,
        bool npcHasToqueSagradoBar = false,
        bool npcHasGolpeDuploBar = false,
        bool npcHasEspadaFenixBar = false,
        bool npcHasArqueiroElfoBar = false,
        bool npcHasKalitzMagoBar = false,
        bool npcHasMagoInfMixedBar = false,
        bool npcHasMagoCiclopeBar = false,
        bool npcHasBruxaNegraBar = false,
        bool npcHasMagoAmaldMixedBar = false,
        bool npcHasDarkShadowPrimaryOnlyBar = false,
        bool npcHasRangerInfMixedBar = false,
        bool npcHasShamaAmaldMixedBar = false,
        bool npcHasShamanInfMixedBar = false,
        bool npcHasBruxaElficaMixedBar = false,
        bool npcHasLichDemoMixedBar = false,
        bool npcHasTrollDualSkillBar = false,
        bool npcHasNevascaBar = false,
        bool npcHasEspadaFlamejanteBar = false,
        bool npcHasPoisonMistBar = false,
        byte targetFireResist = 0,
        byte targetPoisonMistResist = 0,
        bool npcHasGaiaFuryBar = false,
        byte targetGaiaResist = 0,
        bool npcHasShadowBladeBar = false,
        byte targetShadowBladeResist = 0,
        bool npcHasLightningBar = false,
        bool npcHasLightningFourthSlot = false,
        byte targetLightningResist = 0,
        byte targetIceResist = 0,
        byte[]? npcSkillBar = null,
        short? npcBaseDexterity = null)
    {
        var mobTemplate = CreateMob("Wolf", hp: 10_000, clan: npcHasEspadaFlamejanteBar ? (byte)8 : npcHasMagoCiclopeBar || npcHasBruxaNegraBar || npcHasFlashBar || npcHasMixedEnfraquecerBar || npcHasToqueSagradoBar || npcHasGolpeDuploBar || npcHasEspadaFenixBar || npcHasArqueiroElfoBar || npcHasKalitzMagoBar || npcHasMagoInfMixedBar || npcHasMagoAmaldMixedBar || npcHasDarkShadowPrimaryOnlyBar || npcHasRangerInfMixedBar || npcHasShamaAmaldMixedBar || npcHasShamanInfMixedBar || npcHasBruxaElficaMixedBar || npcHasLichDemoMixedBar ? (byte)1 : (byte)2, skillBar: true);
        if (npcHasCarbunkleFireBar)
            new byte[] { 32, 32, 32, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasCarbunkleFireRepeatedBar)
            new byte[] { 32, 32, 32, 32 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasInfernoBar)
            new byte[] { 39, 39, 39, 39 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasPhoenixFireBar)
            new byte[] { 38, 38, 38, 38 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasFeraFlamejanteBar)
            new byte[] { 48, 48, 48, 51 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasExterminarBar)
            new byte[] { 22, 22, 22, 22 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasMixedExterminarBar)
            new byte[] { byte.MaxValue, byte.MaxValue, 22, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasFlashBar)
            new byte[] { byte.MaxValue, byte.MaxValue, 26, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasMixedEnfraquecerBar)
            new byte[] { byte.MaxValue, byte.MaxValue, 51, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasToqueSagradoBar)
            new byte[] { byte.MaxValue, byte.MaxValue, 1, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasGolpeDuploBar)
            new byte[] { byte.MaxValue, byte.MaxValue, 2, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasEspadaFenixBar)
            new byte[] { byte.MaxValue, byte.MaxValue, 12, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasArqueiroElfoBar)
            new byte[] { byte.MaxValue, byte.MaxValue, 79, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasKalitzMagoBar)
            new byte[] { 33, 33, 37, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasMagoInfMixedBar)
            new byte[] { 36, 34, 39, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasMagoCiclopeBar)
            new byte[] { 33, 36, 27, 40 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasBruxaNegraBar)
            new byte[] { 33, 39, 38, 36 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasMagoAmaldMixedBar)
            new byte[] { 36, 34, 26, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasDarkShadowPrimaryOnlyBar)
            new byte[] { 26, byte.MaxValue, byte.MaxValue, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasRangerInfMixedBar)
            new byte[] { byte.MaxValue, 79, 73, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasShamaAmaldMixedBar)
            new byte[] { 19, 33, 48, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasShamanInfMixedBar)
            new byte[] { 52, 55, 63, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasBruxaElficaMixedBar)
            new byte[] { 38, 34, 38, 51 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasLichDemoMixedBar)
            new byte[] { 52, 52, 51, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasTrollDualSkillBar)
            new byte[] { 32, 35, 32, 35 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasNevascaBar)
            new byte[] { 36, 36, 36, 36 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasEspadaFlamejanteBar)
            new byte[] { 17, 17, 17, 17 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasPoisonMistBar)
            new byte[] { 40, 40, 40, 40 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasGaiaFuryBar)
            new byte[] { 52, 52, 52, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasShadowBladeBar)
            new byte[] { 88, 88, 88, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasLightningBar)
            new byte[] { 33, 33, 33, byte.MaxValue }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcHasLightningFourthSlot)
            new byte[] { 33, 33, 33, 33 }.AsSpan().CopyTo(
                mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        if (npcSkillBar is not null)
        {
            if (npcSkillBar.Length != 4)
                throw new ArgumentException("A generated NPC SkillBar fixture must have four entries.", nameof(npcSkillBar));
            npcSkillBar.AsSpan().CopyTo(mobTemplate.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4));
        }
        if (npcHasRangeWeapon)
            new LegacyItem(1, 0, 0, 0, 0, 0, 0).Write(mobTemplate.AsSpan(LegacyAccountSnapshot.MobEquipmentOffset, LegacyItem.SizeInBytes));
        if (npcAttackRun != 0 || npcStrength != 0)
        {
            var baseScore = LegacyScore.Read(mobTemplate.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
            (baseScore with { AttackRun = npcAttackRun, Strength = npcStrength }).Write(mobTemplate.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
            var currentScore = LegacyScore.Read(mobTemplate.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
            (currentScore with { AttackRun = npcAttackRun, Strength = npcStrength }).Write(mobTemplate.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        }
        if (npcBaseDexterity is { } baseDexterity)
        {
            var baseScore = LegacyScore.Read(mobTemplate.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
            (baseScore with { Dexterity = baseDexterity }).Write(mobTemplate.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        }
        var catalog = LegacyNpcGenerationCatalog.Parse("""
            #[42]
            MaxNumMob: 10
            Leader: Wolf
            RouteType: 0
            StartX: 10
            StartY: 10
            StartRange: 4
            StartWait: 0
            Segment1X: 20
            Segment1Y: 10
            DestX: 30
            DestY: 10
            """, new Dictionary<string, byte[]> { ["Wolf"] = mobTemplate });
        var hub = new WorldHub(npcGenerationCatalog: catalog, skillData: MobAiSkillDataFixture.Create());
        Check(hub.Enter(1, "LOOPBACK_TARGET", (bytes, token) => targetStream.WriteAsync(bytes, token)),
            "Could not register the loopback target.");
        Check(hub.Enter(2, "LOOPBACK_OBSERVER", (bytes, token) => observerStream.WriteAsync(bytes, token)),
            "Could not register the loopback observer.");
        var targetMob = CreateMob("TARGET", targetHp, clan: targetClan, skillBar: false);
        targetMob[LegacyAccountSnapshot.MobResistOffset] = targetFireResist;
        if (npcHasShadowBladeBar)
            targetMob[LegacyAccountSnapshot.MobResistOffset] = targetShadowBladeResist;
        if (npcHasLightningBar || npcHasKalitzMagoBar || npcHasShamaAmaldMixedBar)
            targetMob[LegacyAccountSnapshot.MobResistOffset + 3] = targetLightningResist;
        if (npcHasNevascaBar || npcHasBruxaElficaMixedBar || npcHasMagoInfMixedBar || npcHasMagoCiclopeBar || npcHasMagoAmaldMixedBar || npcHasDarkShadowPrimaryOnlyBar || npcHasRangerInfMixedBar)
            targetMob[LegacyAccountSnapshot.MobResistOffset + 1] = targetIceResist;
        targetMob[LegacyAccountSnapshot.MobResistOffset + 2] =
            npcHasPoisonMistBar ? targetPoisonMistResist : targetGaiaResist;
        if (scroll)
            new LegacyItem(3463, 61, 2, 0, 0, 0, 0).Write(
                targetMob.AsSpan(LegacyAccountSnapshot.MobCarryOffset, LegacyItem.SizeInBytes));
        Check(hub.TrySpawnGeneratedNpc(42, out var npc, randomRoll: static _ => 0, positionX: npcX, positionY: npcY) && npc is not null,
            "Could not spawn the loopback generated NPC.");
        var spawnedNpc = npc!;
        Check(hub.SetCharacterState(1, 0, 0, clan: targetClan, guildLevel: 0, coin: 0,
            positionX: targetX, positionY: targetY, targetMob), "Could not set the target MOB snapshot.");
        Check(hub.SetCharacterState(2, 0, 0, clan: observerClan, guildLevel: 0, coin: 0,
            positionX: observerX, positionY: observerY, CreateMob("OBSERVER", 1000, clan: observerClan, skillBar: false)),
            "Could not set the observer MOB snapshot.");
        if (seedCombat)
            Check(hub.SetNpcCombatState(spawnedNpc.ConnectionId, LegacyNpcMode.Combat, 1, [1]),
                "Could not seed the loopback NPC's explicit target.");
        return (hub, spawnedNpc.ConnectionId);
    }

    private static LegacyItemDataTable CreateItemDataWithWeaponRange(short range)
    {
        const int itemIndex = 1;
        var decoded = new byte[LegacyItemDataTable.BodySizeInBytes];
        var record = decoded.AsSpan(itemIndex * LegacyItemDataTable.RecordSizeInBytes, LegacyItemDataTable.RecordSizeInBytes);
        BinaryPrimitives.WriteInt16LittleEndian(record[80..], LegacyItemEffect.Range);
        BinaryPrimitives.WriteInt16LittleEndian(record[82..], range);

        var encoded = new byte[decoded.Length];
        for (var index = 0; index < decoded.Length; index++)
            encoded[index] = (byte)(decoded[index] ^ 0x5A);
        return LegacyItemDataTable.Load(encoded);
    }

    private static LegacyItemDataTable CreateItemDataWithWeaponType(short weaponType)
    {
        const int itemIndex = 1;
        var decoded = new byte[LegacyItemDataTable.BodySizeInBytes];
        var record = decoded.AsSpan(itemIndex * LegacyItemDataTable.RecordSizeInBytes, LegacyItemDataTable.RecordSizeInBytes);
        BinaryPrimitives.WriteInt16LittleEndian(record[80..], LegacyItemEffect.WeaponType);
        BinaryPrimitives.WriteInt16LittleEndian(record[82..], weaponType);

        var encoded = new byte[decoded.Length];
        for (var index = 0; index < decoded.Length; index++)
            encoded[index] = (byte)(decoded[index] ^ 0x5A);
        return LegacyItemDataTable.Load(encoded);
    }

    private static byte[] CreateMob(string name, int hp, byte clan, bool skillBar)
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes(name).CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset, 16));
        mob[LegacyAccountSnapshot.MobClanOffset] = clan;
        new LegacyScore(100, 10, 1000, 0, 0, 0, 0, Math.Max(1000, hp), 1000, hp, 1000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset));
        new LegacyScore(100, 10, 1000, 0, 0, 0, 0, Math.Max(1000, hp), 1000, hp, 1000, 0, 0, 0, 0, 0, 0, 0, 0)
            .Write(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset));
        if (skillBar)
            mob.AsSpan(LegacyAccountSnapshot.MobSkillBarOffset, 4).Fill(byte.MaxValue);
        return mob;
    }

    private static void AssertAttackOne(
        DecodedFrame frame,
        int npcId,
        int targetId,
        short positionX,
        short positionY,
        short? targetX = null,
        short? targetY = null,
        short skillIndex = -1,
        byte skillParm = 1,
        byte motion = 4,
        int? damage = null)
    {
        var payload = frame.Payload.Span;
        Check(frame.Header.Type == AttackRequest.OneTargetMessageType &&
              frame.Header.Size == LegacyMobAttackConfirmation.PacketSize && frame.IsChecksumValid &&
              BinaryPrimitives.ReadInt16LittleEndian(payload[22..]) == positionX &&
              BinaryPrimitives.ReadInt16LittleEndian(payload[24..]) == positionY &&
              BinaryPrimitives.ReadInt16LittleEndian(payload[26..]) == (targetX ?? positionX) &&
              BinaryPrimitives.ReadInt16LittleEndian(payload[28..]) == (targetY ?? positionY) &&
              BinaryPrimitives.ReadUInt16LittleEndian(payload[30..]) == npcId &&
              payload[34] == motion && payload[35] == skillParm &&
              BinaryPrimitives.ReadInt16LittleEndian(payload[44..]) == skillIndex &&
              BinaryPrimitives.ReadInt32LittleEndian(payload[48..]) == targetId &&
              BinaryPrimitives.ReadInt32LittleEndian(payload[52..]) == (damage ?? BinaryPrimitives.ReadInt32LittleEndian(payload[52..])) &&
              BinaryPrimitives.ReadInt32LittleEndian(payload[52..]) > 0,
            "The decoded normal NPC AttackOne did not contain the authoritative 7.69 attack fields.");
    }

    private static async Task<DecodedFrame> ReadFrameAsync(NetworkStream stream, LegacyFrameCodec codec)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var headerBytes = new byte[PacketHeader.SizeInBytes];
        await stream.ReadExactlyAsync(headerBytes, timeout.Token);
        var header = PacketHeader.Read(headerBytes);
        Check(header.Size >= PacketHeader.SizeInBytes && header.Size <= PacketHeader.MaxPacketSize,
            $"Loopback produced invalid frame size {header.Size}.");
        var frameBytes = new byte[header.Size];
        headerBytes.CopyTo(frameBytes, 0);
        await stream.ReadExactlyAsync(frameBytes.AsMemory(PacketHeader.SizeInBytes), timeout.Token);
        return codec.Decode(frameBytes);
    }

    private static async Task AssertNoFrameAsync(NetworkStream stream)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var buffer = new byte[1];
        try
        {
            var read = await stream.ReadAsync(buffer, timeout.Token);
            Check(read == 0, "An observer outside the captured NPC view received bytes.");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
        }
    }

    private static void AssertLoopbackEndpoints(TcpClient client, TcpClient accepted)
    {
        Check(client.Client.LocalEndPoint is IPEndPoint local && IPAddress.IsLoopback(local.Address) &&
              client.Client.RemoteEndPoint is IPEndPoint remote && IPAddress.IsLoopback(remote.Address) &&
              accepted.Client.LocalEndPoint is IPEndPoint acceptedLocal && IPAddress.IsLoopback(acceptedLocal.Address) &&
              accepted.Client.RemoteEndPoint is IPEndPoint acceptedRemote && IPAddress.IsLoopback(acceptedRemote.Address),
            "A loopback harness socket exposed a non-loopback local or remote endpoint.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
