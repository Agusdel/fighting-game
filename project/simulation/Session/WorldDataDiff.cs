using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using FightingGame.Simulation;

namespace FightingGame.Session;

/// <summary>One field that has a different value in two world states.</summary>
/// <param name="Path">The field, for example <c>Fighters[1].Position</c>.</param>
public sealed record FieldDifference(string Path, string Expected, string Actual)
{
    public override string ToString() => $"{Path}: {Expected} -> {Actual}";
}

/// <summary>
/// Finds the fields that differ between two <see cref="WorldData"/> values. For desync reports only.
/// </summary>
/// <remarks>
/// It reads all fields with reflection, so it finds new fields with no change here. It is slow and allocates,
/// so the tick never calls it: a session calls it only when two hashes differ.
/// <list type="bullet">
/// <item>A field of a primitive type, an enum, or a type with its own <c>ToString</c> (for example <c>Fixed</c>,
/// <c>FixedVector2</c>) is one value.</item>
/// <item>Other structs are compared field by field (also private fields, for example the state of <c>FixedRng</c>).</item>
/// <item>Inline arrays (<c>FighterArray</c>) are compared element by element.</item>
/// </list>
/// </remarks>
public static class WorldDataDiff
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly MethodInfo ReadElementMethod =
        typeof(WorldDataDiff).GetMethod(nameof(ReadElement), BindingFlags.Static | BindingFlags.NonPublic)!;

    public static IReadOnlyList<FieldDifference> Compare(in WorldData expected, in WorldData actual)
    {
        var differences = new List<FieldDifference>();
        CompareValues("", typeof(WorldData), expected, actual, differences);
        return differences;
    }

    private static void CompareValues(string path, Type type, object? expected, object? actual, List<FieldDifference> differences)
    {
        if (IsSingleValue(type))
        {
            if (!Equals(expected, actual))
            {
                differences.Add(new FieldDifference(path, expected?.ToString() ?? "null", actual?.ToString() ?? "null"));
            }
            return;
        }

        if (type.GetCustomAttribute<InlineArrayAttribute>() is { } inlineArray)
        {
            Type elementType = type.GetFields(InstanceFields)[0].FieldType;
            MethodInfo read = ReadElementMethod.MakeGenericMethod(type, elementType);
            for (int i = 0; i < inlineArray.Length; i++)
            {
                CompareValues($"{path}[{i}]", elementType,
                    read.Invoke(null, new[] { expected, i }), read.Invoke(null, new[] { actual, i }), differences);
            }
            return;
        }

        foreach (FieldInfo field in type.GetFields(InstanceFields))
        {
            string fieldPath = path.Length == 0 ? field.Name : $"{path}.{field.Name}";
            CompareValues(fieldPath, field.FieldType, field.GetValue(expected), field.GetValue(actual), differences);
        }
    }

    private static bool IsSingleValue(Type type) =>
        type.IsPrimitive || type.IsEnum || !type.IsValueType
        || type.GetMethod(nameof(ToString), Type.EmptyTypes)?.DeclaringType == type;

    /// <summary>Reads element <paramref name="index"/> of a boxed inline array.</summary>
    private static TElement ReadElement<TArray, TElement>(object boxedArray, int index) where TArray : struct
    {
        TArray array = (TArray)boxedArray;
        return Unsafe.Add(ref Unsafe.As<TArray, TElement>(ref array), index);
    }
}
