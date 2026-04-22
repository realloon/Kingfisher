using JetBrains.Annotations;

namespace Kingfisher.Prepatching;

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Method)]
internal sealed class MethodRewriteAttribute(Type targetType, string targetMethodName) : Attribute {
    public readonly Type TargetType = targetType;
    public readonly string TargetMethodName = targetMethodName;
}