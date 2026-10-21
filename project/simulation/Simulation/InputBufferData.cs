using System.Runtime.CompilerServices;
using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// The input buffer of one fighter: for each button, the number of frames that its last press stays valid for an
/// <see cref="ConditionType.InputPressed"/> condition. 0 = no valid press (also the default value).
/// </summary>
/// <remarks>
/// Without a buffer, a press during a busy state (for example a landing or the end of an attack) is lost: on the first
/// frame where the state accepts the button, the button is only held, not pressed. With the buffer, the press stays
/// valid for <see cref="MatchRulesData.InputBufferFrames"/> more frames.
/// <list type="bullet">
/// <item><see cref="Record"/>: at the start of each tick, a press (held now, not held on the last frame) makes the button
/// valid for the frame of the press and the buffer frames after it.</item>
/// <item><see cref="Advance"/>: at the end of each tick, one frame less. Not during hitstop, so a press during hitstop
/// is still valid after it.</item>
/// <item><see cref="Consume"/>: a transition that fires uses its pressed buttons, so one press gives only one action
/// (for example a jump, and not a jump and then a double jump).</item>
/// </list>
/// Element i is the button with bit i in <see cref="InputFlags"/>.
/// </remarks>
[InlineArray(InputFlagsExtensions.ButtonCount)]
public struct InputBufferData
{
    private const int ButtonCount = InputFlagsExtensions.ButtonCount;

    private byte _element0;

    /// <summary>Makes each pressed button valid for this frame and <paramref name="bufferFrames"/> more frames.</summary>
    public void Record(InputFlags pressed, int bufferFrames)
    {
        byte frames = (byte)(bufferFrames + 1);
        for (int i = 0; i < ButtonCount; i++)
        {
            if (pressed.Has(ButtonAt(i)))
            {
                this[i] = frames;
            }
        }
    }

    /// <summary>One frame passed: each valid press has one frame less.</summary>
    public void Advance()
    {
        for (int i = 0; i < ButtonCount; i++)
        {
            if (this[i] > 0)
            {
                this[i]--;
            }
        }
    }

    /// <summary>True if each of the buttons has a valid press.</summary>
    public readonly bool Contains(InputFlags buttons)
    {
        for (int i = 0; i < ButtonCount; i++)
        {
            if (buttons.Has(ButtonAt(i)) && this[i] == 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Removes the presses of the buttons: they cannot be used again.</summary>
    public void Consume(InputFlags buttons)
    {
        for (int i = 0; i < ButtonCount; i++)
        {
            if (buttons.Has(ButtonAt(i)))
            {
                this[i] = 0;
            }
        }
    }

    public readonly void Hash(ref StateHasher hasher)
    {
        for (int i = 0; i < ButtonCount; i++)
        {
            hasher.Add(this[i]);
        }
    }

    private static InputFlags ButtonAt(int index) => (InputFlags)(1 << index);
}
