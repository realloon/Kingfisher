using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;

namespace Kingfisher.Patches;

internal static class FreePatch_MethodReplacements {
    [FreePatch]
    public static void ReplaceHotMethods(ModuleDefinition module) {
        if (module.Assembly.Name.Name != "Assembly-CSharp") {
            return;
        }

        ReplaceMethodBody(
            module,
            "Verse.AI.AttackTargetFinder",
            "BestAttackTarget",
            typeof(FreePatchTargets).GetMethod(nameof(FreePatchTargets.BestAttackTarget))!
        );

        ReplaceMethodBody(
            module,
            "Verse.ListerThings",
            nameof(ListerThings.Remove),
            typeof(FreePatchTargets).GetMethod(nameof(FreePatchTargets.RemoveThing))!
        );

        ReplaceMethodBody(
            module,
            "Verse.ListerBuildings",
            nameof(ListerBuildings.AllBuildingsColonistOfDef),
            typeof(FreePatchTargets).GetMethod(nameof(FreePatchTargets.AllBuildingsColonistOfDef))!
        );

        ReplaceMethodBody(
            module,
            "Verse.ListerBuildings",
            nameof(ListerBuildings.ColonistsHaveBuilding),
            typeof(FreePatchTargets).GetMethod(nameof(FreePatchTargets.ColonistsHaveBuilding))!
        );

        ReplaceMethodBody(
            module,
            "Verse.ListerBuildings",
            nameof(ListerBuildings.ColonistsHaveBuildingWithPowerOn),
            typeof(FreePatchTargets).GetMethod(nameof(FreePatchTargets.ColonistsHaveBuildingWithPowerOn))!
        );

        ReplaceMethodBody(
            module,
            "RimWorld.PawnDiedOrDownedThoughtsUtility",
            nameof(PawnDiedOrDownedThoughtsUtility.RemoveLostThoughts),
            typeof(FreePatchTargets).GetMethod(nameof(FreePatchTargets.RemoveLostThoughts))!
        );

        ReplaceMethodBody(
            module,
            "RimWorld.PawnDiedOrDownedThoughtsUtility",
            nameof(PawnDiedOrDownedThoughtsUtility.RemoveResuedRelativeThought),
            typeof(FreePatchTargets).GetMethod(nameof(FreePatchTargets.RemoveResuedRelativeThought))!
        );
    }

    private static void ReplaceMethodBody(ModuleDefinition module, string typeName, string methodName,
        MethodInfo replacement) {
        var type = module.GetType(typeName)
                   ?? throw new InvalidOperationException($"Could not find type {typeName} in {module.Assembly.Name.Name}.");

        var target = type.Methods.SingleOrDefault(m => MethodMatchesReplacement(m, methodName, replacement))
                     ?? throw new InvalidOperationException(
                         $"Could not find method {typeName}.{methodName} matching replacement {replacement.Name}.");

        var importedReplacement = module.ImportReference(replacement);
        var expectedParameterCount = target.Parameters.Count + (target.HasThis ? 1 : 0);
        if (importedReplacement.Parameters.Count != expectedParameterCount) {
            throw new InvalidOperationException(
                $"Replacement parameter mismatch for {typeName}.{methodName}: expected {expectedParameterCount}, got {importedReplacement.Parameters.Count}.");
        }

        target.Body.InitLocals = false;
        target.Body.ExceptionHandlers.Clear();
        target.Body.Variables.Clear();

        var instructions = target.Body.Instructions;
        instructions.Clear();

        var processor = target.Body.GetILProcessor();
        if (target.HasThis) {
            processor.Append(processor.Create(OpCodes.Ldarg_0));
        }

        for (var i = 0; i < target.Parameters.Count; i++) {
            processor.Append(CreateLoadArgumentInstruction(processor, target.HasThis ? i + 1 : i));
        }

        processor.Append(processor.Create(OpCodes.Call, importedReplacement));
        processor.Append(processor.Create(OpCodes.Ret));
    }

    private static bool MethodMatchesReplacement(MethodDefinition target, string methodName, MethodInfo replacement) {
        if (target.Name != methodName) {
            return false;
        }

        var replacementParameters = replacement.GetParameters();
        var replacementOffset = target.HasThis ? 1 : 0;
        if (replacementParameters.Length != target.Parameters.Count + replacementOffset) {
            return false;
        }

        if (target.HasThis && replacementParameters[0].ParameterType.FullName != target.DeclaringType.FullName) {
            return false;
        }

        for (var i = 0; i < target.Parameters.Count; i++) {
            if (replacementParameters[i + replacementOffset].ParameterType.FullName != target.Parameters[i].ParameterType.FullName) {
                return false;
            }
        }

        return replacement.ReturnType.FullName == target.ReturnType.FullName;
    }

    private static Instruction CreateLoadArgumentInstruction(ILProcessor processor, int argumentIndex) => argumentIndex switch {
        0 => processor.Create(OpCodes.Ldarg_0),
        1 => processor.Create(OpCodes.Ldarg_1),
        2 => processor.Create(OpCodes.Ldarg_2),
        3 => processor.Create(OpCodes.Ldarg_3),
        _ => processor.Create(OpCodes.Ldarg, argumentIndex)
    };
}
