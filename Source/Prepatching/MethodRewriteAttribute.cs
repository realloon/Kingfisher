using JetBrains.Annotations;

namespace Kingfisher.Prepatching;

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Method)]
public sealed class MethodRewriteAttribute(Type targetType, string targetMethodName) : Attribute {
    public readonly Type TargetType = targetType;
    public readonly string TargetMethodName = targetMethodName;
}