// Assets/Obscurus/Scripts/Core/Pooling/ProjectilePool.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Obscurus.Weapons;

namespace Obscurus.Core.Pooling
{
    public static class ProjectilePool
    {
        static readonly Dictionary<PooledProjectile, ObjectPool<PooledProjectile>> _pools = new();

        public static PooledProjectile Get(PooledProjectile prefab, Transform parent = null, int defaultSize = 32, int maxSize = 256)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool<PooledProjectile>(
                    createFunc: () => Object.Instantiate(prefab, parent),
                    actionOnGet: (p) => { p.ownerPool = pool; (p as IPoolable)?.OnRent(); },
                    actionOnRelease: (p) => { (p as IPoolable)?.OnReturn(); },
                    actionOnDestroy: (p) => { if (p) Object.Destroy(p.gameObject); },
                    defaultCapacity: defaultSize,
                    maxSize: maxSize
                );

                // Prewarm
                var tmp = new PooledProjectile[defaultSize];
                for (int i = 0; i < defaultSize; i++) tmp[i] = pool.Get();
                for (int i = 0; i < defaultSize; i++) pool.Release(tmp[i]);

                _pools[prefab] = pool;
            }

            var inst = pool.Get();
            inst.ownerPool = pool;
            return inst;
        }
    }
}