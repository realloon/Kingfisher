using JetBrains.Annotations;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;
using Verse.AI;
using Kingfisher.Features.Buildings;
using Kingfisher.Features.Combat;
using Kingfisher.Features.Things;
using Kingfisher.Features.Thoughts;

namespace Kingfisher.Prepatching;

internal static class AssemblyRewriter {
    [UsedImplicitly]
    [FreePatch]
    public static void ReplaceMethods(ModuleDefinition module) {
        if (!IsAssemblyCSharp(module)) return;

        ReplaceMethodBody(
            module,
            "Verse.ListerThings",
            nameof(ListerThings.Remove),
            typeof(ListerThingsRewrite).GetMethod(nameof(ListerThingsRewrite.Remove))!
        );

        ReplaceMethodBody(
            module,
            "Verse.ListerBuildings",
            nameof(ListerBuildings.AllBuildingsColonistOfDef),
            typeof(ListerBuildingsRewrite).GetMethod(nameof(ListerBuildingsRewrite.AllBuildingsColonistOfDef))!
        );

        ReplaceMethodBody(
            module,
            "Verse.ListerBuildings",
            nameof(ListerBuildings.ColonistsHaveBuilding),
            typeof(ListerBuildingsRewrite).GetMethod(nameof(ListerBuildingsRewrite.ColonistsHaveBuilding))!
        );

        ReplaceMethodBody(
            module,
            "Verse.ListerBuildings",
            nameof(ListerBuildings.ColonistsHaveBuildingWithPowerOn),
            typeof(ListerBuildingsRewrite)
                .GetMethod(nameof(ListerBuildingsRewrite.ColonistsHaveBuildingWithPowerOn))!
        );

        ReplaceMethodBody(
            module,
            "Verse.AI.AttackTargetFinder",
            nameof(AttackTargetFinder.BestAttackTarget),
            typeof(AttackTargetFinderRewrite).GetMethod(nameof(AttackTargetFinderRewrite.BestAttackTarget))!
        );

        ReplaceMethodBody(
            module,
            "RimWorld.PawnDiedOrDownedThoughtsUtility",
            nameof(PawnDiedOrDownedThoughtsUtility.RemoveLostThoughts),
            typeof(PawnDiedOrDownedThoughtsRewrite)
                .GetMethod(nameof(PawnDiedOrDownedThoughtsRewrite.RemoveLostThoughts))!
        );

        ReplaceMethodBody(
            module,
            "RimWorld.PawnDiedOrDownedThoughtsUtility",
            nameof(PawnDiedOrDownedThoughtsUtility.RemoveResuedRelativeThought),
            typeof(PawnDiedOrDownedThoughtsRewrite)
                .GetMethod(nameof(PawnDiedOrDownedThoughtsRewrite.RemoveResuedRelativeThought))!
        );
    }

    private static bool IsAssemblyCSharp(ModuleDefinition module) {
        return module.Assembly.Name.Name == "Assembly-CSharp";
    }

    private static void ReplaceMethodBody(ModuleDefinition module, string typeName, string methodName,
        MethodInfo rewrite) {
        var type = module.GetType(typeName)
                   ?? throw new InvalidOperationException(
                       $"Could not find type {typeName} in {module.Assembly.Name.Name}.");

        MethodDefinition? target = null;
        foreach (var method in type.Methods) {
            if (!MethodMatchesRewrite(method, methodName, rewrite)) {
                continue;
            }

            target = method;
            break;
        }

        if (target == null) {
            throw new InvalidOperationException(
                $"Could not find method {typeName}.{methodName} matching rewrite {rewrite.Name}.");
        }

        var importedRewrite = module.ImportReference(rewrite);
        var expectedParameterCount = target.Parameters.Count + (target.HasThis ? 1 : 0);
        if (importedRewrite.Parameters.Count != expectedParameterCount) {
            throw new InvalidOperationException(
                $"Rewrite parameter mismatch for {typeName}.{methodName}: expected {expectedParameterCount}, got {importedRewrite.Parameters.Count}.");
        }

        var body = target.Body;
        body.InitLocals = false;
        body.ExceptionHandlers.Clear();
        body.Variables.Clear();
        body.Instructions.Clear();

        var processor = body.GetILProcessor();
        if (target.HasThis) {
            processor.Append(processor.Create(OpCodes.Ldarg_0));
        }

        for (var i = 0; i < target.Parameters.Count; i++) {
            processor.Append(CreateLoadArgumentInstruction(processor, target.HasThis ? i + 1 : i));
        }

        processor.Append(processor.Create(OpCodes.Call, importedRewrite));
        processor.Append(processor.Create(OpCodes.Ret));
    }

    private static bool MethodMatchesRewrite(MethodDefinition target, string methodName, MethodInfo rewrite) {
        if (target.Name != methodName) {
            return false;
        }

        var rewriteParameters = rewrite.GetParameters();
        var replacementOffset = target.HasThis ? 1 : 0;
        if (rewriteParameters.Length != target.Parameters.Count + replacementOffset) {
            return false;
        }

        if (target.HasThis && rewriteParameters[0].ParameterType.FullName != target.DeclaringType.FullName) {
            return false;
        }

        for (var i = 0; i < target.Parameters.Count; i++) {
            if (rewriteParameters[i + replacementOffset].ParameterType.FullName !=
                target.Parameters[i].ParameterType.FullName) {
                return false;
            }
        }

        return rewrite.ReturnType.FullName == target.ReturnType.FullName;
    }

    private static Instruction CreateLoadArgumentInstruction(ILProcessor processor, int argumentIndex) =>
        argumentIndex switch {
            0 => processor.Create(OpCodes.Ldarg_0),
            1 => processor.Create(OpCodes.Ldarg_1),
            2 => processor.Create(OpCodes.Ldarg_2),
            3 => processor.Create(OpCodes.Ldarg_3),
            _ => processor.Create(OpCodes.Ldarg, argumentIndex)
        };
}
