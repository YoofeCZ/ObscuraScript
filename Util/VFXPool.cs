// Assets/Obscurus/Scripts/Core/Pooling/VFXPool.cs
using System.Collections.Generic;
using UnityEngine;
using Obscurus.Core.Pooling;

namespace Obscurus.VFX
{
    public static class VFXPool
    {
        static readonly Dictionary<GameObject, ComponentPool<PooledVFX>> _pools = new();

        static ComponentPool<PooledVFX> GetPool(GameObject prefab)
        {
            if (!prefab) return null;
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                var vfx = prefab.GetComponent<PooledVFX>();
                if (!vfx)
                {
                    // dovolíme přidat runtime – ale lepší je dát PooledVFX na prefab v editoru
                    vfx = prefab.AddComponent<PooledVFX>();
                }
                pool = new ComponentPool<PooledVFX>(vfx, defaultSize: 24, maxSize: 256);
                _pools[prefab] = pool;
            }
            return pool;
        }

        public static PooledVFX SpawnOneShot(GameObject prefab, Vector3 pos, Quaternion rot, Transform follow = null, float ttl = -1f)
        {
            var pool = GetPool(prefab);
            if (pool == null) return null;
            var inst = pool.Get();
            inst.PlayOneShot(pos, rot, follow, ttl);
            return inst;
        }

        public static PooledVFX SpawnLoop(GameObject prefab, Vector3 pos, Quaternion rot, Transform follow = null)
        {
            var pool = GetPool(prefab);
            if (pool == null) return null;
            var inst = pool.Get();
            inst.PlayLoop(pos, rot, follow);
            return inst;
        }

        public static void Release(PooledVFX inst)
        {
            if (!inst) return;
            var prefab = inst.GetComponent<PooledSource>()?.sourcePrefab;
            // PooledSource je volitelná utilita – když ji nechceš, drž si mapu jinak.
            foreach (var kv in _pools)
            {
                // hrubá O(n) – v praxi stačí; nebo si ulož referenci na pool do PooledVFX.
                // Jednodušší varianta: přidej do PooledVFX public ComponentPool<PooledVFX> ownerPool;
            }
            inst.gameObject.SetActive(false);
        }
    }

    // (Volitelné) – když potřebuješ vědět původní prefab pro Release lookup
    public class PooledSource : MonoBehaviour
    {
        public GameObject sourcePrefab;
        void Awake()
        {
            if (!sourcePrefab) sourcePrefab = gameObject; // když je to přímo prefab
        }
    }
}
