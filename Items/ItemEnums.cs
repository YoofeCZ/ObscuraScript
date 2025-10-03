namespace Obscurus.Items
{
    public enum ItemType
    {
        Misc = 0,
        Weapon,
        Armour,
        Consumable,
        PickupConsumable,
        Quest,
        Material,

        // NEW:
        Currency,   // pražské groše – „měna“
        Substance,  // alchymické substance (skill tree vstup)
        Ammunition
    }

    public enum WeaponKind
    {
        Ranged = 0,
        Melee = 1
    }

    public enum Rarity
    {
        Common = 0,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    public enum EquipmentSlot
    {
        None = 0,
        Head, Chest, Arms, Legs, OffHand, MainHand, TwoHanded
    }

    public enum ItemEffectType
    {
        HealHP,
        RestoreStamina,
        AddArmorFlat,
        RegenHPOverTime,
        RegenStaminaOverTime,
        RestoreSanity,
        RegenSanityOverTime
    }

    // NEW — větev pro substance / skill tree
    public enum SubstanceBranch
    {
        Vitriol,   // síla / útok (zelený kámen)
        Aurum,     // obrana / život
        Mercurius  // rychlost / „magie“
    }

    // NEW — fixní zdroje v inventáři
    public enum ResourceKey
    {
        PG,           // Pražské groše
        Gold,         // Zlato
        Mercury,      // Rtuť
        Essence,      // Esence
        Scrap,        // Šrot
        Sub_Vitriol,  // substance
        Sub_Aurum,
        Sub_Mercurius
    }
}