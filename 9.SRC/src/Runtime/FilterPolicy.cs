namespace CharacterModIsolation;

internal static class FilterPolicy
{
    public static bool Active => RunContext.IsActive;

    public static bool GenerationActive => RunContext.IsActiveForActs;

    public static bool CardsActive => Active && ModSettings.Current.FilterCards;

    public static bool RelicsActive => Active && ModSettings.Current.FilterRelics;

    public static bool PotionsActive => Active && ModSettings.Current.FilterPotions;

    public static bool EventsActive => RunContext.IsActiveForActs && ModSettings.Current.FilterEvents;

    public static bool EncountersActive => RunContext.IsActiveForActs && ModSettings.Current.FilterEncounters;

    public static bool AncientsActive => RunContext.IsActiveForActs && ModSettings.Current.FilterAncients;

    public static bool ActsActive => RunContext.IsActiveForActs && ModSettings.Current.FilterActs;

    public static bool ShouldRemoveCharacter(Type type) => IsIsolated(type);

    public static bool ShouldRemoveCard(Type type) => IsIsolated(type);

    public static bool ShouldEmptyCardPool(Type poolType) => IsIsolated(poolType);

    public static bool ShouldRemoveRelic(Type type) => IsIsolated(type);

    public static bool ShouldEmptyRelicPool(Type poolType) => IsIsolated(poolType);

    public static bool ShouldRemovePotion(Type type) => IsIsolated(type);

    public static bool ShouldEmptyPotionPool(Type poolType) => IsIsolated(poolType);

    public static bool ShouldRemoveEvent(Type type) => IsIsolated(type);

    public static bool ShouldRemoveEncounter(Type type) => IsIsolated(type);

    public static bool ShouldRemoveMonster(Type type) => IsIsolated(type);

    public static bool ShouldRemoveAncient(Type type) => IsIsolated(type);

    public static bool ShouldRemoveAct(Type type) => IsIsolated(type);

    public static bool ShouldRemoveActForRun(Type type) => IsIsolated(type);

    private static bool IsIsolated(Type type) =>
        RunContext.IsIsolated(ContentOwnership.GetOwnerModId(type));
}
