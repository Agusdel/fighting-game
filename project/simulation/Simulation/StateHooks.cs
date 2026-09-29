namespace FightingGame.Simulation;

/// <summary>
/// Code that runs when a state starts, on each frame of a state, or when a state ends.
/// </summary>
/// <remarks>
/// A hook must keep the simulation deterministic:
/// <list type="bullet">
/// <item>It is a static method. It captures no variables.</item>
/// <item>It reads and writes only the <see cref="FighterData"/> that it receives, and reads only static data.</item>
/// <item>It uses only fixed-point math (no float, no double).</item>
/// </list>
/// For values that must live between frames, use <see cref="FighterData.StateVar0"/> to <see cref="FighterData.StateVar3"/>.
/// </remarks>
public delegate void StateHook(ref FighterData fighter, in StateContext context);

/// <summary>A custom transition condition. The same rules as <see cref="StateHook"/> apply, but it must not change anything.</summary>
public delegate bool StateCondition(in FighterData fighter, in StateContext context);

/// <summary>What a state, a hook, or a condition can read on one frame, in addition to the fighter data.</summary>
public readonly struct StateContext
{
    /// <summary>The buttons held on this frame.</summary>
    public InputFlags Input { get; }

    /// <summary>The buttons pressed on this frame (held now, not held on the last frame).</summary>
    public InputFlags Pressed { get; }

    /// <summary>The buttons released on this frame (held on the last frame, not held now).</summary>
    public InputFlags Released { get; }

    public FighterDefinitionData Definition { get; }
    public StageData Stage { get; }

    public StateContext(InputFlags input, InputFlags previousInput, FighterDefinitionData definition, StageData stage)
    {
        Input = input;
        Pressed = input & ~previousInput;
        Released = previousInput & ~input;
        Definition = definition;
        Stage = stage;
    }

    public FighterStats Stats => Definition.Stats;
}
