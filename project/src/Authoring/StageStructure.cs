using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// Groups the parts of one stage object (for example a platform or a wall): one or more collision components
/// (<see cref="StageCollisionBox"/>) and any number of visual nodes (for example <c>Sprite2D</c>).
/// Visual nodes have no rules: they do not affect the simulation.
/// </summary>
[Tool]
[GlobalClass]
public partial class StageStructure : StageNode
{
}
