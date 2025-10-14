// Assets/Obscurus/Scripts/AI/TrainingDummyAI.cs
using UnityEngine;
using System.Linq;

namespace Obscurus.AI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Obscurus/AI/Training Dummy AI")]
    public class TrainingDummyAI : MonoBehaviour
    {
        [Header("Chování")]
        public float lookAtPlayerRange = 25f;
        public float turnSpeedDegPerSec = 540f;

        [Header("Cíl (volitelné)")]
        public Transform explicitTarget;       // sem můžeš ručně hodit Player
        public string playerTag = "Player";

        [Header("Respawn (optional)")]
        public bool autoResetOnDeath = true;
        public float resetAfterSeconds = 3f;

        EnemyStats stats;
        Transform target;
        float diedAt = -999f;
        float nextFindTime;

        void Awake()
        {
            stats = GetComponent<EnemyStats>();
        }

        void OnEnable()
        {
            if (stats) stats.OnDied.AddListener(OnDied);
        }

        void OnDisable()
        {
            if (stats) stats.OnDied.RemoveListener(OnDied);
        }

        void Update()
        {
            if (stats && stats.IsDead)
            {
                if (autoResetOnDeath && Time.time >= diedAt + resetAfterSeconds)
                    ResetDummy();
                return;
            }

            // hledej cíl ~2× za sekundu, když není
            if (!target && Time.time >= nextFindTime)
            {
                nextFindTime = Time.time + 0.5f;
                target = FindTarget();
            }

            if (!target) return;

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist <= lookAtPlayerRange)
            {
                Vector3 dir = target.position - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    var wanted = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, turnSpeedDegPerSec * Time.deltaTime);
                }
            }
        }

        Transform FindTarget()
        {
            if (explicitTarget) return explicitTarget;

            // 1) PlayerController v scéně
            var pc = FindObjectsOfType<MonoBehaviour>(true).FirstOrDefault(x => x.GetType().Name == "PlayerController");
            if (pc) return pc.transform;

            // 2) Tag "Player"
            var t = GameObject.FindGameObjectWithTag(playerTag);
            if (t) return t.transform;

            // 3) MainCamera
            if (Camera.main) return Camera.main.transform;

            return null;
        }

        void OnDied() => diedAt = Time.time;

        public void ResetDummy()
        {
            if (!stats) return;
            // jednoduchý reset poolů
            stats.Kill(); // zajistí eventy při 0 → pak „oživíme“
            stats.enabled = false;
            stats.enabled = true;
            diedAt = -999f;
        }
    }
}
