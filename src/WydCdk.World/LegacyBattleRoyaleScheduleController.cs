namespace WydCdk.World;

public sealed class LegacyBattleRoyaleScheduleController
{
    private readonly LegacyBattleRoyaleConfiguration configuration;
    private readonly LegacyBattleRoyaleSchedule schedule;
    private readonly WorldHub world;

    public LegacyBattleRoyaleScheduleController(
        LegacyBattleRoyaleConfiguration configuration,
        WorldHub world)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(world);
        this.configuration = configuration;
        schedule = new LegacyBattleRoyaleSchedule(configuration);
        this.world = world;
    }

    public LegacyBattleRoyaleTransition? Tick(DateTime localNow)
    {
        // Server.cpp:7930-7935 sets BrGrid = tm_min / 20 before the BRHour/state checks of an eligible server.
        if (configuration.IsEligible(localNow))
            world.SetBattleRoyaleGrid(localNow.Minute / 20);

        var transition = schedule.Tick(localNow);
        var publishesAfterDoor = transition is { } doorTransition &&
            (doorTransition.PreviousState, doorTransition.State) is (1, 2) or (3, 0);
        if (transition is not null && (transition.PreviousState != 2 || transition.State != 3) && !publishesAfterDoor)
            world.SetBattleRoyaleState(transition.State, transition.BRItem);

        return transition;
    }

    /// <summary>Publishes the 1-to-2 or 3-to-0 state after its 7.69 SetColoseumDoor call.</summary>
    public void PublishDoorTransition(LegacyBattleRoyaleTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if ((transition.PreviousState, transition.State) is not ((1, 2) or (3, 0)) ||
            !transition.DoorCalls.Any(static call => call.Timing == LegacyBattleRoyaleDoorCallTiming.BeforeNotice))
            throw new ArgumentException("Only Battle Royale transitions preceded by a legacy door call can be published here.", nameof(transition));

        world.SetBattleRoyaleState(transition.State, transition.BRItem);
    }

    /// <summary>Publishes state 3 only after the Host has attempted the legacy altar item creation.</summary>
    public void PublishRewardTransition(LegacyBattleRoyaleTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.PreviousState != 2 || transition.State != 3)
            throw new ArgumentException("Only the Battle Royale 2-to-3 reward transition can be published here.", nameof(transition));

        world.SetBattleRoyaleState(transition.State, transition.BRItem);
    }
}
