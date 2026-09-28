using System;
using System.Collections.Generic;
using FightingGame.Core;
using FightingGame.Simulation;
using Godot;
using SimSpawnPositionPair = FightingGame.Simulation.SpawnPositionPair;

namespace FightingGame.Authoring;

/// <summary>
/// Converts the authoring nodes of a stage scene into <see cref="StageData"/> with exact fixed-point values.
/// </summary>
/// <remarks>
/// Determinism rules:
/// <list type="bullet">
/// <item>All authored values must be whole pixels. Godot stores positions as 32-bit floats. A float holds every whole
/// number exactly (up to 16 million), but most fractions not. So a whole number gives the same fixed-point value on
/// every machine.</item>
/// <item>A position is the sum of the local positions from the node up to the <see cref="StageRoot"/>, in
/// <see cref="int"/> math. <c>GlobalPosition</c> is not used, because it comes from float matrix math.</item>
/// <item>Every node in that chain must be a <see cref="Node2D"/> with whole-pixel position, no rotation,
/// scale (1, 1), and no skew. Visual nodes outside the chain (for example sprites) have no rules.</item>
/// <item>The tree is walked depth-first in child order. The child order is saved in the scene file, so the arrays
/// have the same order on every machine.</item>
/// </list>
/// The converter does not trust the editor snapping: a scene file can be changed by hand. It collects all errors,
/// so the user sees every problem at once.
/// </remarks>
public static class StageConverter
{
    /// <summary>A float must be closer than this to a whole number.</summary>
    private const float IntegerTolerance = 0.001f;

    /// <summary>Converts the stage. Throws <see cref="StageConversionException"/> with all errors if the stage is not valid.</summary>
    public static StageData Convert(StageRoot root)
    {
        List<string> errors = TryConvert(root, out StageData? data);
        if (data == null)
        {
            throw new StageConversionException(root.SceneFilePath, errors);
        }
        return data;
    }

    /// <summary>Converts the stage. Returns the list of errors (empty on success). <paramref name="data"/> is null if there are errors.</summary>
    public static List<string> TryConvert(StageRoot root, out StageData? data)
    {
        var context = new Context(root);
        context.Walk(root);
        context.CheckStructure();

        data = context.Errors.Count == 0
            ? new StageData(
                context.Solids.ToArray(),
                context.Platforms.ToArray(),
                context.Singles.ToArray(),
                context.Pairs.ToArray())
            : null;
        return context.Errors;
    }

    private sealed class Context
    {
        private readonly StageRoot _root;

        public readonly List<string> Errors = new();
        public readonly List<FixedAABB> Solids = new();
        public readonly List<FixedAABB> Platforms = new();
        public readonly List<FixedVector2> Singles = new();
        public readonly List<SimSpawnPositionPair> Pairs = new();

        public Context(StageRoot root)
        {
            _root = root;
        }

        public void Walk(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                switch (child)
                {
                    case StageCollisionBox box:
                        AddBox(box);
                        break;
                    case SpawnPositionPair pair:
                        AddPair(pair);
                        break;
                    case SpawnPosition position when !position.IsInPair:
                        if (TryGetPosition(position, out Vector2I single))
                        {
                            Singles.Add(ToFixed(single));
                        }
                        break;
                }
                Walk(child);
            }
        }

        public void CheckStructure()
        {
            if (Solids.Count == 0)
            {
                Errors.Add("The stage needs at least one StageCollisionBox with Type = Solid.");
            }
            if (Singles.Count == 0)
            {
                Errors.Add("The stage needs at least one single SpawnPosition (a SpawnPosition that is not in a SpawnPositionPair).");
            }
            if (Pairs.Count < StageData.MinSpawnPositionPairs)
            {
                Errors.Add($"The stage needs at least {StageData.MinSpawnPositionPairs} SpawnPositionPair nodes, but it has {Pairs.Count}.");
            }
        }

        private void AddBox(StageCollisionBox box)
        {
            if (!TryGetPosition(box, out Vector2I min))
            {
                return;
            }
            if (box.Size.X < 1 || box.Size.Y < 1)
            {
                Errors.Add($"{PathOf(box)}: Size must be at least 1 x 1.");
                return;
            }

            FixedVector2 fixedMin = ToFixed(min);
            var aabb = new FixedAABB(fixedMin, fixedMin + ToFixed(box.Size));
            (box.Type == StageCollisionType.Solid ? Solids : Platforms).Add(aabb);
        }

        private void AddPair(SpawnPositionPair pair)
        {
            List<SpawnPosition> positions = pair.GetSpawnPositions();
            if (positions.Count != 2)
            {
                Errors.Add($"{PathOf(pair)}: a SpawnPositionPair needs exactly 2 SpawnPosition children, but it has {positions.Count}.");
                return;
            }
            // '&' (not '&&'): check both positions, so both report their errors.
            if (TryGetPosition(positions[0], out Vector2I a) & TryGetPosition(positions[1], out Vector2I b))
            {
                Pairs.Add(new SimSpawnPositionPair(ToFixed(a), ToFixed(b)));
            }
        }

        /// <summary>
        /// Adds the local positions from <paramref name="node"/> up to the root (the root itself is not included).
        /// Checks every node in the chain. Returns false (and adds errors) if a node is not valid.
        /// </summary>
        private bool TryGetPosition(Node2D node, out Vector2I position)
        {
            position = Vector2I.Zero;
            bool valid = true;
            for (Node current = node; current != _root; current = current.GetParent())
            {
                if (current is not Node2D node2D)
                {
                    ReportOnce(current, "must be a Node2D (a stage authoring node cannot be below a node of another type).");
                    return false;
                }
                valid &= CheckTransform(node2D, out Vector2I local);
                position += local;
            }
            return valid;
        }

        private bool CheckTransform(Node2D node, out Vector2I local)
        {
            bool valid = true;
            valid &= TryToInt(node, "Position.X", node.Position.X, out int x);
            valid &= TryToInt(node, "Position.Y", node.Position.Y, out int y);
            local = new Vector2I(x, y);

            if (node.Rotation != 0)
            {
                ReportOnce(node, "Rotation must be 0.");
                valid = false;
            }
            if (node.Scale != Vector2.One)
            {
                ReportOnce(node, "Scale must be (1, 1).");
                valid = false;
            }
            if (node.Skew != 0)
            {
                ReportOnce(node, "Skew must be 0.");
                valid = false;
            }
            return valid;
        }

        private bool TryToInt(Node node, string name, float value, out int result)
        {
            result = Mathf.RoundToInt(value);
            if (Math.Abs(value - result) > IntegerTolerance)
            {
                ReportOnce(node, $"{name} = {value} is not a whole number of pixels.");
                return false;
            }
            return true;
        }

        /// <summary>A parent node is in the chain of many children. Report each problem of a node only once.</summary>
        private void ReportOnce(Node node, string message)
        {
            string error = $"{PathOf(node)}: {message}";
            if (!Errors.Contains(error))
            {
                Errors.Add(error);
            }
        }

        private string PathOf(Node node) => _root.GetPathTo(node).ToString();

        private static FixedVector2 ToFixed(Vector2I value) => new(Fixed.FromInt(value.X), Fixed.FromInt(value.Y));
    }
}

/// <summary>The stage scene is not valid. <see cref="Errors"/> lists every problem, with the node path.</summary>
public sealed class StageConversionException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public StageConversionException(string scenePath, IReadOnlyList<string> errors)
        : base($"The stage '{scenePath}' is not valid:\n  " + string.Join("\n  ", errors))
    {
        Errors = errors;
    }
}
