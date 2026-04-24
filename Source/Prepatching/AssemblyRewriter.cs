using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Prepatcher;
using JetBrains.Annotations;

namespace Kingfisher.Prepatching;

public static class AssemblyRewriter {
    [UsedImplicitly]
    [FreePatch]
    public static void ReplaceMethods(ModuleDefinition module) {
        if (module.Assembly.Name.Name != "Assembly-CSharp") return;

        foreach (var replacement in GetMethodBodyReplacements()) {
            ReplaceMethodBody(module, replacement.TargetTypeName, replacement.TargetMethodName, replacement.Rewrite);
        }
    }

    private static IEnumerable<MethodBodyReplacement> GetMethodBodyReplacements() {
        var replacementsBySignature = new Dictionary<string, MethodInfo>();
        var methods =
            typeof(AssemblyRewriter).Assembly.GetTypes()
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .OrderBy(method => method.DeclaringType!.FullName, StringComparer.Ordinal)
                .ThenBy(method => method.Name, StringComparer.Ordinal);

        foreach (var method in methods) {
            var attribute = method.GetCustomAttribute<MethodRewriteAttribute>();
            if (attribute == null) {
                continue;
            }

            if (!method.IsStatic) {
                throw new InvalidOperationException(
                    $"Rewrite method {method.DeclaringType!.FullName}.{method.Name} must be static.");
            }

            var targetTypeName = attribute.TargetType.FullName
                                 ?? throw new InvalidOperationException(
                                     $"Target type {attribute.TargetType} has no full name.");
            var signatureKey = GetReplacementSignatureKey(targetTypeName, attribute.TargetMethodName, method);
            if (!replacementsBySignature.TryAdd(signatureKey, method)) {
                throw new InvalidOperationException(
                    $"Duplicate replacement binding for {targetTypeName}.{attribute.TargetMethodName}: " +
                    $"{replacementsBySignature[signatureKey].DeclaringType!.FullName}.{replacementsBySignature[signatureKey].Name} and " +
                    $"{method.DeclaringType!.FullName}.{method.Name}.");
            }

            yield return new MethodBodyReplacement(targetTypeName, attribute.TargetMethodName, method);
        }
    }

    private static void ReplaceMethodBody(ModuleDefinition module, string typeName, string methodName,
        MethodInfo rewrite) {
        var type = module.GetType(typeName)
                   ?? throw new InvalidOperationException(
                       $"Could not find type {typeName} in {module.Assembly.Name.Name}.");

        var importedRewrite = module.ImportReference(rewrite);
        MethodDefinition? target = null;

        foreach (var method in type.Methods) {
            if (!MethodMatchesRewrite(method, methodName, importedRewrite)) {
                continue;
            }

            target = method;
            break;
        }

        if (target == null) {
            throw new InvalidOperationException(
                $"Could not find method {typeName}.{methodName} matching rewrite {rewrite.Name}.");
        }

        var expectedParameterCount = target.Parameters.Count + (target.HasThis ? 1 : 0);
        if (importedRewrite.Parameters.Count != expectedParameterCount) {
            throw new InvalidOperationException(
                $"Rewrite parameter mismatch for {typeName}.{methodName}: expected {expectedParameterCount}, got {importedRewrite.Parameters.Count}.");
        }

        var body = target.Body;
        body.InitLocals = false;
        body.ExceptionHandlers.Clear();
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

    private static bool MethodMatchesRewrite(MethodDefinition target, string methodName, MethodReference rewrite) {
        if (target.Name != methodName) {
            return false;
        }

        var replacementOffset = target.HasThis ? 1 : 0;
        if (rewrite.Parameters.Count != target.Parameters.Count + replacementOffset) {
            return false;
        }

        if (target.HasThis && rewrite.Parameters[0].ParameterType.FullName != target.DeclaringType.FullName) {
            return false;
        }

        for (var i = 0; i < target.Parameters.Count; i++) {
            if (rewrite.Parameters[i + replacementOffset].ParameterType.FullName !=
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

    private static string GetReplacementSignatureKey(string typeName, string methodName, MethodInfo rewrite) {
        var parameterTypes = string.Join(",",
            rewrite.GetParameters().Select(parameter => parameter.ParameterType.FullName));
        return $"{typeName}.{methodName}({parameterTypes})->{rewrite.ReturnType.FullName}";
    }

    private sealed class MethodBodyReplacement(string targetTypeName, string targetMethodName, MethodInfo rewrite) {
        public readonly string TargetTypeName = targetTypeName;
        public readonly string TargetMethodName = targetMethodName;
        public readonly MethodInfo Rewrite = rewrite;
    }
}