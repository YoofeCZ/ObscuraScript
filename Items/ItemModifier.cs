using System;
using UnityEngine;

namespace Obscurus.Items
{
    /// <summary> Jednoduchý key→value modifikátor (např. "Damage","+12"). </summary>
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