using Prepatcher;

namespace Kingfisher.Features.Hediffs;

#if true
internal static class HediffDefImmunityRewrite {
    public static bool PossibleToDevelopImmunityNaturally(HediffDef hediffDef) {
        ref var cachedResult = ref hediffDef.PossibleToDevelopImmunityNaturallyCache();
        if (cachedResult != CacheState.Unknown) {
            return cachedResult == CacheState.True;
        }

        var result = ComputePossibleToDevelopImmunityNaturally(hediffDef);
        cachedResult = result ? CacheState.True : CacheState.False;
        return result;
    }

    private static bool ComputePossibleToDevelopImmunityNaturally(HediffDef hediffDef) {
        var comps = hediffDef.comps;
        if (comps == null) {
            return false;
        }

        foreach (var t in comps) {
            if (t is not HediffCompProperties_Immunizable immunizable) {
                continue;
            }

            if (immunizable.immunityPerDayNotSick > 0f || immunizable.immunityPerDaySick > 0f) {
                return true;
            }
        }

        return false;
    }

    [PrepatcherField]
    private static extern ref CacheState PossibleToDevelopImmunityNaturallyCache(this HediffDef target);

    private enum CacheState : byte {
        Unknown = 0,
        False = 1,
        True = 2
    }
}
#endif