#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Save
{
#if HAS_ODIN
    [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
#endif
    [CreateAssetMenu(fileName = "SavePrefabDB", menuName = "Obscurus/Save/Prefab DB")]
    public class SavePrefabDB : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
#if HAS_ODIN
            [VerticalGroup("row"), LabelWidth(70)]
#endif
            public string key;
#if HAS_ODIN
            [VerticalGroup("row"), LabelWidth(70)]
#endif
            public GameObject prefab;
        }

#if HAS_ODIN
        [TableList(IsReadOnly = false, AlwaysExpanded = true)]
#endif
        [SerializeField] List<Entry> entries = new();

        public GameObject Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].key == key) return entries[i].prefab;
            return null;
        }
    }
}