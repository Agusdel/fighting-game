using System.Collections.Generic;
using Godot;

namespace FightingGame.Authoring;

/// <summary>
/// Root node of a stage scene. <see cref="StageConverter"/> converts the authoring nodes below it into stage data.
/// In the editor, the scene tree shows the conversion errors as warnings on this node.
/// </summary>
[Tool]
[GlobalClass]
public partial class StageRoot : StageNode
{
    public override string[] _GetConfigurationWarnings()
    {
        List<string> errors = StageConverter.TryConvert(this, out _);
        return errors.ToArray();
    }
}
