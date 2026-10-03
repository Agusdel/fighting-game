using System;
using FightingGame.Simulation;

namespace FightingGame.Session;

/// <summary>
/// Keeps the world state and its hash for the last <see cref="Capacity"/> frames, so a session can roll back.
/// </summary>
/// <remarks>
/// It is a ring buffer keyed by frame number: frame F uses entry F % Capacity, so saving frame F overwrites
/// frame F - Capacity. A session must never ask for a frame that is not stored: that is always a session bug,
/// so it throws.
/// </remarks>
public sealed class SnapshotBuffer
{
    private struct Entry
    {
        /// <summary>The frame stored in this entry, or -1 if the entry is empty.</summary>
        public int Frame;
        public WorldData World;
        public ulong Hash;
    }

    private readonly Entry[] _entries;

    public SnapshotBuffer(int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity must be at least 1.");
        }
        _entries = new Entry[capacity];
        Clear();
    }

    public int Capacity => _entries.Length;

    /// <summary>Stores a copy of the state under <see cref="WorldData.Frame"/>, and returns its hash.</summary>
    public ulong Save(in WorldData world)
    {
        if (world.Frame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(world), $"Frame {world.Frame} is negative.");
        }
        ref Entry entry = ref _entries[world.Frame % Capacity];
        entry.Frame = world.Frame;
        entry.World = world;
        entry.Hash = world.ComputeHash();
        return entry.Hash;
    }

    public bool Contains(int frame) => frame >= 0 && _entries[frame % Capacity].Frame == frame;

    /// <summary>The stored state of the frame. Copy it before you change it.</summary>
    public ref readonly WorldData Get(int frame) => ref EntryOf(frame).World;

    public ulong HashOf(int frame) => EntryOf(frame).Hash;

    public void Clear()
    {
        for (int i = 0; i < _entries.Length; i++)
        {
            _entries[i] = new Entry { Frame = -1 };
        }
    }

    private ref Entry EntryOf(int frame)
    {
        if (!Contains(frame))
        {
            throw new InvalidOperationException($"Frame {frame} is not in the snapshot buffer.");
        }
        return ref _entries[frame % Capacity];
    }
}
