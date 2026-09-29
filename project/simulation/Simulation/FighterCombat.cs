using System;
using FightingGame.Core;

namespace FightingGame.Simulation;

/// <summary>
/// Hit detection and hit resolution between fighters.
/// </summary>
/// <remarks>
/// Fairness: the detection only reads the fighters and writes the results into local arrays. Then the resolution
/// applies all hits at the same time. The results of several hits on one fighter are combined with operations that
/// do not depend on the order (sum of damage, sum of knockback, maximum hitstun and hitstop, bitwise OR of the hit
/// registry). So the player slot order cannot change the result, and two fighters can hit each other on the same
/// frame (a trade).
/// </remarks>
public static class FighterCombat
{
    public static void DetectAndResolveHits(ref WorldData world, in FrameInput input, GameData data)
    {
        const int Count = GameConstants.MaxPlayers;
        FighterDefinitionData definition = data.FighterDefinition;
        bool hitstopEnabled = data.Rules.HitstopEnabled;

        Span<bool> wasHit = stackalloc bool[Count];
        Span<int> damage = stackalloc int[Count];
        Span<FixedVector2> knockback = stackalloc FixedVector2[Count];
        Span<int> hitstun = stackalloc int[Count];
        Span<int> hitstop = stackalloc int[Count];
        Span<byte> newTargets = stackalloc byte[Count];

        // Detection: read only.
        for (int attacker = 0; attacker < Count; attacker++)
        {
            for (int target = 0; target < Count; target++)
            {
                if (attacker == target ||
                    !TryFindHit(world.Fighters[attacker], world.Fighters[target], target, definition, out HitboxData hitbox))
                {
                    continue;
                }

                sbyte facing = world.Fighters[attacker].Facing;
                wasHit[target] = true;
                damage[target] += hitbox.Damage;
                knockback[target] += new FixedVector2(hitbox.Knockback.X * facing, hitbox.Knockback.Y);
                hitstun[target] = Math.Max(hitstun[target], hitbox.HitstunFrames);
                newTargets[attacker] |= (byte)(1 << target);
                if (hitstopEnabled)
                {
                    hitstop[attacker] = Math.Max(hitstop[attacker], hitbox.HitstopFrames);
                    hitstop[target] = Math.Max(hitstop[target], hitbox.HitstopFrames);
                }
            }
        }

        // Resolution: apply all results.
        for (int i = 0; i < Count; i++)
        {
            ref FighterData fighter = ref world.Fighters[i];
            fighter.HitTargets |= newTargets[i];
            fighter.HitstopFrames = Math.Max(fighter.HitstopFrames, hitstop[i]);

            if (!wasHit[i])
            {
                continue;
            }

            fighter.Health = Math.Max(0, fighter.Health - damage[i]);

            FighterStateData state = definition.States[fighter.StateId];
            if (state.Has(StateFlags.ArmoredAgainstHits) || definition.HitstunState == FighterStateData.NoState)
            {
                continue;
            }

            fighter.Velocity = knockback[i];
            FighterStateMachine.Enter(ref fighter, definition.HitstunState, Simulator.CreateContext(fighter, input[i], data));
            fighter.HitstunFrames = hitstun[i];
        }
    }

    /// <summary>
    /// Finds the first active hitbox of the attacker that touches an active hurtbox of the target.
    /// A target that the current attack already hit cannot be hit again.
    /// </summary>
    private static bool TryFindHit(in FighterData attacker, in FighterData target, int targetSlot,
        FighterDefinitionData definition, out HitboxData hit)
    {
        hit = default;
        if (!attacker.Active || !target.Active || (attacker.HitTargets & (1 << targetSlot)) != 0)
        {
            return false;
        }

        FighterStateData attackerState = definition.States[attacker.StateId];
        if (attackerState.Hitboxes.Length == 0)
        {
            return false;
        }

        HurtboxData[] hurtboxes = definition.HurtboxesOf(definition.States[target.StateId]);
        foreach (HitboxData hitbox in attackerState.Hitboxes)
        {
            if (!hitbox.IsActive(attacker.StateFrame))
            {
                continue;
            }

            FixedAABB hitArea = ToWorld(hitbox.Box, attacker);
            foreach (HurtboxData hurtbox in hurtboxes)
            {
                if (hurtbox.IsActive(target.StateFrame) && hitArea.Overlaps(ToWorld(hurtbox.Box, target)))
                {
                    hit = hitbox;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Converts a box relative to the feet (defined for a fighter that faces right) to world coordinates.</summary>
    public static FixedAABB ToWorld(FixedAABB box, in FighterData fighter) =>
        (fighter.Facing < 0 ? box.MirrorX(Fixed.Zero) : box).Translate(fighter.Position);
}
