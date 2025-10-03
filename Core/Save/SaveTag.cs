#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Save
{
    /// <summary>
    /// Uživatelský „štítek“ – přidej na objekt/prefab a ten se bude ukládat.
    /// Zajistí SaveId + SaveAgent a přednastaví vhodné volby.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
#if HAS_ODIN
    [HideMonoScript]
    [InfoBox("Připni na objekt -> bude se ukládat. Automaticky zajistí SaveId + SaveAgent a přednastaví volby.", InfoMessageType.Info)]
#endif
    public class SaveTag : MonoBehaviour
    {
        public enum Preset { Generic, Player, Door, Enemy, NPC, StaticProp, Custom }
        public enum IdPolicy { KeepSerialized, GenerateAtRuntime }

#if HAS_ODIN
        [EnumToggleButtons]
#endif
        public Preset preset = Preset.Generic;

#if HAS_ODIN
        [FoldoutGroup("Advanced")]
        [PropertyTooltip("Pro prefaby, které spawnuješ ve více kopiích, zvol GenerateAtRuntime, aby měl každý kus unikátní GUID.")]
#endif
        public IdPolicy idPolicy = IdPolicy.KeepSerialized;

#if HAS_ODIN
        [FoldoutGroup("Advanced")]
        [LabelText("Prefab Key"), Tooltip("Klíč do SavePrefabDB pro respawn chybějících objektů.")]
#endif
        public string prefabKey;

#if HAS_ODIN
        [FoldoutGroup("Advanced")]
        [LabelText("Auto prefabKey = jméno objektu")]
#endif
        public bool autoPrefabKeyFromName = true;

        // Player refs
#if HAS_ODIN
        [FoldoutGroup("Player Refs"), ShowIf("@preset == Preset.Player"), Required]
#endif
        public Transform cameraPivot;
#if HAS_ODIN
        [FoldoutGroup("Player Refs"), ShowIf("@preset == Preset.Player")]
#endif
        public Camera playerCamera;

#if HAS_ODIN
        [Button(ButtonSizes.Medium), GUIColor(0.2f,0.8f,1f)]
#endif
        public void ApplyNow()
        {
            EnsureSaveId();
            EnsureSaveAgent();
            ApplyPreset();
            ApplyPrefabKeyIfNeeded();
        }

        void Awake()
        {
            ApplyNow();

            // IdPolicy: na runtime vygeneruj nové ID pro každou instanci (spawner)
            if (Application.isPlaying && idPolicy == IdPolicy.GenerateAtRuntime)
            {
                var sid = GetComponent<SaveId>();
                if (sid != null) sid.SetIdRuntime(Guid.NewGuid().ToString("N"));
            }
        }

#if UNITY_EDITOR
        void OnValidate() { ApplyNow(); }
#endif

        void EnsureSaveId()
        {
            var sid = GetComponent<SaveId>();
            if (!sid) sid = gameObject.AddComponent<SaveId>();

            if (!Application.isPlaying && idPolicy == IdPolicy.KeepSerialized)
                sid.EnsureId();

            // >>> Pro preset Player nastav stabilní runtime ID
            if (preset == Preset.Player)
                sid.SetIdRuntime("player");
        }


        void EnsureSaveAgent()
        {
            var agent = GetComponent<SaveAgent>();
            if (!agent) agent = gameObject.AddComponent<SaveAgent>();

            if (preset == Preset.Player)
            {
                agent.role = SaveAgent.Role.Player;
                if (!cameraPivot && playerCamera) cameraPivot = playerCamera.transform.parent;
                if (!agent.cameraPivot) agent.cameraPivot = cameraPivot;
                if (!agent.playerCamera && playerCamera) agent.playerCamera = playerCamera;
            }
            else agent.role = SaveAgent.Role.Generic;
        }

        void ApplyPreset()
        {
            var a = GetComponent<SaveAgent>(); if (!a) return;

            switch (preset)
            {
                case Preset.Generic:
                    a.captureTransform = true; a.transformSpace = SaveAgent.SpaceMode.World;
                    a.captureActiveSelf = true;
                    a.captureRigidbody = true;
                    a.captureAnimator = true; a.animatorDeepState = false;
                    a.captureNavMeshAgent = false;
                    a.captureCustomComponents = true;
                    break;
                case Preset.Player:
                    a.captureTransform = true; a.transformSpace = SaveAgent.SpaceMode.World;
                    a.captureActiveSelf = true;
                    a.captureRigidbody = true;
                    a.captureAnimator = false;
                    a.captureNavMeshAgent = false;
                    a.captureCustomComponents = true;
                    break;
                case Preset.Door:
                    a.captureTransform = true; a.transformSpace = SaveAgent.SpaceMode.Local;
                    a.captureActiveSelf = true;
                    a.captureRigidbody = false;
                    a.captureAnimator = true; a.animatorDeepState = false;
                    a.captureNavMeshAgent = false;
                    a.captureCustomComponents = true;
                    break;
                case Preset.Enemy:
                    a.captureTransform = true; a.transformSpace = SaveAgent.SpaceMode.World;
                    a.captureActiveSelf = true;
                    a.captureRigidbody = false;
                    a.captureAnimator = true; a.animatorDeepState = false;
                    a.captureNavMeshAgent = true;
                    a.captureCustomComponents = true;
                    break;
                case Preset.NPC:
                    a.captureTransform = true; a.transformSpace = SaveAgent.SpaceMode.World;
                    a.captureActiveSelf = true;
                    a.captureRigidbody = false;
                    a.captureAnimator = true; a.animatorDeepState = false;
                    a.captureNavMeshAgent = true;
                    a.captureCustomComponents = true;
                    break;
                case Preset.StaticProp:
                    a.captureTransform = true; a.transformSpace = SaveAgent.SpaceMode.World;
                    a.captureActiveSelf = true;
                    a.captureRigidbody = true;
                    a.captureAnimator = false;
                    a.captureNavMeshAgent = false;
                    a.captureCustomComponents = false;
                    break;
                case Preset.Custom:
                    // necháme nastavení na tobě (a ponecháme co je)
                    break;
            }
        }

        void ApplyPrefabKeyIfNeeded()
        {
            var s = GetComponent<SaveComponent>();
            if (!s) return;
            var key = prefabKey;
            if (string.IsNullOrEmpty(key) && autoPrefabKeyFromName)
                key = gameObject.name;
            if (!string.IsNullOrEmpty(key))
                s._ForceSetPrefabKey(key);
        }
    }
}
