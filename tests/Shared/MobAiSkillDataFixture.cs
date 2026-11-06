using WydCdk.World;

internal static class MobAiSkillDataFixture
{
    // InstanceType by skill ID from TMProject 7.69 Servidor/Server/Common/SkillData.csv.
    // The test fixture keeps only the field GetAttack reads; skill names and other data
    // remain owned by the production SkillData loader and its integration tests.
    private static readonly byte[] InstanceTypes =
    [
        4, 0, 1, 0, 4, 0, 0, 3, 1, 0, 1, 0, 1, 0, 0, 0,
        0, 2, 1, 3, 1, 1, 2, 3, 4, 8, 7, 6, 4, 6, 4, 8,
        2, 5, 3, 2, 3, 0, 2, 2, 4, 0, 9, 0, 0, 0, 0, 0,
        2, 12, 4, 0, 4, 0, 0, 4, 11, 11, 11, 11, 11, 11, 11, 11,
        0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1,
        1, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0,
        0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0
    ];

    public static LegacySkillDataTable Create()
    {
        var text = string.Join(Environment.NewLine, InstanceTypes.Select(static (instanceType, id) =>
            $"{id},0,0,0,0,0,{instanceType},0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,Skill{id}"));
        return LegacySkillDataTable.Load(new StringReader(text));
    }
}
