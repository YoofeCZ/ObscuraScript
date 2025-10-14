// Assets/Obscurus/Scripts/Core/Pooling/PooledVFX.cs
using System.Linq;
using UnityEngine;
using UnityEngine.VFX;
using Obscurus.Core.Pooling;

namespace Obscurus.VFX
{
    [DisallowMultipleComponent]
    public class PooledVFX : MonoBehaviour, IPoolable
    {
        public bool looping = false;     // true = držím, ruční release; false = one-shot
        public float fallbackTTL = 1.0f; // pro one-shot když nezjistíme lifetime

        ParticleSystem[] ps;
        VisualEffect vfx;

        float releaseAt = -1f;
        Transform _follow; // volitelný follow parent

        public void OnRent()
        {
            if (ps == null) ps  = GetComponentsInChildren<ParticleSystem>(true);
            if (!vfx)      vfx = GetComponent<VisualEffect>();
            releaseAt = -1f;
            _follow = null;

            // reset simulace
            if (ps != null) foreach (var p in ps) { p.Clear(true); p.Play(true); }
            if (vfx) { /*vfx.Reinit();*/ vfx.Play(); } // Reinit typicky není třeba – nechte zakomentované
        }

        public void OnReturn()
        {
            if (ps != null) foreach (var p in ps) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (vfx) vfx.Stop();
            _follow = null;
        }

        void Update()
        {
            if (_follow) { transform.position = _follow.position; transform.rotation = _follow.rotation; }

            if (!looping && releaseAt > 0f && Time.time >= releaseAt)
            {
                VFXPool.Release(this);
            }
        }

        public void PlayOneShot(Vector3 pos, Quaternion rot, Transform follow = null, float ttl = -1f)
        {
            transform.SetPositionAndRotation(pos, rot);
            _follow = follow;

            float calcTTL = ttl > 0 ? ttl : EstimateTTL();
            looping   = false;
            releaseAt = Time.time + calcTTL;
        }

        public void PlayLoop(Vector3 pos, Quaternion rot, Transform follow = null)
        {
            transform.SetPositionAndRotation(pos, rot);
            _follow = follow;
            looping = true;
            releaseAt = -1f;
        }

        float EstimateTTL()
        {
            float ttl = 0f;
            if (ps != null && ps.Length > 0)
            {
                foreach (var p in ps)
                {
                    var main = p.main;
                    float d = main.duration;
                    float life = main.startLifetime.mode switch
                    {
                        ParticleSystemCurveMode.TwoConstants => main.startLifetime.constantMax,
                        ParticleSystemCurveMode.TwoCurves    => main.startLifetime.constantMax, // aproximace
                        _                                    => main.startLifetime.constant
                    };
                    if (!main.loop) ttl = Mathf.Max(ttl, d + life);
                }
            }
            if (ttl <= 0f && vfx) ttl = fallbackTTL; // VFX Graph neumí snadno TTL — necháme fallback
            if (ttl <= 0f) ttl = fallbackTTL;
            return ttl + 0.1f;
        }
    }
}
