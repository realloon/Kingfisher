// Copyright (c) 2021 bradson
// Copyright (c) 2026 Vortex
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
// SPDX-License-Identifier: MPL-2.0

using Prepatcher;
using Kingfisher.Prepatching;

namespace Kingfisher.Features;

public static class ImmunityHandlerRewrite {
    [MethodRewrite(typeof(ImmunityHandler), nameof(ImmunityHandler.NeededImmunitiesNow))]
    public static List<ImmunityHandler.ImmunityInfo> NeededImmunitiesNow(ImmunityHandler handler) {
        var hediffSet = handler.pawn.health.hediffSet;
        var cache = handler.Cache();
        return !cache.IsDirty(hediffSet) ? cache.Infos : Recompute(hediffSet, cache);
    }

    # region Helper

    private static List<ImmunityHandler.ImmunityInfo> Recompute(HediffSet hediffSet, State cache) {
        var hediffs = hediffSet.hediffs;
        var infos = cache.Infos;

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

        cache.MarkClean(hediffSet);

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