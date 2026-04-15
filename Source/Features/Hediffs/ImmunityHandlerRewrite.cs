using Prepatcher;

namespace Kingfisher.Features.Hediffs;

internal static class ImmunityHandlerRewrite {
    public static List<ImmunityHandler.ImmunityInfo> NeededImmunitiesNow(ImmunityHandler handler) {
        return TryGet(handler, out var result) ? result : Recompute(handler);
    }

    # region Helper

    private static bool TryGet(ImmunityHandler handler, out List<ImmunityHandler.ImmunityInfo> result) {
        var hediffSet = handler.pawn.health.hediffSet;
        var cache = handler.Cache();
        if (cache.IsDirty(hediffSet)) {
            result = null!;
            return false;
        }

        result = cache.Infos;
        return true;
    }

    private static List<ImmunityHandler.ImmunityInfo> Recompute(ImmunityHandler handler) {
        var hediffSet = handler.pawn.health.hediffSet;
        var hediffs = hediffSet.hediffs;
        var infos = handler.Cache().Infos;

        infos.Clear();
        foreach (var hediff in hediffs) {
            if (!hediff.def.PossibleToDevelopImmunityNaturally()) {
                continue;
            }

            infos.Add(new ImmunityHandler.ImmunityInfo {
                immunity = hediff.def,
                source = hediff.def
            });
        }

        handler.Cache().MarkClean(hediffSet);
        return infos;
    }

    [PrepatcherField]
    [ValueInitializer(nameof(CreateState))]
    private static extern State Cache(this ImmunityHandler target);

    private static State CreateState() => new();

    private sealed class State {
        private List<Hediff>? _hediffs;
        private int _hediffListVersion = -1;

        public readonly List<ImmunityHandler.ImmunityInfo> Infos = [];

        public bool IsDirty(HediffSet hediffSet) {
            var hediffs = hediffSet.hediffs;
            return _hediffs != hediffs || _hediffListVersion != hediffs._version;
        }

        public void MarkClean(HediffSet hediffSet) {
            var hediffs = hediffSet.hediffs;
            _hediffs = hediffs;
            _hediffListVersion = hediffs._version;
        }
    }

    # endregion
}