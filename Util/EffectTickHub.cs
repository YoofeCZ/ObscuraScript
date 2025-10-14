// Assets/Obscurus/Scripts/Core/Effects/EffectTickHub.cs
using System.Collections.Generic;
using UnityEngine;
using Obscurus.AI;

namespace Obscurus.Effects
{
    public static class EffectTickHub
    {
        class Entry
        {
            public EnemyStats stats;
            public Transform socket;
            public float expireAt;
            public float nextImpactAt;
        }

        static readonly List<Entry> _entries = new(256);
        static float _acc;

        // zavolej při ApplyEffect na každém nodu (1x)
        public static void Register(EnemyStats stats, Transform socket, float expireAt)
        {
            if (!stats || stats.IsDead) return;

            // nenoste duplikáty – jednoduchý merge podle (stats, req.chainID)
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.stats == stats)
                {
                    e.expireAt = Mathf.Max(e.expireAt, expireAt);
                    return;
                }
            }

            _entries.Add(new Entry {
                stats = stats,
                socket = socket,
                expireAt = expireAt,
                nextImpactAt = 0f
            });
        }

        // zavolej z nějakého globálního updateru (např. GameManager) v Update()
        public static void Tick(float dt)
        {
            const float tickInterval = 0.5f;
            _acc += dt;
            if (_acc < tickInterval) return;
            _acc = 0f;

            float now = Time.time;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (!e.stats || e.stats.IsDead || now >= e.expireAt)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                
                
                var pos = e.socket ? e.socket.position : e.stats.transform.position;

                

                // lehký impact VFX rate-limit (max ~1× za 0.4 s)
                
                {
                    
                    e.nextImpactAt = now + 0.4f;
                }
            }
        }
    }
}
