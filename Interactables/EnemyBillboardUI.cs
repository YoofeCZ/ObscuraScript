// Assets/Obscurus/Scripts/AI/EnemyBillboardUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Obscurus.AI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Obscurus/AI/Enemy Billboard UI")]
    public class EnemyBillboardUI : MonoBehaviour
    {
        [Header("Refs")]
        public EnemyStats stats;
        public CanvasGroup canvasGroup;

        [Header("UI Root")]
        [Tooltip("Transform, který se má fyzicky posouvat/otáčet (většinou Canvas). Když necháš prázdné, zkusí se najít Canvas v potomcích; jinak použije tento objekt.")]
        public Transform driveTransform;

        [Header("HP UI")]
        public Slider hpSlider;
        public TMP_Text hpText;

        [Header("Armor UI")]
        public Slider armorSlider;
        public TMP_Text armorText;

        [Header("Zobrazení")]
        public bool showOnDamage = true;
        public float showDuration = 2.0f;
        public float fadeSpeed = 6f;

        [Header("Billboard")]
        public bool faceCamera = true;
        public Vector3 worldOffset = new Vector3(0, 2.0f, 0);

        float visibleUntil;
        Transform anchor;     // kam se to má „lepit“ (většinou parent / nepřítel)

        void Awake()
        {
            if (!stats) stats = GetComponentInParent<EnemyStats>();

            if (!driveTransform)
            {
                var c = GetComponentInChildren<Canvas>(true);
                driveTransform = c ? c.transform : transform;
            }

            if (!canvasGroup)
                canvasGroup = driveTransform.GetComponentInChildren<CanvasGroup>(true);

            anchor = stats ? stats.transform : (transform.parent ? transform.parent : transform);
        }

        void OnEnable()
        {
            if (stats)
            {
                stats.OnHealthChanged.AddListener(OnHealthChanged);
                stats.OnArmorChanged.AddListener(OnArmorChanged);
                stats.OnDamaged.AddListener(OnDamaged);
                stats.OnDied.AddListener(OnDied);
            }

            // init UI
            if (stats)
            {
                OnHealthChanged(stats.Health01 * stats.maxHealth, stats.maxHealth);
                OnArmorChanged(stats.Armor01 * stats.maxArmor, stats.maxArmor);
            }

            if (canvasGroup) canvasGroup.alpha = showOnDamage ? 0f : 1f;

            // jistota: canvas musí být World Space
            var canvas = driveTransform.GetComponentInChildren<Canvas>(true);
            if (canvas && canvas.renderMode != RenderMode.WorldSpace)
                canvas.renderMode = RenderMode.WorldSpace;
        }

        void OnDisable()
        {
            if (stats)
            {
                stats.OnHealthChanged.RemoveListener(OnHealthChanged);
                stats.OnArmorChanged.RemoveListener(OnArmorChanged);
                stats.OnDamaged.RemoveListener(OnDamaged);
                stats.OnDied.RemoveListener(OnDied);
            }
        }

        void LateUpdate()
        {
            if (!driveTransform) return;

            // pozice nad hlavou (kotva = nepřítel/parent)
            if (anchor)
                driveTransform.position = anchor.position + worldOffset;

            // billboard – fallback i když Camera.main není
            if (faceCamera)
            {
                var cam = Camera.main ? Camera.main : FindObjectOfType<Camera>();
                if (cam)
                {
                    // Otoč čelem ke kameře (Canvas má „předek“ v +Z)
                    driveTransform.LookAt(cam.transform, Vector3.up);
                    // ať kouká „na kameru“, ne „od kamery“
                    driveTransform.Rotate(0f, 180f, 0f, Space.Self);
                }
            }

            // fade out po showOnDamage
            if (canvasGroup && showOnDamage)
            {
                float target = Time.time < visibleUntil ? 1f : 0f;
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.deltaTime);
            }
        }

        void OnHealthChanged(float current, float max)
        {
            if (hpSlider) hpSlider.value = max > 0f ? current / max : 0f;
            if (hpText)   hpText.text   = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        void OnArmorChanged(float current, float max)
        {
            if (armorSlider) armorSlider.value = max > 0f ? current / max : 0f;
            if (armorText)   armorText.text   = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        void OnDamaged()
        {
            if (showOnDamage) visibleUntil = Time.time + showDuration;
        }

        void OnDied()
        {
            visibleUntil = Time.time - 1f;
        }
    }
}
