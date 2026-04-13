using Prepatcher;

namespace Kingfisher.Prepatching;

internal static class InjectedThingCompFields {
    [PrepatcherField]
    [InjectComponent]
    public static extern CompExplosive? ExplosiveComp(this ThingWithComps target);

    [PrepatcherField]
    [InjectComponent]
    public static extern CompPowerTrader? PowerTraderComp(this ThingWithComps target);

    [PrepatcherField]
    [InjectComponent]
    public static extern CompUniqueWeapon? UniqueWeaponComp(this ThingWithComps target);
}