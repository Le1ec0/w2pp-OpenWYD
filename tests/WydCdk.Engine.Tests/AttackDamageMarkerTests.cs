using System.Buffers.Binary;
using WydCdk.Protocol;
using WydCdk.World;

internal static class AttackDamageMarkerTests
{
    public static Task Run()
    {
        Check(LegacyAttackDamageMarkerGate.Classify(-2) == LegacyAttackDamageMarkerKind.Physical,
            "Physical marker -2 was not accepted.");
        Check(LegacyAttackDamageMarkerGate.Classify(-1) == LegacyAttackDamageMarkerKind.Skill,
            "Skill marker -1 was not accepted.");
        Check(LegacyAttackDamageMarkerGate.Classify(0) == LegacyAttackDamageMarkerKind.Empty,
            "Zero marker must be ignored without a crack error.");
        foreach (var invalid in new[] { 1, -3, int.MinValue, int.MaxValue })
            Check(LegacyAttackDamageMarkerGate.Classify(invalid) == LegacyAttackDamageMarkerKind.Invalid,
                $"Nonzero marker {invalid} must be rejected.");

        Check(LegacyAttackDamageMarkerGate.CrackErrorPoints == 10 &&
            LegacyAttackDamageMarkerGate.CrackErrorType == 77,
            "Invalid damage marker must add 10/type77.");
        Check(LegacyCrackErrorRules.Add(0,
            LegacyAttackDamageMarkerGate.CrackErrorPoints,
            LegacyAttackDamageMarkerGate.CrackErrorType) is
            { TotalPoints: 10, AddedPoints: 10, Type: 77, ShouldLog: true, RequiresCharacterLogout: false },
            "Invalid damage marker did not use the shared crack-error counter policy.");

        var codec = LegacyFrameCodec.CreateDefault();
        var payload = new byte[AttackRequest.AreaPacketSize - PacketHeader.SizeInBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset), 42);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(AttackRequest.DamageArrayOffset + 4), 75);
        var input = codec.Decode(codec.Encode(AttackRequest.AreaMessageType, 42, 1_000, payload, 0));
        Check(AttackConfirmation.TryCreate(input, new byte[816], out var confirmation) && confirmation is not null,
            "Invalid marker fixture could not create an authoritative attack response.");
        Check(confirmation!.TrySetDamageSlot(0, 42, 0), "Rejected marker could not be normalized in the response.");
        var response = codec.Decode(confirmation.ToFrame(codec, 1_000, 0));
        Check(AttackRequest.TryReadDamageSlot(response, 0, out var targetId, out var damage) &&
            targetId == 42 && damage == 0,
            "Rejected marker relay must preserve TargetId with zero damage.");
        return Task.CompletedTask;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
