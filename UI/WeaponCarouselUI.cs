// Assets/Obscurus/UI/WeaponCarouselUI.cs
#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Obscurus.Items;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Obscurus.UI
{
    /// <summary>
    /// Weapon carousel, který čte ItemDefinition z ItemDatabase
    /// a tlačítkem UPGRADE upravuje hodnoty v těchto definicích.
    /// Upgrade teď stojí Scrap z PlayerInventory.
    /// </summary>
    public class WeaponCarouselUI : MonoBehaviour
    {
        // ----------------- CONFIG -----------------
        [Serializable]
        public class UpgradeRule
        {
#if HAS_ODIN
            [Sirenix.OdinInspector.HorizontalGroup("r1", 0.25f), Sirenix.OdinInspector.LabelText("DMG +")]
#endif
            public float damageStep = 1f;

#if HAS_ODIN
            [Sirenix.OdinInspector.HorizontalGroup("r1", 0.25f), Sirenix.OdinInspector.LabelText("AtkSpd +")]
#endif
            public float attackSpeedStep = 0.1f;

#if HAS_ODIN
            [Sirenix.OdinInspector.HorizontalGroup("r1", 0.25f), Sirenix.OdinInspector.LabelText("Crit% +")]
#endif
            public float critPercentStep = 1f;              // 0–100

#if HAS_ODIN
            [Sirenix.OdinInspector.HorizontalGroup("r1", 0.25f), Sirenix.OdinInspector.LabelText("Allow Mag")]
#endif
            public bool allowMagazineUpgrade = true;

#if HAS_ODIN
            [Sirenix.OdinInspector.ShowIf(nameof(allowMagazineUpgrade))]
            [Sirenix.OdinInspector.HorizontalGroup("r2", 0.4f), Sirenix.OdinInspector.LabelText("Mag +")]
#endif
            public int magazineStep = 1;

            public override string ToString() =>
                $"DMG+{damageStep}, SPD+{attackSpeedStep}, CRIT+{critPercentStep}%, MAG{(allowMagazineUpgrade ? $"+{magazineStep}" : "—")}";
        }

        [Serializable]
        public class PerWeaponOverride
        {
#if HAS_ODIN
            [Sirenix.OdinInspector.HorizontalGroup("row", width: 0.45f), Sirenix.OdinInspector.LabelText("Weapon")]
#endif
            public ItemDefinition weapon;

#if HAS_ODIN
            [Sirenix.OdinInspector.HorizontalGroup("row"), Sirenix.OdinInspector.InlineProperty, Sirenix.OdinInspector.HideLabel]
#endif
            public UpgradeRule rule = new UpgradeRule();
        }

        // ----------------- DATA SOURCE -----------------
#if HAS_ODIN
        [Sirenix.OdinInspector.BoxGroup("Data Source", true, true)]
        [Sirenix.OdinInspector.LabelText("Database")]
#endif
        public ItemDatabase database;

#if HAS_ODIN
        [Sirenix.OdinInspector.BoxGroup("Data Source")]
        [Sirenix.OdinInspector.LabelText("Auto Populate From DB")]
#endif
        public bool autoPopulateOnEnable = true;

#if HAS_ODIN
        [Sirenix.OdinInspector.BoxGroup("Data Source")]
        [Sirenix.OdinInspector.LabelText("Include Ranged")]
#endif
        public bool includeRanged = true;

#if HAS_ODIN
        [Sirenix.OdinInspector.BoxGroup("Data Source")]
        [Sirenix.OdinInspector.LabelText("Include Melee")]
#endif
        public bool includeMelee = false;

#if HAS_ODIN
        [Sirenix.OdinInspector.BoxGroup("Data Source")]
        [Sirenix.OdinInspector.LabelText("Ammo Key Filter (optional)")]
#endif
        public string filterAmmoKey = "";

#if HAS_ODIN
        [Sirenix.OdinInspector.Button(Sirenix.OdinInspector.ButtonSizes.Medium)]
        private void PopulateFromDatabaseButton() => PopulateFromDatabase();
#endif

        // ----------------- UPGRADE SETTINGS -----------------
#if HAS_ODIN
        [Sirenix.OdinInspector.BoxGroup("Upgrade Rules", true, true)]
        [Sirenix.OdinInspector.LabelText("Default Rule"), Sirenix.OdinInspector.InlineProperty, Sirenix.OdinInspector.HideLabel]
#endif
        public UpgradeRule defaultRule = new UpgradeRule();

#if HAS_ODIN
        [Sirenix.OdinInspector.BoxGroup("Upgrade Rules")]
        [Sirenix.OdinInspector.LabelText("Per-Weapon Overrides")]
        [Sirenix.OdinInspector.TableList(AlwaysExpanded = true)]
#endif
        public List<PerWeaponOverride> perWeapon = new();

        // ----------------- COST -----------------
        [Header("Costs")]
        [Tooltip("Kolik stojí jeden upgrade ve Scrapu.")]
        public int scrapCost = 5;

        [Tooltip("Ukázat cenu v textu tlačítka (např. 'UPGRADE (5)')")]
        public bool showScrapCostOnButton = true;

        [Tooltip("Barva labelu, když není dost Scrapu.")]
        public Color insufficientColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);

        [Tooltip("Explicitní reference na inventář (když prázdné, najde se automaticky ve scéně).")]
        public Obscurus.Player.PlayerInventory playerInventory;

        // ----------------- RUNTIME STATE -----------------
        [Serializable] public class Entry { public ItemDefinition def; }

        [Header("Runtime (filled automatically)")]
        public List<Entry> entries = new();

        [Header("UI References")]
        public Button btnLeft;
        public Button btnRight;
        public Image weaponImage;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI dmgText;
        public TextMeshProUGUI fireRateText;
        public TextMeshProUGUI etcText;
        public Button upgradeButton;
        public RectTransform imageArea;
        public RectTransform infoArea;

        [Header("Behaviour")]
        [Tooltip("Nechává šipky vždy viditelné.")]
        public bool alwaysShowArrows = true;
        [Tooltip("Přejde z poslední položky zpět na první a naopak.")]
        public bool loopNavigation = true;

        // pro staré scény (ignorováno, když alwaysShowArrows = true)
        public bool hideArrowsOnEdges = true;

        [Header("Animace")]
        public float animDuration = 0.15f;

        private int index = 0;
        private Coroutine animCo;
        private CanvasGroup imgCG, infoCG;
        private TextMeshProUGUI upgradeLabel;
        private Color upgradeLabelDefaultColor = Color.white;

        // ----------------- LIFECYCLE -----------------
        private void Awake()
        {
            if (btnLeft)  btnLeft.onClick.AddListener(() => Navigate(-1));
            if (btnRight) btnRight.onClick.AddListener(() => Navigate(+1));
            if (upgradeButton) upgradeButton.onClick.AddListener(OnUpgradeClicked);

            if (!playerInventory)
                playerInventory = FindObjectOfType<Obscurus.Player.PlayerInventory>();

            if (upgradeButton)
            {
                upgradeLabel = upgradeButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (upgradeLabel) upgradeLabelDefaultColor = upgradeLabel.color;
            }
        }

        private void OnEnable()
        {
            imgCG = EnsureCanvasGroup(imageArea);
            infoCG = EnsureCanvasGroup(infoArea);

            if (playerInventory != null)
                playerInventory.Changed += OnInventoryChanged;

            if (autoPopulateOnEnable) PopulateFromDatabase();
            ApplyIndex(instant: true, dir: 0);
        }

        private void OnDisable()
        {
            if (playerInventory != null)
                playerInventory.Changed -= OnInventoryChanged;
        }

        private void OnInventoryChanged()
        {
            UpdateUpgradeInteractivity();
        }

        // ----------------- POPULATE -----------------
        public void PopulateFromDatabase()
        {
            entries.Clear();

            if (!database)
            {
                Debug.LogWarning("[WeaponCarouselUI] Database not set.");
                return;
            }

            foreach (var it in database.EnumerateByType(ItemType.Weapon))
            {
                if (!it) continue;
                if (it.weaponKind == WeaponKind.Ranged && !includeRanged) continue;
                if (it.weaponKind == WeaponKind.Melee  && !includeMelee)  continue;

                if (!string.IsNullOrEmpty(filterAmmoKey) &&
                    it.weaponKind == WeaponKind.Ranged &&
                    (it.ranged == null || !string.Equals(it.ranged.ammoKey, filterAmmoKey, StringComparison.Ordinal)))
                {
                    continue;
                }

                entries.Add(new Entry { def = it });
            }

            index = Mathf.Clamp(index, 0, Mathf.Max(0, entries.Count - 1));
        }

        // ----------------- NAVIGATION -----------------
        public void Next() => Navigate(+1);
        public void Prev() => Navigate(-1);

        private void Navigate(int dir)
        {
            if (entries == null || entries.Count == 0) return;

            int count = entries.Count;
            int newIndex;

            if (loopNavigation && count > 0)
            {
                newIndex = ((index + dir) % count + count) % count; // true modulo
            }
            else
            {
                newIndex = Mathf.Clamp(index + dir, 0, count - 1);
            }

            if (newIndex == index) return;

            index = newIndex;
            ApplyIndex(instant: false, dir: dir >= 0 ? +1 : -1);
        }

        private void ApplyIndex(bool instant, int dir)
        {
            bool hasAny = entries != null && entries.Count > 0;
            bool canNav = hasAny && entries.Count > 1;

            // Šipky – vždy viditelné (pokud je aspoň něco k zobrazení)
            if (btnLeft)
            {
                if (alwaysShowArrows) btnLeft.gameObject.SetActive(hasAny);
                else if (hideArrowsOnEdges) btnLeft.gameObject.SetActive(hasAny && index > 0);
                else btnLeft.interactable = (hasAny && index > 0);
                btnLeft.interactable = canNav;
            }

            if (btnRight)
            {
                if (alwaysShowArrows) btnRight.gameObject.SetActive(hasAny);
                else if (hideArrowsOnEdges) btnRight.gameObject.SetActive(hasAny && index < entries.Count - 1);
                else btnRight.interactable = (hasAny && index < entries.Count - 1);
                btnRight.interactable = canNav;
            }

            if (!hasAny) { ClearUI(); UpdateUpgradeInteractivity(); return; }

            if (instant)
            {
                Bind(CurrentDef());
                ResetAnimState();
            }
            else
            {
                if (animCo != null) StopCoroutine(animCo);
                animCo = StartCoroutine(CrossFadeCo());
            }

            UpdateUpgradeInteractivity();
        }

        private void ResetAnimState()
        {
            if (imgCG)  imgCG.alpha  = 1f;
            if (infoCG) infoCG.alpha = 1f;
        }

        // Fade-only, žádný posun RectTransformů v layoutu
        private IEnumerator CrossFadeCo()
        {
            float dur = Mathf.Max(0.01f, animDuration);

            // Fade out
            yield return Tween(dur, t =>
            {
                if (imgCG)  imgCG.alpha  = 1f - t;
                if (infoCG) infoCG.alpha = 1f - t;
            });

            // Změna obsahu
            Bind(CurrentDef());

            // Fade in
            yield return Tween(dur, t =>
            {
                if (imgCG)  imgCG.alpha  = t;
                if (infoCG) infoCG.alpha = t;
            });

            animCo = null;
        }

        // ----------------- BIND -----------------
        private ItemDefinition CurrentDef() =>
            (entries != null && entries.Count > 0 && index >= 0 && index < entries.Count) ? entries[index].def : null;

        private void Bind(ItemDefinition def)
        {
            if (!def)
            {
                ClearUI();
                return;
            }

            // Icon + Name
            if (weaponImage) weaponImage.sprite = def.icon;
            if (nameText)    nameText.text = def.Name;

            // Stats
            if (def.weaponKind == WeaponKind.Ranged && def.ranged != null)
            {
                var r = def.ranged;
                if (dmgText)      dmgText.text      = $"DMG: {r.baseDamage:0.#}";
                if (fireRateText) fireRateText.text = $"ROF: {r.attackSpeed:0.##}/s";
                if (etcText)
                {
                    string mag = r.magazineSize > 0 ? $" • MAG: {r.magazineSize}" : "";
                    string crit= $" • CRIT: {r.critChance:0.#}%";
                    etcText.text = $"{r.ammoKey}{mag}{crit}";
                }
            }
            else if (def.weaponKind == WeaponKind.Melee && def.melee != null)
            {
                var m = def.melee;
                if (dmgText)      dmgText.text      = $"DMG: {m.baseDamage:0.#}";
                if (fireRateText) fireRateText.text = $"CD: {m.attackCooldown:0.##}s";
                if (etcText)      etcText.text      = $"CRIT: {m.critChance:0.#}% • RNG: {m.range:0.#}";
            }
            else
            {
                if (dmgText) dmgText.text = "";
                if (fireRateText) fireRateText.text = "";
                if (etcText) etcText.text = "";
            }
        }

        private void ClearUI()
        {
            if (weaponImage) weaponImage.sprite = null;
            if (nameText)    nameText.text = "—";
            if (dmgText)     dmgText.text = "";
            if (fireRateText)fireRateText.text = "";
            if (etcText)     etcText.text = "";
        }

        // ----------------- UPGRADE -----------------
        private UpgradeRule GetRuleFor(ItemDefinition def)
        {
            if (def == null) return defaultRule;
            for (int i = 0; i < perWeapon.Count; i++)
            {
                var ow = perWeapon[i];
                if (ow != null && ow.weapon == def && ow.rule != null)
                    return ow.rule;
            }
            return defaultRule;
        }

        private void OnUpgradeClicked()
        {
            var def = CurrentDef();
            if (!def) return;

            // 1) Zkontroluj a odečti Scrap
            if (playerInventory != null)
            {
                // ResourceKey je v projektu – předpokládáme enum s hodnotou Scrap
                if (!playerInventory.SpendResource(ResourceKey.Scrap, scrapCost))
                {
                    Debug.Log("[WeaponCarouselUI] Not enough Scrap for upgrade.");
                    UpdateUpgradeInteractivity(); // pro jistotu refresh stavu tlačítka
                    return;
                }
            }

            // 2) Aplikuj upgrade
            var rule = GetRuleFor(def);

            if (def.weaponKind == WeaponKind.Ranged && def.ranged != null)
            {
                def.ranged.baseDamage    = Mathf.Max(0f, def.ranged.baseDamage + rule.damageStep);
                def.ranged.attackSpeed   = Mathf.Max(0f, def.ranged.attackSpeed + rule.attackSpeedStep);
                def.ranged.critChance    = Mathf.Clamp(def.ranged.critChance + rule.critPercentStep, 0f, 100f);

                if (rule.allowMagazineUpgrade && def.ranged.magazineSize > 0 && rule.magazineStep != 0)
                {
                    def.ranged.magazineSize = Mathf.Max(0, def.ranged.magazineSize + rule.magazineStep);
                }
            }
            else if (def.weaponKind == WeaponKind.Melee && def.melee != null)
            {
                def.melee.baseDamage     = Mathf.Max(0f, def.melee.baseDamage + rule.damageStep);
                float cd = Mathf.Max(0.05f, def.melee.attackCooldown - rule.attackSpeedStep);
                def.melee.attackCooldown = cd;
                def.melee.critChance     = Mathf.Clamp(def.melee.critChance + rule.critPercentStep, 0f, 100f);
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(def);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
#endif
            // 3) Refresh UI
            Bind(def);
            UpdateUpgradeInteractivity();
        }

        // ----------------- HELPERS -----------------
        private void UpdateUpgradeInteractivity()
        {
            bool hasAny = entries != null && entries.Count > 0;
            bool hasScrap = playerInventory == null || playerInventory.GetResource(ResourceKey.Scrap) >= scrapCost;

            if (upgradeButton) upgradeButton.interactable = hasAny && hasScrap;

            if (upgradeLabel)
            {
                // text s cenou
                if (showScrapCostOnButton)
                    upgradeLabel.text = $"UPGRADE ({scrapCost})";
                else
                    upgradeLabel.text = "UPGRADE";

                // barva podle prostředků
                upgradeLabel.color = hasScrap ? upgradeLabelDefaultColor : insufficientColor;
            }
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform rt)
        {
            if (!rt) return null;
            var cg = rt.GetComponent<CanvasGroup>();
            if (!cg) cg = rt.gameObject.AddComponent<CanvasGroup>();
            return cg;
        }

        private static IEnumerator Tween(float duration, Action<float> step)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                step?.Invoke(k);
                yield return null;
            }
            step?.Invoke(1f);
        }
    }
}
