using System.Buffers.Binary;
using System.Text;
using WydCdk.World;

internal static class AttackVineSummonTests
{
    public static void Run()
    {
        NpcRootLoadsTheOptionalVinhaTemplate();
        Skill98RequiresAnActiveAttacker();
        Skill98CreatesVinhaAtTheTargetGridAndStopsTheBatch();
        Skill98DoesNotCreateVinhaOutsideTheGrid();
        Skill98WithoutVinhaTemplateStillStopsTheBatch();
    }

    private static void Skill98RequiresAnActiveAttacker()
    {
        var catalog = LegacyNpcGenerationCatalog.Parse(string.Empty, new Dictionary<string, byte[]> { ["Vinha"] = CreateVinhaTemplate() });
        var noAttackerWorld = new WorldHub(npcGenerationCatalog: catalog);
        Check(noAttackerWorld.TryApplyAttackActionBatch(CreateBatch(2_101, 2_100), out var missingAttackerOutcome) &&
            missingAttackerOutcome is not null,
            "Book Vinha batch without an attacker was rejected instead of returning an outcome.");
        Check(missingAttackerOutcome!.SlotOutcomes is [{ SkillResult: LegacySkillAttackResult.ParticipantNotFound }] &&
            missingAttackerOutcome.SpawnedNpcs.Count == 0 && noAttackerWorld.GetNpcSnapshots().Count == 0,
            "Book Vinha created an NPC without a registered attacker.");

        var deadAttackerWorld = CreateWorld(catalog, attackerHp: 0);
        Check(deadAttackerWorld.TryApplyAttackActionBatch(CreateBatch(2_101, 2_100), out var deadAttackerOutcome) &&
            deadAttackerOutcome is not null,
            "Book Vinha batch with a dead attacker was rejected instead of returning an outcome.");
        Check(deadAttackerOutcome!.SlotOutcomes is [{ SkillResult: LegacySkillAttackResult.AttackerNotAlive }] &&
            deadAttackerOutcome.SpawnedNpcs.Count == 0 && deadAttackerWorld.GetNpcSnapshots().Count == 0,
            "Book Vinha created an NPC for a dead attacker.");
    }

