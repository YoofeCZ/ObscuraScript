// Assets/Obscurus/Scripts/Weapons/ChainLightningEffectDef.cs
using UnityEngine;
using Obscurus.Effects;

namespace Obscurus.Weapons
{
    [CreateAssetMenu(fileName = "ChainLightningEffect", menuName = "Obscurus/Effects/Chain Lightning (AOE)")]
    public class ChainLightningEffectDef : ScriptableObject
    {
        [Header("AOE (kopule)")]
        [Tooltip("DMG za jeden tick AOE (všichni uvnitř).")]
        public float aoeDamage = 20f;

        [Tooltip("Jak dlouho kopule běží (s).")]
        public float duration = 3f;

        [Tooltip("Poloměr kopule (m).")]
        public float radius = 6f;

        [Tooltip("Interval ticků poškození (s).")]
        public float tickInterval = 0.25f;

        [Header("Electrized (DoT efekt na cílech)")]
        public ElectrizedEffectDef electrized;           // definice DoT
        [Tooltip("Kolik stacků Electrized přidat každý AOE tick (na každého uvnitř).")]
        public int stacksPerTick = 1;

        [Header("Targeting")]
        public LayerMask enemyMask = ~0;
        public string enemyTag = "Enemy";

        [Header("VFX Prefaby")]
        [Tooltip("Prefab KOPULE (musí mít komponentu ShockField). Ten se spawne na zemi v místě zásahu.")]
        public GameObject aoePrefab;

        [Tooltip("Loop efekt, který se připne na každého nepřítele uvnitř kopule.")]
        public GameObject activePrefab;

        [Tooltip("Jednorázový záblesk při dopadu projektilu.")]
        public GameObject impactPrefab;
    }
}