using System;
using FightingGame.Core;

namespace FightingGame.Session;

/// <summary>
/// Measures how many frames the local peer runs ahead of the remote peers, and tells the session when to wait one frame.
/// If one peer runs ahead (for example its clock is a little faster), the other peers must predict more and roll back
/// more often. The peer that is ahead waits, so all peers stay at about the same frame.
/// </summary>
/// <remarks>
/// No clock is needed. For each received input message: local advantage = local frame - sender frame. This value
/// includes the one-way latency. The sender measures the same value on its side and sends it in the message
/// (remote advantage). (local advantage - remote advantage) / 2 = the frames that this peer is ahead: the latency
/// cancels out if it is the same in both directions. Both values are averaged over the last <see cref="WindowSize"/>
/// messages of each peer. With more than one remote peer, the peer that is most behind counts.
/// </remarks>
internal sealed class TimeSync
{
    public const int WindowSize = 32;

    /// <summary>Samples needed from a peer before its values count (fewer samples are too noisy).</summary>
    public const int MinSamples = 8;

    /// <summary>Frames run between two waits. Small, spread steps are not visible as a freeze.</summary>
    public const int MinFramesBetweenWaits = 10;

    /// <summary>The last values of one peer, and their sum.</summary>
    private sealed class Window
    {
        private readonly int[] _values = new int[WindowSize];
        private int _next;

        public int Count { get; private set; }
        public long Sum { get; private set; }

        public void Add(int value)
        {
            if (Count == WindowSize)
            {
                Sum -= _values[_next];
            }
            else
            {
                Count++;
            }
            _values[_next] = value;
            Sum += value;
            _next = (_next + 1) % WindowSize;
        }
    }

    private readonly Window[] _localAdvantages;
    private readonly Window[] _remoteAdvantages;
    private readonly int[] _lastLocalAdvantages;
    private int _framesSinceWait = MinFramesBetweenWaits;

    public TimeSync(int remotePeerCount)
    {
        _localAdvantages = new Window[remotePeerCount];
        _remoteAdvantages = new Window[remotePeerCount];
        _lastLocalAdvantages = new int[remotePeerCount];
        for (int i = 0; i < remotePeerCount; i++)
        {
            _localAdvantages[i] = new Window();
            _remoteAdvantages[i] = new Window();
        }
    }

    /// <summary>The last local advantage over the peer. The session sends it to that peer.</summary>
    public int LocalAdvantageOf(int peer) => _lastLocalAdvantages[peer];

    public void OnInputMessage(int peer, int localFrame, int senderFrame, int senderAdvantage)
    {
        int localAdvantage = localFrame - senderFrame;
        _lastLocalAdvantages[peer] = localAdvantage;
        _localAdvantages[peer].Add(localAdvantage);
        _remoteAdvantages[peer].Add(senderAdvantage);
    }

    /// <summary>The frames that the local peer is ahead of the peer that is most behind. 0 until enough messages arrived.</summary>
    public Fixed FramesAhead
    {
        get
        {
            Fixed ahead = Fixed.Zero;
            for (int peer = 0; peer < _localAdvantages.Length; peer++)
            {
                if (TryGetDifference(peer, out long numerator, out long denominator))
                {
                    ahead = Fixed.Max(ahead, Fixed.FromRatio(numerator, 2 * denominator));
                }
            }
            return ahead;
        }
    }

    /// <summary>True if the local peer is 1 frame or more ahead, and the last wait was long enough ago.</summary>
    public bool ShouldWait()
    {
        if (_framesSinceWait < MinFramesBetweenWaits)
        {
            return false;
        }
        for (int peer = 0; peer < _localAdvantages.Length; peer++)
        {
            // (local average - remote average) / 2 >= 1, in integers.
            if (TryGetDifference(peer, out long numerator, out long denominator) && numerator >= 2 * denominator)
            {
                return true;
            }
        }
        return false;
    }

    public void OnFrameRun() => _framesSinceWait++;

    public void OnWait() => _framesSinceWait = 0;

    /// <summary>local average - remote average = numerator / denominator (both averages with integer sums).</summary>
    private bool TryGetDifference(int peer, out long numerator, out long denominator)
    {
        Window local = _localAdvantages[peer];
        Window remote = _remoteAdvantages[peer];
        if (local.Count < MinSamples || remote.Count < MinSamples)
        {
            numerator = 0;
            denominator = 1;
            return false;
        }
        numerator = local.Sum * remote.Count - remote.Sum * local.Count;
        denominator = (long)local.Count * remote.Count;
        return true;
    }
}
