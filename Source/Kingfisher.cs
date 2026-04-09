global using Verse;
global using RimWorld;
using JetBrains.Annotations;
using HarmonyLib;

namespace Kingfisher;

[UsedImplicitly]
[StaticConstructorOnStartup]
public class Kingfisher {
    static Kingfisher() {
        var harmony = new Harmony("Vortex.Kingfisher");
        harmony.PatchAll();
    }
}