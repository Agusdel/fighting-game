using System;
using FightingGame.Networking;

namespace FightingGame.Session;

/// <summary>
/// A remote peer reported a different hash for a confirmed frame. Confirmed frames never roll back, so the peers
/// have different states: a determinism bug, different game data, or a cheat.
/// </summary>
public sealed class DesyncException : Exception
{
    public DesyncException(PeerId peer, int frame, ulong localHash, ulong remoteHash)
        : base($"Desync with {peer} at frame {frame}: local hash {localHash:X16}, remote hash {remoteHash:X16}.")
    {
        Peer = peer;
        Frame = frame;
        LocalHash = localHash;
        RemoteHash = remoteHash;
    }

    public PeerId Peer { get; }
    public int Frame { get; }
    public ulong LocalHash { get; }
    public ulong RemoteHash { get; }
}

/// <summary>
/// Compares the hashes of confirmed frames with the remote peers. Each peer records the hash of each confirmed frame
/// that is a multiple of the hash interval (a report), sends its newest report on each tick, and compares the
/// reports that it receives with its own.
/// </summary>
/// <remarks>
/// The newest report is sent again on each tick (unreliable) until a newer one exists, so a lost message does no harm
/// and no acknowledgment is needed. A remote report for a frame that is not confirmed here yet waits until it is.
/// A report older than the kept history (<see cref="HistorySize"/> reports) is not compared.
/// </remarks>
internal sealed class DesyncDetector
{
    public const int HistorySize = 64;

    private readonly int _interval;
    private readonly PeerId[] _peers;
    private readonly RollbackSessionStats _stats;

    /// <summary>The own reports, at index (frame / interval) % <see cref="HistorySize"/>. Frame -1 = empty.</summary>
    private readonly int[] _reportFrames = new int[HistorySize];
    private readonly ulong[] _reportHashes = new ulong[HistorySize];
    private int _lastReportFrame = -1;

    /// <summary>For each remote peer: the newest report that is not compared yet (frame -1 = none).</summary>
    private readonly HashMessage[] _waitingReports;

    /// <summary>For each remote peer: the newest report that was compared (the peer sends each report many times).</summary>
    private readonly int[] _lastComparedFrames;

    public DesyncDetector(int interval, PeerId[] peers, RollbackSessionStats stats)
    {
        _interval = interval;
        _peers = peers;
        _stats = stats;
        Array.Fill(_reportFrames, -1);
        _waitingReports = new HashMessage[peers.Length];
        Array.Fill(_waitingReports, new HashMessage { Frame = -1 });
        _lastComparedFrames = new int[peers.Length];
        Array.Fill(_lastComparedFrames, -1);
    }

    /// <summary>The first desync found, or null.</summary>
    public DesyncException? Desync { get; private set; }

    /// <summary>The newest own report, or null before the first one.</summary>
    public HashMessage? LatestReport =>
        _lastReportFrame < 0 ? null : new HashMessage { Frame = _lastReportFrame, Hash = HashOf(_lastReportFrame) };

    /// <summary>Records a report for each multiple of the interval up to <paramref name="confirmedFrame"/>.</summary>
    public void OnConfirmed(int confirmedFrame, SnapshotBuffer snapshots)
    {
        int next = _lastReportFrame < 0 ? 0 : _lastReportFrame + _interval;
        for (int frame = next; frame <= confirmedFrame; frame += _interval)
        {
            int index = frame / _interval % HistorySize;
            _reportFrames[index] = frame;
            _reportHashes[index] = snapshots.HashOf(frame);
            _lastReportFrame = frame;
        }

        for (int peer = 0; peer < _peers.Length; peer++)
        {
            HashMessage waiting = _waitingReports[peer];
            if (waiting.Frame >= 0 && waiting.Frame <= _lastReportFrame)
            {
                _waitingReports[peer].Frame = -1;
                Compare(peer, waiting);
            }
        }
    }

    public void OnRemoteReport(int peer, HashMessage report)
    {
        if (report.Frame % _interval != 0)
        {
            _stats.InvalidMessages++;
            return;
        }
        if (report.Frame <= _lastComparedFrames[peer])
        {
            return;
        }
        if (report.Frame <= _lastReportFrame)
        {
            Compare(peer, report);
        }
        else if (report.Frame > _waitingReports[peer].Frame)
        {
            _waitingReports[peer] = report;
        }
    }

    private void Compare(int peer, HashMessage report)
    {
        int index = report.Frame / _interval % HistorySize;
        if (_reportFrames[index] != report.Frame)
        {
            return;   // Too old: not in the history any more.
        }

        _lastComparedFrames[peer] = Math.Max(_lastComparedFrames[peer], report.Frame);
        _stats.HashesCompared++;
        if (_reportHashes[index] != report.Hash && Desync == null)
        {
            Desync = new DesyncException(_peers[peer], report.Frame, _reportHashes[index], report.Hash);
        }
    }

    private ulong HashOf(int frame) => _reportHashes[frame / _interval % HistorySize];
}
