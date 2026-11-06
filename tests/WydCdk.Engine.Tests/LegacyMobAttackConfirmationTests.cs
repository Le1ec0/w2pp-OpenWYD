using System.Buffers.Binary;
using WydCdk.Protocol;

internal static class LegacyMobAttackConfirmationTests
{
    public static void Run()
    {
        var codec = LegacyFrameCodec.CreateDefault();
        var confirmation = new LegacyMobAttackConfirmation(
            positionX: 2048,
            positionY: 2049,
            targetX: 2050,
            targetY: 2051,
            attackerId: 24999,
            targetId: 999,
            damage: 1234);
        var frame = codec.Decode(confirmation.ToFrame(codec, clientTick: 123456, keywordIndex: 19));
        var payload = frame.Payload.Span;

        Check(frame.IsChecksumValid && frame.Header.Type == AttackRequest.OneTargetMessageType &&
              frame.Header.Id == LegacyMobAttackConfirmation.SceneId && frame.Header.Size == 68 &&
              frame.Header.ClientTick == 123456,
            "Generated mob attack did not use the packed 68-byte MSG_AttackOne frame.");
        Check(BinaryPrimitives.ReadUInt16LittleEndian(payload[22..]) == 2048 &&
              BinaryPrimitives.ReadUInt16LittleEndian(payload[24..]) == 2049 &&
              BinaryPrimitives.ReadUInt16LittleEndian(payload[26..]) == 2050 &&
              BinaryPrimitives.ReadUInt16LittleEndian(payload[28..]) == 2051 &&
              BinaryPrimitives.ReadUInt16LittleEndian(payload[30..]) == 24999 &&
              BinaryPrimitives.ReadUInt16LittleEndian(payload[32..]) == 52428,
            "MSG_AttackOne did not encode authoritative positions, attacker ID, and progress.");
        Check(payload[34] == 4 && payload[35] == 1 && payload[36] == 0 && payload[37] == 0 &&
              BinaryPrimitives.ReadInt32LittleEndian(payload[4..]) == -1 &&
              BinaryPrimitives.ReadInt32LittleEndian(payload[40..]) == -1 &&
              BinaryPrimitives.ReadInt16LittleEndian(payload[44..]) == -1 &&
              BinaryPrimitives.ReadInt16LittleEndian(payload[46..]) == -1,
            "MSG_AttackOne normal physical skill/motion/resource fields differed from GetAttack.");
        Check(BinaryPrimitives.ReadInt32LittleEndian(payload[48..]) == 999 &&
              BinaryPrimitives.ReadInt32LittleEndian(payload[52..]) == 1234,
            "MSG_AttackOne did not encode the authoritative 32-bit STRUCT_DAM target ID and damage.");

        var npcDamage = codec.Decode(new LegacyMobAttackConfirmation(
            positionX: 2048,
            positionY: 2049,
            targetX: 2048,
            targetY: 2049,
            attackerId: 24999,
            targetId: 20_042,
            damage: 100,
            skillIndex: 6,
            skillParm: 1,
            motion: 4).ToFrame(codec, clientTick: 123456, keywordIndex: 19));
        Check(npcDamage.IsChecksumValid && BinaryPrimitives.ReadInt32LittleEndian(npcDamage.Payload.Span[48..]) == 20_042 &&
              BinaryPrimitives.ReadInt32LittleEndian(npcDamage.Payload.Span[52..]) == 100 &&
              BinaryPrimitives.ReadInt16LittleEndian(npcDamage.Payload.Span[44..]) == 6 &&
              npcDamage.Payload.Span[34] == 4,
            "The generated-mob AttackOne encoder did not support the NPC target selected by Orc_Medico row 29.");

        var fireConfirmation = new LegacyMobAttackConfirmation(
            positionX: 2048,
            positionY: 2049,
            targetX: 2050,
            targetY: 2051,
            attackerId: 24999,
            targetId: 999,
            damage: 1492,
            skillIndex: 32,
            skillParm: 1,
            motion: 4);
        var firePayload = codec.Decode(fireConfirmation.ToFrame(codec, clientTick: 123457, keywordIndex: 20)).Payload.Span;
        Check(firePayload[34] == 4 && firePayload[35] == 1 &&
              BinaryPrimitives.ReadInt16LittleEndian(firePayload[44..]) == 32 &&
              BinaryPrimitives.ReadInt32LittleEndian(firePayload[52..]) == 1492,
            "The Carbunkle Fire-bar attack did not encode its selected SkillIndex, SkillParm, Motion, and resistant damage.");

        var phoenixFrame = codec.Decode(new LegacyMobAttackConfirmation(
            positionX: 2048,
            positionY: 2049,
            targetX: 2050,
            targetY: 2051,
            attackerId: 24999,
            targetId: 999,
            damage: 1492,
            skillIndex: 38,
            skillParm: 1,
            motion: 4).ToFrame(codec, clientTick: 123458, keywordIndex: 21));
        var phoenixPayload = phoenixFrame.Payload.Span;
        Check(phoenixFrame.Header.Size == 68 && phoenixFrame.IsChecksumValid &&
              phoenixPayload[34] == 4 && phoenixPayload[35] == 1 &&
              BinaryPrimitives.ReadInt16LittleEndian(phoenixPayload[44..]) == 38 &&
              BinaryPrimitives.ReadInt32LittleEndian(phoenixPayload[52..]) == 1492,
            "The Phoenix of Fire-bar attack did not preserve the 68-byte AttackOne fields and resistant damage.");

        var poisonFrame = codec.Decode(new LegacyMobAttackConfirmation(
            positionX: 2048,
            positionY: 2049,
            targetX: 2050,
            targetY: 2051,
            attackerId: 24999,
            targetId: 999,
            damage: 1492,
            skillIndex: 40,
            skillParm: 1,
            motion: 4).ToFrame(codec, clientTick: 123459, keywordIndex: 22));
        var poisonPayload = poisonFrame.Payload.Span;
        Check(poisonFrame.Header.Size == 68 && poisonFrame.IsChecksumValid &&
              poisonPayload[34] == 4 && poisonPayload[35] == 1 &&
              BinaryPrimitives.ReadInt16LittleEndian(poisonPayload[44..]) == 40 &&
              BinaryPrimitives.ReadInt32LittleEndian(poisonPayload[52..]) == 1492,
            "The Poison Mist-bar attack did not preserve the 68-byte AttackOne fields and resistant damage.");

        var gaiaConfirmation = new LegacyMobAttackConfirmation(
            positionX: 2048,
            positionY: 2049,
            targetX: 2050,
            targetY: 2051,
            attackerId: 24999,
            targetId: 999,
            damage: 1492,
            skillIndex: 52,
            skillParm: 1,
            motion: 4);
        var gaiaFrame = codec.Decode(gaiaConfirmation.ToFrame(codec, clientTick: 123458, keywordIndex: 21));
        var gaiaPayload = gaiaFrame.Payload.Span;
        Check(gaiaFrame.Header.Size == 68 && gaiaFrame.IsChecksumValid && gaiaPayload[35] == 1 &&
              BinaryPrimitives.ReadInt16LittleEndian(gaiaPayload[44..]) == 52 &&
              BinaryPrimitives.ReadInt32LittleEndian(gaiaPayload[52..]) == 1492,
            "The Gaia Fury-bar attack did not preserve the existing AttackOne wire size and selected skill fields.");

        var rejectedMiss = false;
        try
        {
            _ = new LegacyMobAttackConfirmation(2048, 2049, 2050, 2051, 24999, 999, damage: -3);
        }
        catch (ArgumentOutOfRangeException)
        {
            rejectedMiss = true;
        }
        Check(rejectedMiss, "The normal mob attack encoder accepted the -3 miss marker that 7.69 does not multicast.");

        var deathFrame = codec.Decode(new LegacyMobDeathConfirmation(killedPlayerId: 999, killerMobId: 24999)
            .ToFrame(codec, clientTick: 123457, keywordIndex: 20));
        Check(deathFrame.IsChecksumValid && deathFrame.Header.Type == LegacyMobDeathConfirmation.MessageType &&
              deathFrame.Header.Id == LegacyMobDeathConfirmation.SceneId && deathFrame.Header.Size == 32 &&
              deathFrame.Header.ClientTick == 123457 &&
              BinaryPrimitives.ReadInt32LittleEndian(deathFrame.Payload.Span) == 0 &&
              BinaryPrimitives.ReadUInt16LittleEndian(deathFrame.Payload.Span[4..]) == 999 &&
              BinaryPrimitives.ReadUInt16LittleEndian(deathFrame.Payload.Span[6..]) == 24999 &&
              deathFrame.Payload.Span[8..12].SequenceEqual(new byte[4]) &&
              BinaryPrimitives.ReadInt64LittleEndian(deathFrame.Payload.Span[12..]) == 0,
            "MSG_CNFMobKill did not match the 7.69 packed death notification sent after a non-revived lethal NPC hit.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
