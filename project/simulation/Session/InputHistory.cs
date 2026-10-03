using System;
using FightingGame.Simulation;

namespace FightingGame.Session;

/// <summary>
/// Keeps the inputs of all players for the last <see cref="Capacity"/> frames, so a session can run old frames again.
/// </summary>
/// <remarks>
/// It is a ring buffer keyed by frame number, with the same rules as <see cref="SnapshotBuffer"/>: frame F uses
/// entry F % Capacity, and a frame that is not stored throws (a session bug).
/// The input of frame F is the input that the tick from frame F to frame F + 1 uses.
/// </remarks>
public sealed class InputHistory
{
    private readonly int[] _frames;
    private readonly FrameInput[] _inputs;

    public InputHistory(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity must be at least 1.");
        }
        _frames = new int[capacity];
        _inputs = new FrameInput[capacity];
        Clear();
    }

    public int Capacity => _frames.Length;

    public void Set(int frame, in FrameInput input)
    {
        if (frame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), $"Frame {frame} is negative.");
        }
        int index = frame % Capacity;
        _frames[index] = frame;
        _inputs[index] = input;
    }

    public bool Contains(int frame) => frame >= 0 && _frames[frame % Capacity] == frame;

    public ref readonly FrameInput Get(int frame)
    {
        if (!Contains(frame))
        {
            throw new InvalidOperationException($"Frame {frame} is not in the input history.");
        }
        return ref _inputs[frame % Capacity];
    }

    public void Clear()
    {
        Array.Fill(_frames, -1);
        Array.Clear(_inputs);
    }
}
