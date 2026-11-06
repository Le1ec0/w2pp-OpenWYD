using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackMortarTests
{
    public static void Run()
    {
        SkillGateAllowsMortarToReachGroundItemValidation();
        WorldHubUsesThe7_69TransposedGroundGridLookupWithoutConsumingTheItem();
        AttackConfirmationSetsMortarMotionWithoutChangingTheWireShape();
    }

    private static void SkillGateAllowsMortarToReachGroundItemValidation()
    {
        const string mortar = "97,69,1,20,15,1,1,40,0,0,0,0,0,8.0.0.7.0.0.0.0,8.0.0.7.0.0.0.0,0,0,1,2,0,0,0,1,Morteiro";
        var table = LegacySkillDataTable.Load(new StringReader(mortar));

        var decision = LegacySkillAttackGate.Evaluate(
            table,
            skillIndex: 97,
            characterClass: 0,
            learnedSkill: 0,
            clientTick: 5_000,
            previousAttackTick: 1_000);

        Check(decision is { IsAccepted: true, RequiresMortarItem: true, LearnedSkillError: null },
            "Mortar skill 97 must reach the ground-item gate after its ordinary shared-skill checks.");
    }

    private static void WorldHubUsesThe7_69TransposedGroundGridLookupWithoutConsumingTheItem()
    {
        const ushort attackX = 50;
        const ushort attackY = 70;
        var world = new WorldHub();
        Check(world.EnterGroundItem(new LegacyItem(746, 0, 0, 0, 0, 0, 0), (short)attackY, (short)attackX) > 0,
            "The mortar fixture could not be registered at the transposed 7.69 grid cell.");

        Check(world.HasMortarItemAtLegacyAttackPosition(attackX, attackY),
            "The 7.69 lookup [PosX][PosY] did not find the item at world coordinates (PosY, PosX).");
        Check(world.HasMortarItemAtLegacyAttackPosition(attackX, attackY),
            "Checking a mortar must not consume the item from the ground grid.");

        var ordinaryOrientation = new WorldHub();
        Check(ordinaryOrientation.EnterGroundItem(new LegacyItem(746, 0, 0, 0, 0, 0, 0), (short)attackX, (short)attackY) > 0,
            "The ordinary-orientation mortar fixture could not be registered.");
        Check(!ordinaryOrientation.HasMortarItemAtLegacyAttackPosition(attackX, attackY),
            "The C# lookup normalized the transposed coordinate access instead of preserving 7.69.");

        var wrongItem = new WorldHub();
        Check(wrongItem.EnterGroundItem(new LegacyItem(745, 0, 0, 0, 0, 0, 0), (short)attackY, (short)attackX) > 0,
            "The wrong-item fixture could not be registered.");
        Check(!wrongItem.HasMortarItemAtLegacyAttackPosition(attackX, attackY),
            "A ground item other than index 746 was accepted as a mortar.");

        Check(!world.HasMortarItemAtLegacyAttackPosition(0, attackY) &&
            !world.HasMortarItemAtLegacyAttackPosition(attackX, 4096),
            "The mortar lookup accepted coordinates outside the 7.69 strict 1..4095 bounds.");

        var staticMapItem = new WorldHub();
        staticMapItem.ConfigureMapItems([
            new LegacyMapItemState(1, new LegacyItem(746, 0, 0, 0, 0, 0, 0), (short)attackY, (short)attackX, 0, 0,
                LegacyMapItemStateCodes.Open, 0, 90),
        ]);
        Check(staticMapItem.HasMortarItemAtLegacyAttackPosition(attackX, attackY),
            "A 7.69 pItemGrid mortar backed by a configured map item was not found.");
    }

    private static void AttackConfirmationSetsMortarMotionWithoutChangingTheWireShape()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var payload = new byte[AttackRequest.OneTargetPacketSize - PacketHeader.SizeInBytes];
        var input = codec.Decode(codec.Encode(AttackRequest.OneTargetMessageType, 0, 1234, payload, 7));
        var mob = new byte[LegacyAccountSnapshot.CharacterStride];
        Check(AttackConfirmation.TryCreate(input, mob, out var confirmation) && confirmation is not null,
            "The mortar response fixture could not create an attack confirmation.");

        var response = confirmation ?? throw new InvalidOperationException("Attack confirmation was null.");
        Check(response.TrySetMotion(1), "The attack confirmation could not set the server-owned Motion byte.");
        var encoded = response.ToFrame(codec, input.Header.ClientTick, 7);
        var decoded = codec.Decode(encoded);
        Check(decoded.IsChecksumValid && decoded.Header.Size == AttackRequest.OneTargetPacketSize &&
            decoded.Payload.Span[34] == 1,
            "The mortar Motion byte was not relayed at the existing 7.69 offset or changed the frame shape.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
