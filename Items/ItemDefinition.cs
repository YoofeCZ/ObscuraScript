#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Items
{

#if HAS_ODIN
    [HideMonoScript]
    [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
    [InfoBox("Definice itemu (ScriptableObject). Pole se zobrazují podle ItemType.")]
#endif
    [CreateAssetMenu(fileName = "Item_", menuName = "Obscurus/Items/Item Definition")]
    public class ItemDefinition : ScriptableObject
    {
        // ===== Identity =====
        [Header("Identita")]
        [SerializeField, Tooltip("Lidsky čitelné jméno.")]
        private string displayName = "New Item";

        [SerializeField, Tooltip("Stabilní GUID – generuje se automaticky.")]
        private string itemId;

        [SerializeField] private ItemType type = ItemType.Misc;
        [SerializeField] private Rarity rarity = Rarity.Common;
        [SerializeField, Min(1)] private int maxStack = 1;

        // ===== Visual =====
        [Header("Vizuál/Prefab")]
        public Sprite icon;
        public GameObject prefab;
        public EquipmentSlot equipSlot = EquipmentSlot.None;

        // ===== Text =====
        [Header("Popis")]
        [TextArea(2, 6)] public string description;
        [TextArea(1, 4)] public string notes;

        // ===== Per-type data =====

        // (A) WEAPON – rozlišení Ranged/Melee + payloady
#if HAS_ODIN
        [ShowIf("@type == ItemType.Weapon")]
        [LabelText("Weapon Kind")]
#endif
        public WeaponKind weaponKind = WeaponKind.Ranged;

#if HAS_ODIN
        [ShowIf("@type == ItemType.Weapon && weaponKind == WeaponKind.Ranged"), InlineProperty, HideLabel]
#endif
        public RangedWeaponData ranged;

#if HAS_ODIN
        [ShowIf("@type == ItemType.Weapon && weaponKind == WeaponKind.Melee"), InlineProperty, HideLabel]
#endif
        public MeleeWeaponData melee;

        // ⚠️ BACKWARD-COMPAT: původní jednotná WeaponData (skrytá)
        [Obsolete("Použij 'ranged' (RangedWeaponData) nebo 'melee' (MeleeWeaponData) podle weaponKind.")]
        [HideInInspector] public WeaponData weapon;

        // (B) ARMOUR
#if HAS_ODIN
        [ShowIf("@type == ItemType.Armour"), InlineProperty, HideLabel]
#endif
        public ArmourData armour;

        // (C) CONSUMABLE / PICKUP CONSUMABLE
#if HAS_ODIN
        [ShowIf("@type == ItemType.Consumable || type == ItemType.PickupConsumable"), InlineProperty, HideLabel]
#endif
        public ConsumableData consumable;

#if HAS_ODIN
        [ShowIf("@type == ItemType.PickupConsumable")]
        [LabelText("Apply instantly on pickup")]
#endif
        public bool applyInstantlyOnPickup = true;

        // (D) MATERIAL
#if HAS_ODIN
        [ShowIf("@type == ItemType.Material"), InlineProperty, HideLabel]
#endif
        public MaterialData material;

        // (E) QUEST
#if HAS_ODIN
        [ShowIf("@type == ItemType.Quest"), InlineProperty, HideLabel]
#endif
        public QuestData quest;

        // (F) Substance
#if HAS_ODIN
        [ShowIf("@type == ItemType.Substance"), InlineProperty, HideLabel]
#endif
        public SubstanceData substance;

        // (G) Ammunition
#if HAS_ODIN
        [ShowIf("@type == ItemType.Ammunition"), InlineProperty, HideLabel]
#endif
        public AmmunitionData ammo;

        // (H) Currency
#if HAS_ODIN
        [ShowIf("@type == ItemType.Currency"), InlineProperty, HideLabel]
#endif
        public CurrencyData currency;

        // ===== Common advanced =====
        [Header("Modifikátory (volitelné)")]
        public List<ItemModifier> modifiers = new();

        [Header("Požadavky (volitelné)")]
        [SerializeReference] public List<ItemRequirement> requirements = new();

        // ===== Accessors =====
#if HAS_ODIN
        [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
#endif
        public string Id => itemId;

#if HAS_ODIN
        [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
#endif
        public string Name { get => displayName; set => displayName = value; }

        public ItemType Type => type;
        public Rarity Rarity => rarity;
        public int MaxStack => Mathf.Max(1, maxStack);
        public bool IsConsumableLike => type == ItemType.Consumable || type == ItemType.PickupConsumable;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(itemId))
                itemId = Guid.NewGuid().ToString("N");
            if (maxStack < 1) maxStack = 1;

            // MIGRACE: původní 'weapon' -> 'ranged' / 'melee'
            if (type == ItemType.Weapon && weapon != null)
            {
                if (weaponKind == WeaponKind.Ranged)
                {
                    bool isRangedUnset =
                        ranged.baseDamage <= 0f &&
                        string.IsNullOrEmpty(ranged.ammoKey) &&
                        ranged.magazineSize == 0 &&
                        ranged.reloadSeconds <= 0f &&
                        ranged.shotsPerUse <= 1 &&
                        ranged.startLoaded == 0;

                    if (isRangedUnset)
                    {
                        ranged.baseDamage   = weapon.baseDamage;
                        ranged.attackSpeed  = weapon.attackSpeed;
                        ranged.critChance   = weapon.critChance;
                        ranged.durability   = weapon.durability;
                        ranged.ammoKey      = weapon.ammoKey;
                        ranged.shotsPerUse  = weapon.shotsPerUse;
                        ranged.magazineSize = weapon.magazineSize;
                        ranged.reloadSeconds= weapon.reloadSeconds;
                        ranged.startLoaded  = weapon.startLoaded;
                    }
                }
                if (weaponKind == WeaponKind.Melee)
                {
                    bool isMeleeUnset =
                        melee.baseDamage <= 0f &&
                        melee.attackCooldown <= 0f;

                    if (isMeleeUnset)
                    {
                        melee.baseDamage     = weapon.baseDamage;
                        melee.attackCooldown = Mathf.Max(0.1f, 1f / Mathf.Max(0.01f, weapon.attackSpeed));
                        melee.critChance     = weapon.critChance;
                        melee.durability     = weapon.durability;
                    }
                }
            }
        }

        public override string ToString() => $"{displayName} [{type}] ({rarity})";
    }

    // ======= PER-TYPE DATA =======

    /// <summary>⚠️ původní unifikovaná definice zbraně – ponecháno pro backward compat.</summary>
    [Serializable]
    public class WeaponData
    {
#if HAS_ODIN
        [HorizontalGroup("w1", 0.5f)] [LabelText("Base Damage")]
#endif
        public float baseDamage = 10f;

#if HAS_ODIN
        [HorizontalGroup("w1")] [LabelText("Attack Speed")]
#endif
        public float attackSpeed = 1.0f;

#if HAS_ODIN
        [HorizontalGroup("w2", 0.5f)] [LabelText("Crit %")]
#endif
        [Range(0, 100)] public float critChance = 0;

#if HAS_ODIN
        [HorizontalGroup("w2")] [LabelText("Durability")]
#endif
        [Range(0, 100)] public float durability = 100f;

        // Munice
#if HAS_ODIN
        [HorizontalGroup("w3", 0.6f)] [LabelText("Ammo Key (optional)"), Tooltip("Např. 'bolt_nail', 'pistol_ball'.")]
#endif
        public string ammoKey = ""; // prázdné = nemá munici

#if HAS_ODIN
        [HorizontalGroup("w3", 0.4f)] [LabelText("Shots / Use")]
#endif
        [Min(1)] public int shotsPerUse = 1;

        // zásobník & reload
#if HAS_ODIN
        [HorizontalGroup("w4", 0.33f)] [LabelText("Magazine Size")]
#endif
        [Min(0)] public int magazineSize = 0; // 0 = bez zásobníku

#if HAS_ODIN
        [HorizontalGroup("w4", 0.33f)] [LabelText("Reload (s)")]
#endif
        [Min(0f)] public float reloadSeconds = 1.5f;

#if HAS_ODIN
        [HorizontalGroup("w4", 0.33f)] [LabelText("Start Loaded")]
#endif
        [Min(0)] public int startLoaded = 0;
    }

    /// <summary>Ranged zbraň – rozšířená o damage typy, projektil a AoE parametry.</summary>
    [Serializable]
    public class RangedWeaponData
    {
#if HAS_ODIN
        [HorizontalGroup("rw1", 0.25f)] [LabelText("Base Damage")]
#endif
        public float baseDamage = 10f;

#if HAS_ODIN
        [HorizontalGroup("rw1", 0.25f)] [LabelText("Attack Speed")]
#endif
        public float attackSpeed = 1.0f;

#if HAS_ODIN
        [HorizontalGroup("rw1", 0.25f)] [LabelText("Crit %")]
#endif
        [Range(0, 100)] public float critChance = 0;

#if HAS_ODIN
        [HorizontalGroup("rw1", 0.25f)] [LabelText("Durability")]
#endif
        [Range(0, 100)] public float durability = 100f;

        // --- Damage typing ---
        [Header("Damage Typing")]
        [Tooltip("Primární typ poškození zbraně (HUD/balance).")]
        public DamageType primaryDamage = DamageType.Physical;

        [Tooltip("Dodatečné tagy (např. Fire + Pierce).")]
        public List<DamageType> extraDamageTags = new();

        // --- Ammo & Magazine ---
        [Header("Ammo & Magazine")]
        [Tooltip("Identifikátor munice – musí sedět s položkou typu Ammunition v DB.")]
        public string ammoKey = "";

        [Min(1), Tooltip("Výstřelů na jedno použití (burst).")]
        public int shotsPerUse = 1;

        [Min(0), Tooltip("Velikost zásobníku (0 = bez zásobníku).")]
        public int magazineSize = 0;

        [Min(0f), Tooltip("Reload v sekundách.")]
        public float reloadSeconds = 1.5f;

        [Min(0), Tooltip("Náboje po equipu.")]
        public int startLoaded = 0;

        // --- Projectile / Pattern ---
        [Header("Projectile / Pattern")]
        [Tooltip("Rychlost projektilu (m/s).")]
        public float projectileSpeed = 120f;

        [Tooltip("Životnost projektilu (s).")]
        public float projectileLifetime = 3f;

        [Tooltip("Použít gravitaci? 0/1 uložené v DB.")]
        [Range(0f, 1f)] public float projectileGravity = 0f;

        [Tooltip("Rozptyl ve stupních.")]
        [Range(0f, 15f)] public float spreadAngleDeg = 0.5f;

        [Tooltip("Počet pelet na výstřel (shotgun).")]
        [Min(1)] public int pelletCount = 1;

        [Tooltip("Násobič dmg pelet (pokud nedělíš mezi pelety).")]
        public float pelletDamageMultiplier = 0.2f;

        [Tooltip("1 = dělí damage mezi pelety; 0 = násobí pelletDamageMultiplier.")]
        [Range(0f, 1f)] public float splitDamageAcrossPellets = 1f;

        [Tooltip("Průraz (interpretace na projektilu).")]
        public float penetration = 0f;

        [Tooltip("Šance na odraz (0–1).")]
        [Range(0f, 1f)] public float ricochetChance = 0f;

        [Header("Headshot (volitelné)")]
        [Range(0f, 1f)] public float canHeadshot = 0f;
        public float headshotMultiplier = 1.5f;

        // --- AoE / DoT ---
        [Header("AoE / DoT (po dopadu)")]
        [Range(0f, 1f)] public float aoeEnabled = 0f;
        public DamageType aoeDamageType = DamageType.Lightning;
        [Min(0f)] public float aoeRadius = 6f;
        [Min(0f)] public float aoeDuration = 3f;
        [Min(0.05f)] public float aoeTickInterval = 0.25f;
        [Min(0f)] public float aoeDamage = 20f;
        [Min(0f)] public float aoeSplashDelay = 0f;
        [Min(0f)] public float aoeDotDps = 0f;
        [Min(0f)] public float aoeDotDuration = 0f;

        [Header("AoE Targeting (volitelné)")]
        public LayerMask aoeHitMask = ~0;
        public string aoeEnemyTag = "Enemy";
    }

    /// <summary>Melee zbraň – doplněno typování poškození pro synergii s resistencemi.</summary>
    [Serializable]
    public class MeleeWeaponData
    {
#if HAS_ODIN
        [HorizontalGroup("mw1", 0.25f)] [LabelText("Base Damage")]
#endif
        public float baseDamage = 25f;

#if HAS_ODIN
        [HorizontalGroup("mw1", 0.25f)] [LabelText("Cooldown (s)")]
#endif
        [Min(0.05f)] public float attackCooldown = 0.6f;

#if HAS_ODIN
        [HorizontalGroup("mw1", 0.25f)] [LabelText("Crit %")]
#endif
        [Range(0, 100)] public float critChance = 0;

#if HAS_ODIN
        [HorizontalGroup("mw1", 0.25f)] [LabelText("Durability")]
#endif
        [Range(0, 100)] public float durability = 100f;

        [Header("Damage Typing")]
        public DamageType primaryDamage = DamageType.Physical;
        public List<DamageType> extraDamageTags = new();

        [Header("Reach")]
        [Min(0f)] public float range = 2.2f;
        [Min(0f)] public float radius = 0.25f;
    }

    [Serializable]
    public class ArmourData
    {
#if HAS_ODIN
        [HorizontalGroup("a1", 0.5f)] [LabelText("Armor")]
#endif
        public float armorValue = 10f;

#if HAS_ODIN
        [HorizontalGroup("a1")] [LabelText("Durability")]
#endif
        [Range(0, 100)] public float durability = 100f;

        [Header("Resistence podle DamageType (volitelné)")]
        [Tooltip("Seznam resistencí – flat se odečte, pak se násobí multiplikátorem.")]
        public List<DamageResistance> resistances = new();
    }

    [Serializable]
    public class ConsumableData
    {
#if HAS_ODIN
        [LabelText("Destroy After Use")]
#endif
        public bool destroyOnUse = true;

#if HAS_ODIN
        [LabelText("Cooldown (s)")]
#endif
        [Min(0)] public float cooldown = 0;

#if HAS_ODIN
        [TableList(AlwaysExpanded = true)]
#endif
        public List<ItemEffect> effects = new();
    }

    [Serializable]
    public class MaterialData
    {
#if HAS_ODIN
        [LabelText("Craft Tags"), Tooltip("Herb, Ore, Scrap, Glass...")]
#endif
        public List<string> tags = new();

#if HAS_ODIN
        [LabelText("Notes"), MultiLineProperty]
#endif
        public string craftingNotes;
    }

    [Serializable]
    public class QuestData
    {
#if HAS_ODIN
        [HorizontalGroup("q1", 0.6f)] [LabelText("Quest Id")]
#endif
        public string questId = "Quest/Main/Chapter1";

#if HAS_ODIN
        [HorizontalGroup("q1", 0.2f)] [LabelText("Droppable")]
#endif
        public bool droppable = false;

#if HAS_ODIN
        [HorizontalGroup("q1", 0.2f)] [LabelText("Tradable")]
#endif
        public bool tradable = false;
    }

    // >>> Substance payload
    [Serializable]
    public class SubstanceData
    {
#if HAS_ODIN
        [LabelText("Branch")]
#endif
        public SubstanceBranch branch = SubstanceBranch.Vitriol;

#if HAS_ODIN
        [LabelText("Lore / Notes"), MultiLineProperty]
#endif
        public string lore;
    }

    // >>> Ammunition payload (rozšířeno o balistiku/tagy)
    [Serializable]
    public class AmmunitionData
    {
#if HAS_ODIN
        [HorizontalGroup("am1", 0.5f)] [LabelText("Ammo Key"), Tooltip("Identifikátor munice – musí sedět se zbraní.")]
#endif
        public string ammoKey = "bolt_nail";

#if HAS_ODIN
        [HorizontalGroup("am1", 0.5f)] [LabelText("Max Carry")]
#endif
        [Min(1)] public int maxCarry = 200;

#if HAS_ODIN
        [HorizontalGroup("am2", 0.33f)] [LabelText("Dmg Mult")]
#endif
        [Min(0)] public float damageMultiplier = 1f;

#if HAS_ODIN
        [HorizontalGroup("am2", 0.33f)] [LabelText("Speed Mult")]
#endif
        public float speedMultiplier = 1f;

#if HAS_ODIN
        [HorizontalGroup("am2", 0.34f)] [LabelText("Accuracy Mult")]
#endif
        public float accuracyMultiplier = 1f;

        [Header("Penetrace / Odraz (volitelné)")]
        public float penetrationBonus = 0f;
        [Range(0f,1f)] public float ricochetChanceBonus = 0f;

        [Header("Damage Tagy (volitelné)")]
        public List<DamageType> addDamageTags = new();

#if HAS_ODIN
        [HorizontalGroup("am3", 0.5f)] [LabelText("Craft Batch Size")]
#endif
        [Min(1)] public int craftBatchSize = 10;

        // HUD
#if HAS_ODIN
        [HorizontalGroup("am3", 0.25f)] [LabelText("HUD Icon")]
#endif
        public Sprite hudIcon;

#if HAS_ODIN
        [HorizontalGroup("am3", 0.25f)] [LabelText("HUD Binding Key")]
#endif
        public string hudBindingKey = "ammo_primary";
    }

    // >>> Currency payload (POZOR: tohle bylo pryč – vracím zpět)
    [Serializable]
    public class CurrencyData
    {
#if HAS_ODIN
        [HorizontalGroup("cur1", 0.6f)] [LabelText("Currency Key")]
#endif
        public string currencyKey = "PG";

#if HAS_ODIN
        [HorizontalGroup("cur1", 0.4f)] [LabelText("Nominal Value")]
#endif
        [Min(1)] public int nominalValue = 1;

#if HAS_ODIN
        [LabelText("Notes"), MultiLineProperty]
#endif
        public string notes;
    }

    // ======= EFFECT PAYLOAD (pro Consumables) =======
    [Serializable]
    public class ItemEffect
    {
#if HAS_ODIN
        [HorizontalGroup("row", width: 0.46f)]
        [LabelWidth(120)]
#endif
        public ItemEffectType type = ItemEffectType.HealHP;

#if HAS_ODIN
        [ShowIf("@type == ItemEffectType.HealHP || " +
                "type == ItemEffectType.RestoreStamina || " +
                "type == ItemEffectType.AddArmorFlat || " +
                "type == ItemEffectType.RestoreSanity")]
        [HorizontalGroup("row", width: 0.18f)]
        [LabelText("Value"), LabelWidth(60)]
#endif
        public float value = 30f;

#if HAS_ODIN
        [ShowIf("@type == ItemEffectType.AddArmorFlat || " +
                "type == ItemEffectType.RegenHPOverTime || " +
                "type == ItemEffectType.RegenStaminaOverTime || " +
                "type == ItemEffectType.RegenSanityOverTime")]
        [HorizontalGroup("row", width: 0.18f)]
        [LabelText("Duration"), LabelWidth(70)]
#endif
        [Min(0)] public float duration = 0f;

#if HAS_ODIN
        [ShowIf("@type == ItemEffectType.RegenHPOverTime || " +
                "type == ItemEffectType.RegenStaminaOverTime || " +
                "type == ItemEffectType.RegenSanityOverTime")]
        [HorizontalGroup("row", width: 0.18f)]
        [LabelText("Per Sec"), LabelWidth(70)]
#endif
        [Min(0)] public float perSecond = 0f;

        public float TotalAmount() =>
            (type == ItemEffectType.RegenHPOverTime ||
             type == ItemEffectType.RegenStaminaOverTime ||
             type == ItemEffectType.RegenSanityOverTime)
            ? perSecond * duration
            : value;

        public override string ToString()
        {
            switch (type)
            {
                case ItemEffectType.HealHP:               return $"+{value} HP";
                case ItemEffectType.RestoreStamina:       return $"+{value} Stamina";
                case ItemEffectType.AddArmorFlat:         return $"+{value} Armor {(duration > 0 ? $"({duration}s)" : "(perm)")}";
                case ItemEffectType.RegenHPOverTime:      return $"+{perSecond}/s for {duration}s  (={TotalAmount()} HP total)";
                case ItemEffectType.RegenStaminaOverTime: return $"+{perSecond}/s for {duration}s  (={TotalAmount()} ST total)";
                case ItemEffectType.RestoreSanity:        return $"+{value} Sanity";
                case ItemEffectType.RegenSanityOverTime:  return $"+{perSecond}/s for {duration}s  (={TotalAmount()} Sanity total)";
            }
            return type.ToString();
        }
    }

    // ======= Reusable typ pro resistence (Armor) =======
    [Serializable]
    public class DamageResistance
    {
        [Tooltip("Typ poškození, na který se rezistence vztahuje.")]
        public DamageType type = DamageType.Physical;

        [Tooltip("Pevný odpočet z damage před multiplikací.")]
        public float flatReduction = 0f;

        [Tooltip("Multiplikátor poškození po odečtení flat části (0.8 = -20%).")]
        public float multiplier = 1f;
    }
}
