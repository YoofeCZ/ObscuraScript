// Assets/Obscurus/Scripts/Core/Pooling/ComponentPool.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Obscurus.Core.Pooling
{
    public interface IPoolable
    {
        void OnRent();    // aktivace z poolu
        void OnReturn();  // reset před vrácením do poolu
    }

    public class ComponentPool<T> where T : Component
    {
        readonly T prefab;
        readonly Transform parent;
        readonly ObjectPool<T> pool;

        public ComponentPool(T prefab, int defaultSize = 16, int maxSize = 256, Transform parent = null)
        {
            this.prefab = prefab;
            this.parent = parent;

            pool = new ObjectPool<T>(
                createFunc: () => Object.Instantiate(prefab, parent),
                actionOnGet:  (inst) => {
                    inst.gameObject.SetActive(true);
                    if (inst is IPoolable p) p.OnRent();
                },
                actionOnRelease: (inst) => {
                    if (inst is IPoolable p) p.OnReturn();
                    inst.gameObject.SetActive(false);
                },
                actionOnDestroy: (inst) => { if (inst) Object.Destroy(inst.gameObject); },
                defaultCapacity: defaultSize,
                maxSize: maxSize
            );

            // Přednabij (volitelné)
            var tmp = new List<T>(defaultSize);
            for (int i = 0; i < defaultSize; i++) tmp.Add(Get());
            foreach (var t in tmp) Release(t);
        }

        public T Get() => pool.Get();
        public void Release(T inst) => pool.Release(inst);
    }
}