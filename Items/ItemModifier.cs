using System;
using UnityEngine;

namespace Obscurus.Items
{
    /// <summary>
    /// Jednoduchý key→value modifikátor (např. "Damage","+12").
    /// 
    /// ✅ Podporované klíče (aktuálně čtené v kódu zbraní):
    ///  - "damage", "basedamage"            → +X k baseDamage (flat)
    ///  - "damage_mult", "damagemult"       → *X násobek damage (mult)
    ///  - "attackspeed"                     → +X APS
    ///  - "attackspeed_mult"                → *X APS
    ///  - "crit", "critchance"              → +X % crit šance
    ///  - "magazine", "magazinesize"        → +X velikost zásobníku (int)
    ///  - "magazine_mult", "magazinesize_mult" → *X velikost zásobníku
    ///  - "reload", "reloadsec"             → +X s reload
    ///  - "reload_mult", "reloadsec_mult"   → *X reload
    ///  - "shotsperuse"                     → +X výstřelů na použití (int)
    ///  - "startloaded"                     → +X nábojů naložených na startu (int)
    ///
    /// 🔧 Doporučené “rozšířené” klíče (můžeš začít používat do DB – na straně runtime si je pak načteš):
    ///  - "projectile_speed", "projectile_lifetime", "projectile_gravity" (0/1)
    ///  - "spread_deg", "pellet_count", "pellet_dmg_mult", "split_damage_pellets" (0/1)
    ///  - "penetration", "ricochet_chance"
    ///  - "headshot_multiplier", "can_headshot" (0/1)
    ///  - "aoe_enabled" (0/1), "aoe_radius", "aoe_duration", "aoe_tick", "aoe_damage",
    ///    "aoe_splash_delay", "aoe_dot_dps", "aoe_dot_dur"
    ///  - "ammo_dmg_mult", "ammo_speed_mult", "ammo_accuracy_mult"
    /// 
    /// Tyto “rozšířené” klíče slovník zatím nikde explicitně nečte – jsou určené,
    /// abys je měl v DB připravené a mohl si je tahat v konkrétních zbraních/projektilu/AoE.
    /// </summary>
    [Serializable]
    public struct ItemModifier
    {
        [Tooltip("Klíč statistiky (např. Damage, Speed, CritChance, Armor, …)")]
        public string key;

        [Tooltip("Hodnota (kladná nebo záporná).")]
        public float value;

        public ItemModifier(string key, float value)
        {
            this.key = key; this.value = value;
        }

        public override string ToString() => string.IsNullOrEmpty(key) ? "—" : $"{key}: {value:+0.##;-0.##;0}";
    }
}
