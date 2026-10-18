using System;
using System.Collections.Generic;
using FightingGame.Core;
using Godot;

namespace FightingGame.Presentation;

/// <summary>
/// Shows the pose of a fighter state at a state frame: the animation with the state name, seeked to the frame. Used by
/// the game view and by the debug tools, so they show the same pose.
/// </summary>
/// <remarks>
/// The <c>AnimationPlayer</c> never runs on its own clock (manual process mode, see <see cref="UseManualProcess"/>).
/// A looping animation repeats; a non-looping one holds its last frame. A state with no animation shows the "RESET" pose.
/// A looping animation with a "loop_start" marker plays the part before the marker once (an intro), then repeats only
/// the part from the marker to the end (for example a fall: tuck once, then a small loop). Key the loop end with the
/// same values as the marker frame, so the loop has no jump.
/// </remarks>
public static class FighterPose
{
    public const string ResetAnimation = "RESET";

    /// <summary>The marker where the repeated part of a looping animation starts (see the remarks).</summary>
    public const string LoopStartMarker = "loop_start";

    /// <summary>The animation player of a fighter scene instance (a direct child of the root), or null.</summary>
    public static AnimationPlayer? FindPlayer(Node fighter)
    {
        foreach (Node child in fighter.GetChildren())
        {
            if (child is AnimationPlayer player)
            {
                return player;
            }
        }
        return null;
    }

    /// <summary>The player changes the pose only when <see cref="Show"/> seeks it.</summary>
    public static void UseManualProcess(AnimationPlayer player) =>
        player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;

    /// <summary>Shows the pose. Returns the animation used ("" if none: the pose does not change).</summary>
    public static string Show(AnimationPlayer player, string stateName, int stateFrame)
    {
        string name = player.HasAnimation(stateName) ? stateName
            : player.HasAnimation(ResetAnimation) ? ResetAnimation
            : "";
        if (name.Length == 0)
        {
            return "";
        }

        Animation animation = player.GetAnimation(name);
        double time = (double)stateFrame / GameConstants.TickRate;
        time = animation.LoopMode != Animation.LoopModeEnum.None && animation.Length > 0
            ? LoopTime(animation, time)
            : Math.Min(time, animation.Length);
        if (player.CurrentAnimation != name)
        {
            player.Play(name);
        }
        player.Seek(time, update: true);
        return name;
    }

    /// <summary>The time in a looping animation: the intro once (before the loop start marker), then the loop.</summary>
    private static double LoopTime(Animation animation, double time)
    {
        double loopStart = animation.HasMarker(LoopStartMarker) ? animation.GetMarkerTime(LoopStartMarker) : 0;
        if (time < loopStart || loopStart >= animation.Length)
        {
            return Math.Min(time, animation.Length);
        }
        return loopStart + (time - loopStart) % (animation.Length - loopStart);
    }

    /// <summary>The length of an animation in frames.</summary>
    public static int FrameCount(Animation animation) => (int)Math.Round(animation.Length * GameConstants.TickRate);

    /// <summary>The frames that have a key in any track (rounded to whole frames).</summary>
    public static HashSet<int> KeyFrames(Animation animation)
    {
        var frames = new HashSet<int>();
        for (int track = 0; track < animation.GetTrackCount(); track++)
        {
            for (int key = 0; key < animation.TrackGetKeyCount(track); key++)
            {
                frames.Add((int)Math.Round(animation.TrackGetKeyTime(track, key) * GameConstants.TickRate));
            }
        }
        return frames;
    }
}
