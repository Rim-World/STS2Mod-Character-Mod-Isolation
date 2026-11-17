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

    public static bool ShouldRemoveCharacter(Type type) =>
        CardsActive && CharacterModRegistry.GetIsolatedOwner(type) is not null;

    public static bool ShouldRemoveCard(Type type) =>
        CardsActive && CharacterModRegistry.GetIsolatedOwner(type) is not null;

    public static bool ShouldEmptyCardPool(Type poolType) =>
        CardsActive && CharacterModRegistry.GetIsolatedOwner(poolType) is not null;

    public static bool ShouldRemoveRelic(Type type) =>
        RelicsActive && CharacterModRegistry.GetIsolatedOwner(type) is not null;

    public static bool ShouldEmptyRelicPool(Type poolType) =>
        RelicsActive && CharacterModRegistry.GetIsolatedOwner(poolType) is not null;

    public static bool ShouldRemovePotion(Type type) =>
        PotionsActive && CharacterModRegistry.GetIsolatedOwner(type) is not null;

    public static bool ShouldEmptyPotionPool(Type poolType) =>
        PotionsActive && CharacterModRegistry.GetIsolatedOwner(poolType) is not null;

    public static bool ShouldRemoveEvent(Type type) =>
        EventsActive && CharacterModRegistry.GetIsolatedOwner(type) is not null;

    public static bool ShouldRemoveEncounter(Type type) =>
        EncountersActive && CharacterModRegistry.GetIsolatedOwner(type) is not null;

    public static bool ShouldRemoveMonster(Type type) =>
        EncountersActive && CharacterModRegistry.GetIsolatedOwner(type) is not null;

    public static bool ShouldRemoveAncient(Type type) =>
        AncientsActive && CharacterModRegistry.GetIsolatedOwner(type) is not null;

    public static bool ShouldRemoveAct(Type type) =>
        ActsActive && CharacterModRegistry.GetIsolatedOwner(type) is not null;

    public static bool ShouldRemoveActForRun(Type type) =>
        Active && ModSettings.Current.FilterActs && CharacterModRegistry.GetIsolatedOwner(type) is not null;
}
