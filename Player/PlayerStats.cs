using UnityEngine;

namespace Obscurus.Player
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Základní staty")]
        public float maxHP = 100f;
        public float maxStamina = 100f;

        [Header("Bonusy z upgradu")]
        public float damageBonus = 0f;     // +poškození všech zbraní (flat %)
        public float moveSpeedBonus = 0f;  // +% pohybu (0.05 = +5 %)
        public float fireRateBonus = 0f;   // +% rychlost střelby

        // Substance větve – jednoduché přírůstky
        public void AddVitriolTier()    { damageBonus   += 5f;  fireRateBonus += 0.02f; }
        public void AddAurumTier()      { maxHP         += 10f; }
        public void AddMercuriusTier()  { moveSpeedBonus+= 0.05f; fireRateBonus += 0.03f; }
    }
}