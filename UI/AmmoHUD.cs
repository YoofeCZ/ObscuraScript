using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Obscurus.Items;
using Obscurus.Player;
using Obscurus.Weapons;

namespace Obscurus.UI
{
    /// Ukazuje "inMag / reserve" + ikonu munice.
    /// Viditelné pouze při equipnuté RANGED zbrani. Reaguje na WeaponChanged,
    /// IAmmoEvents.AmmoChanged a PlayerInventory.Changed (rezerva).
    [DisallowMultipleComponent]
    public class AmmoHUD : MonoBehaviour
    {
        [Header("Refs (auto)")]
        public PlayerInventory inventory;
        public ItemDatabase db;
        [Tooltip("Když nenecháš prázdné, vezme se WeaponHolder.Local.")]
        public WeaponHolder holder;

        [Header("UI")]
        public TMP_Text ammoLabel;   // "inMag / reserve"
        public Image iconImage;
        [Tooltip("Volitelně: pokud existuje, použije se ke skrývání/ukazování HUDu.")]
        public CanvasGroup group;

        [Header("Debug fallback (vypnuto v produkci)")]
        public bool   allowDebugFallback = false;
        public string debugFallbackAmmoKey = "arrow";

        [Header("Polling fallback")]
        [Tooltip("Když zbraň neposílá AmmoChanged, zkontroluj změny 1× za frame.")]
        public bool pollIfNoEvents = true;

        // cache
        RangedWeaponBase _curWeapon;
        IAmmoEvents _curWeaponEvents;
        string _curAmmoKey;
        int _lastInMag   = int.MinValue;
        int _lastReserve = int.MinValue;
        Sprite _lastIcon;

        void Awake()
        {
            if (!inventory) inventory = FindObjectOfType<PlayerInventory>(true);
            if (!db)        db        = FindObjectOfType<ItemDatabase>(true);
            if (!holder)    holder    = WeaponHolder.Local ?? FindObjectOfType<WeaponHolder>(true);
            if (!group)     group     = GetComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            WeaponHolder.LocalChanged += OnLocalHolderChanged;
            BindHolder(holder ?? WeaponHolder.Local);

            if (inventory) inventory.Changed += RefreshNow;
            // defaultně skryj, dokud něco nepřijde
            SetVisible(false);
            RefreshNow();
        }

        void OnDisable()
        {
            WeaponHolder.LocalChanged -= OnLocalHolderChanged;
            UnbindHolder();
            if (inventory) inventory.Changed -= RefreshNow;
        }

        void Update()
        {
            if (!pollIfNoEvents) return;
            if (_curWeapon == null) return;              // nic → nic nepollovat
            if (_curWeaponEvents != null) return;        // máme eventy → nepolluj
            RefreshIfChanged();
        }

        // ---------- Binding ----------

        void OnLocalHolderChanged(WeaponHolder h)
        {
            BindHolder(h);
            RefreshNow();
        }

        void BindHolder(WeaponHolder h)
        {
            if (holder == h) return;
            UnbindHolder();
            holder = h;
            if (holder) holder.WeaponChanged += OnWeaponChanged;
        }

        void UnbindHolder()
        {
            if (holder) holder.WeaponChanged -= OnWeaponChanged;
            holder = null;
            BindWeapon(null);
        }

        // WeaponHolder.WeaponChanged: Action<IWeapon>
        void OnWeaponChanged(IWeapon w) => BindWeapon(w as RangedWeaponBase);

        void BindWeapon(RangedWeaponBase w)
        {
            // odpoj starou
            if (_curWeaponEvents != null)
            {
                _curWeaponEvents.AmmoChanged -= RefreshNow;
                _curWeaponEvents = null;
            }
            _curWeapon = w;

            // připoj novou (pokud umí eventy)
            if (_curWeapon is IAmmoEvents evSrc)
            {
                _curWeaponEvents = evSrc;
                _curWeaponEvents.AmmoChanged += RefreshNow;
            }

            // reset cache
            _lastInMag = int.MinValue;
            _lastReserve = int.MinValue;
            _curAmmoKey = null;
            _lastIcon = null;

            RefreshNow();
        }

        // ---------- Refresh ----------

        void RefreshNow() => RefreshIfChanged(force: true);

        void RefreshIfChanged(bool force = false)
        {
            if (!inventory || !db)
            {
                SetVisible(false);
                return;
            }

            // vyber zbraň: jen RANGED; melee/null → schovat
            var weapon = _curWeapon ?? (holder?.Current as RangedWeaponBase);
            if (weapon == null && allowDebugFallback == false)
            {
                SetVisible(false);
                return;
            }

            // klíč munice – pouze ze zbraně; bez ní jen volitelný DEBUG fallback
            string key = weapon ? weapon.weaponDef?.weapon?.ammoKey : debugFallbackAmmoKey;
            if (string.IsNullOrEmpty(key))
            {
                SetVisible(false);
                return;
            }

            int inMag   = (weapon && weapon.HasMagazine) ? weapon.InMagazine : 0;
            int reserve = inventory.GetAmmoReserve(key);

            bool keyChanged = (_curAmmoKey != key);

            if (force || keyChanged || inMag != _lastInMag || reserve != _lastReserve)
            {
                // ikona jen při změně key
                if (keyChanged)
                {
                    _curAmmoKey = key;
                    _lastIcon = null;
                    var ammoDef = db.FindAmmoByKey(key);
                    if (ammoDef && ammoDef.ammo != null) _lastIcon = ammoDef.ammo.hudIcon;
                }

                Draw(inMag, reserve, _lastIcon);
                _lastInMag = inMag;
                _lastReserve = reserve;
            }

            SetVisible(true);
        }

        // ---------- UI helpers ----------

        void Draw(int inMag, int reserve, Sprite icon)
        {
            if (ammoLabel) ammoLabel.text = $"{inMag}/{reserve}";
            if (iconImage)
            {
                if (iconImage.sprite != icon) iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }
        }

        void SetVisible(bool v)
        {
            if (group)
            {
                group.alpha = v ? 1f : 0f;
                group.interactable   = v;
                group.blocksRaycasts = v;
            }
            else
            {
                if (ammoLabel) ammoLabel.gameObject.SetActive(v);
                if (iconImage) iconImage.gameObject.SetActive(v);
            }
        }
    }
}
