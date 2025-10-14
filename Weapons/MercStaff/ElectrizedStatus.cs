// Assets/Obscurus/Scripts/Effects/ElectrizedStatus.cs
using System.Collections;
using UnityEngine;
using Obscurus.AI;
using Obscurus.VFX;

namespace Obscurus.Effects
{
    [DisallowMultipleComponent]
    public class ElectrizedStatus : MonoBehaviour
    {
        public int Stacks { get; private set; }
        public bool IsActive => _running;

        EnemyStats _stats;
        GameObject _owner;
        ElectrizedEffectDef _def;
        float _expireAt;
        bool _running;

        PooledVFX _vfx;
        Transform _socket;

        public void Apply(ElectrizedEffectDef def, GameObject owner, int addStacks, Transform socketHint = null)
        {
            if (!_stats) _stats = GetComponent<EnemyStats>();
            if (!_stats || _stats.IsDead || def == null) return;

            _def = def;
            _owner = owner;
            _socket = socketHint ? socketHint : transform;

            // vfx attach (jednou)
            if (_def.onTargetVFX && _vfx == null && _socket)
                _vfx = VFXPool.SpawnLoop(_def.onTargetVFX, _socket.position, _socket.rotation, _socket);

            // stacky
            Stacks = Mathf.Clamp(Stacks + Mathf.Max(1, addStacks), 1, Mathf.Max(1, _def.maxStacks));
            _expireAt = Time.time + Mathf.Max(0.1f, _def.duration);

            if (!_running) StartCoroutine(Co_Tick());
        }

        IEnumerator Co_Tick()
        {
            _running = true;
            var wait = new WaitForSeconds(Mathf.Max(0.05f, _def.tickInterval));

            while (Time.time < _expireAt && _stats && !_stats.IsDead)
            {
                float dmg = Stacks * _def.damagePerStack;
                _stats.ApplyDamage(dmg, _socket ? _socket.position : transform.position, Vector3.up, _owner);
                yield return wait;
            }

            // end
            Stacks = 0;
            _running = false;
            if (_vfx) VFXPool.Release(_vfx);
            _vfx = null;
        }

        void OnDisable()
        {
            if (_vfx) VFXPool.Release(_vfx);
            _vfx = null;
            _running = false;
            Stacks = 0;
        }
    }
}
