using FightingGame.Core;
using FightingGame.Simulation;

namespace FightingGame.Simulation.Tests.Simulation;

/// <summary>A match with the default game data, and helpers to run ticks in tests.</summary>
internal sealed class TestWorld
{
    public readonly GameData Data = DefaultGameData.Create();
    public WorldState State;

    public TestWorld(int playerCount = 1, ulong seed = 1)
    {
        State = WorldState.Create(Data, playerCount, seed);
    }

    public ref FighterState Fighter(int slot = 0) => ref State.Fighters[slot];

    /// <summary>Runs <paramref name="frames"/> ticks with the same input for player 0 and no input for the others.</summary>
    public void Run(int frames, InputFlags player0 = InputFlags.None)
    {
        for (int i = 0; i < frames; i++)
        {
            Tick(player0);
        }
    }

    public void Tick(InputFlags player0 = InputFlags.None, InputFlags player1 = InputFlags.None)
    {
        FrameInput input = default;
        input[0] = player0;
        input[1] = player1;
        Simulator.Tick(ref State, input, Data);
    }

    /// <summary>Runs ticks until player 0 is grounded. Returns the number of ticks.</summary>
    public int RunUntilGrounded(int maxFrames = 600)
    {
        for (int i = 1; i <= maxFrames; i++)
        {
            Tick();
            if (Fighter().Grounded)
            {
                return i;
            }
        }
        throw new System.InvalidOperationException("The fighter did not land.");
    }

    /// <summary>Places player 0 in the air at <paramref name="feet"/> with the given velocity.</summary>
    public void PlaceInAir(FixedVector2 feet, FixedVector2 velocity)
    {
        ref FighterState fighter = ref Fighter();
        fighter.Position = feet;
        fighter.Velocity = velocity;
        fighter.Grounded = false;
        fighter.SetAction(FighterAction.Airborne);
        fighter.JumpsLeft = 1;
    }

    /// <summary>Places player 0 so that it stands on the platform, and runs one tick to settle.</summary>
    public void PlaceOnPlatform()
    {
        FixedAABB platform = Data.Stage.Platforms[0];
        PlaceInAir(new FixedVector2(576, platform.Min.Y - 1), FixedVector2.Zero);
        RunUntilGrounded();
        Run(Data.Fighter.LandFrames + 1);
    }
}