    private static void NpcRootLoadsTheOptionalVinhaTemplate()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"wyd-vinha-{Guid.NewGuid():N}");
        var npcRoot = Path.Combine(tempRoot, "npc");
        Directory.CreateDirectory(npcRoot);
        try
        {
            var generationPath = Path.Combine(tempRoot, "NPCGener.txt");
            File.WriteAllText(generationPath, string.Empty);
            File.WriteAllBytes(Path.Combine(npcRoot, "Vinha"), CreateVinhaTemplate());
            var catalog = LegacyNpcGenerationCatalog.Load(generationPath, npcRoot, optionalTemplateNames: ["Vinha"]);

            Check(catalog.TryCreateNamedNpc("Vinha", 2_101, 2_100, out var generated) && generated is not null,
                "The configured npc root did not load the optional Vinha template.");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static void Skill98CreatesVinhaAtTheTargetGridAndStopsTheBatch()
    {
        var template = CreateVinhaTemplate();
        var catalog = LegacyNpcGenerationCatalog.Parse(string.Empty, new Dictionary<string, byte[]> { ["Vinha"] = template });
        var world = CreateWorld(catalog);
        var batch = CreateBatch(targetX: 2_101, targetY: 2_100);

        Check(world.TryApplyAttackActionBatch(batch, out var outcome) && outcome is not null,
            "Book Vinha attack batch was rejected.");
        Check(outcome!.SlotOutcomes.Count == 1 && outcome.SlotOutcomes[0].SkillResult == LegacySkillAttackResult.Accepted,
            "Book Vinha did not stop after its first target slot.");
        Check(outcome.SpawnedNpcs is [{ ParentSlotIndex: 0 }],
            "Book Vinha did not return the spawned NPC for the host relay.");
        Check(world.GetNpcSnapshots() is [{ PositionX: 2_101, PositionY: 2_100, GenerateIndex: -1, RouteType: 3 }],
            "Book Vinha did not register one dynamic Vinha NPC with its target position and route type.");

        var npc = world.GetNpcSnapshots()[0];
        Check(Encoding.ASCII.GetString(npc.MobSnapshot, LegacyAccountSnapshot.MobNameOffset, 5) == "Vinha",
            "Book Vinha did not retain the Vinha mob template name.");
        Check(BinaryPrimitives.ReadInt32LittleEndian(npc.MobSnapshot.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24)) == 125,
            "Book Vinha did not restore the created NPC's current HP to its template MaxHP.");
        Check(npc.AffectSnapshot.AsSpan().SequenceEqual(new byte[LegacyAccountSnapshot.AffectStride]),
            "Book Vinha did not initialize the created NPC with an empty affect snapshot.");
        Check(world.GetNpcSnapshots().Count == 1,
            "Book Vinha processed a later target slot and created more than one NPC.");
    }

    private static void Skill98DoesNotCreateVinhaOutsideTheGrid()
    {
        foreach (var targetX in new ushort[] { ushort.MaxValue, LegacyMapGrid.HeightWidth })
        {
            var catalog = LegacyNpcGenerationCatalog.Parse(string.Empty, new Dictionary<string, byte[]> { ["Vinha"] = CreateVinhaTemplate() });
            var world = CreateWorld(catalog);
            var batch = CreateBatch(targetX, targetY: 2_100);

            Check(world.TryApplyAttackActionBatch(batch, out var outcome) && outcome is not null,
                $"Book Vinha rejected the batch for out-of-grid X={targetX} instead of ending the target loop.");
            Check(outcome!.SlotOutcomes.Count == 1 && outcome.SpawnedNpcs.Count == 0 && world.GetNpcSnapshots().Count == 0,
                $"Book Vinha created an NPC for out-of-grid X={targetX}.");
        }
    }

    private static void Skill98WithoutVinhaTemplateStillStopsTheBatch()
    {
        var world = CreateWorld(npcGenerationCatalog: null);
        var batch = CreateBatch(targetX: 2_101, targetY: 2_100);

        Check(world.TryApplyAttackActionBatch(batch, out var outcome) && outcome is not null,
            "Book Vinha without its optional NPC template rejected the attack batch.");
        Check(outcome!.SlotOutcomes.Count == 1 && outcome.SpawnedNpcs.Count == 0 && world.GetNpcSnapshots().Count == 0,
            "Book Vinha without its optional NPC template processed more slots or created an NPC.");
    }

    private static WorldHub CreateWorld(LegacyNpcGenerationCatalog? npcGenerationCatalog, int attackerHp = 100)
    {
        var world = new WorldHub(npcGenerationCatalog: npcGenerationCatalog);
        var attackerMob = new byte[LegacyAccountSnapshot.CharacterStride];
        BinaryPrimitives.WriteInt32LittleEndian(
            attackerMob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24),
            attackerHp);
        Check(world.Enter(1, "VINHA_ATTACKER", (_, _) => ValueTask.CompletedTask) &&
            world.SetCharacterState(1, 0, 0, 1, 0, 0, 2_000, 2_000, attackerMob),
            "Book Vinha attacker fixture was not installed.");
        return world;
    }

    private static LegacyAttackActionBatchRequest CreateBatch(ushort targetX, ushort targetY) => new(
        1,
        [
            new LegacyAttackActionBatchSlotRequest(0, LegacyAttackActionSlotKind.Skill, 99, SkillTargetIndex: 0),
            new LegacyAttackActionBatchSlotRequest(1, LegacyAttackActionSlotKind.Skill, 98, SkillTargetIndex: 1),
        ],
        Skill: VinhaSkill(),
        ItemData: null)
    {
        TargetX = targetX,
        TargetY = targetY,
    };

    private static LegacySkillDefinition VinhaSkill() =>
        new(98, 36, 6, 42, 6, 8, 0, 0, 0, 0, 1, 0, 1,
            [], [], 0, 0, 1, 1, 0, 0, 0, 0, "Muro_de_Espinhos");

    private static byte[] CreateVinhaTemplate()
    {
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Encoding.ASCII.GetBytes("Vinha").CopyTo(mob.AsSpan(LegacyAccountSnapshot.MobNameOffset));
        BinaryPrimitives.WriteInt16LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobBaseScoreOffset + 12), 1);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 16), 125);
        BinaryPrimitives.WriteInt32LittleEndian(mob.AsSpan(LegacyAccountSnapshot.MobCurrentScoreOffset + 24), 7);
        return mob;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
