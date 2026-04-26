#if DEBUG
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;

namespace Kingfisher.Profiling.Aggregate;

public static class AggregateProfilerPatches {
    private static readonly MethodInfo BeginTickMethod = AccessTools.Method(
        typeof(AggregateProfiler),
        nameof(AggregateProfiler.BeginTick));

    private static readonly MethodInfo EndTickMethod = AccessTools.Method(
        typeof(AggregateProfiler),
        nameof(AggregateProfiler.EndTick));

    [UsedImplicitly]
    public static IEnumerable<CodeInstruction> TickTranspiler(IEnumerable<CodeInstruction> instructions,
        ILGenerator generator) {
        var codes = instructions.ToList();
        if (codes.Count == 0) {
            return codes;
        }

        var startTimestamp = generator.DeclareLocal(typeof(long));
        var firstLabels = codes[0].labels;
        codes[0].labels = [];

        var result = new List<CodeInstruction>(codes.Count + 4) {
            new(OpCodes.Call, BeginTickMethod) { labels = firstLabels },
            new(OpCodes.Stloc, startTimestamp)
        };

        foreach (var code in codes) {
            if (code.opcode == OpCodes.Ret) {
                result.Add(new CodeInstruction(OpCodes.Ldloc, startTimestamp));
                result.Add(new CodeInstruction(OpCodes.Call, EndTickMethod));
            }

            result.Add(code);
        }

        return result;
    }
}
#endif