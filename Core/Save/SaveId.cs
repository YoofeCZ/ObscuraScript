#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Save
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class SaveId : MonoBehaviour
    {
#if HAS_ODIN
        [ShowInInspector, ReadOnly, LabelText("GUID")]
#endif
        [SerializeField] string id;

        public string Id => id;

#if HAS_ODIN
        [Button(ButtonSizes.Small), GUIColor(0.2f,0.8f,1f)]
#endif
        void GenerateNew() => id = System.Guid.NewGuid().ToString("N");

        void Reset() { EnsureId(); }
#if UNITY_EDITOR
        void OnValidate() { if (!Application.isPlaying) EnsureId(); }
#endif
        public void EnsureId()
        {
            if (string.IsNullOrEmpty(id))
                id = System.Guid.NewGuid().ToString("N");
        }

        /// <summary>Runtime přepsání ID (pro spawnované kopie).</summary>
        public void SetIdRuntime(string newId) => id = newId;
    }
}