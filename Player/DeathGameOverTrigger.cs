#if ODIN_INSPECTOR || ODIN_SERIALIZER
#define HAS_ODIN
#endif
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;   // PlayableDirector (Timeline)
#if HAS_ODIN
using Sirenix.OdinInspector;
#endif

namespace Obscurus.Player
{
    [DisallowMultipleComponent]
#if HAS_ODIN
    [HideMonoScript, InfoBox("Napoj to na objekt s HealthSystem. Při OnDied zahraje volitelně Timeline a pak ukáže Game Over UI.")]
#endif
    public class DeathGameOverTrigger : MonoBehaviour
    {
        [Header("Refs")]
        public HealthSystem health;                     // přiřaď, nebo se najde v Awake/Start
        public PlayableDirector deathTimeline;          // volitelné

        [Header("Options")]
        [Tooltip("Zastavit gameplay hned při smrti (zamknout vstup). Timeline poběží v UnscaledTime.")]
        public bool freezeGameplayOnDeath = true;

        [Tooltip("Když je hráč už po spawnu mrtvý (HP=0), ukaž rovnou Game Over.")]
        public bool showIfAlreadyDeadOnStart = true;

        [Tooltip("Bezpečnostní záchrana: po délce timeline UI ukázat i když 'stopped' nepřijde.")]
        public bool forceShowAfterTimelineDuration = true;

        GameOverUI _gameOver;
        bool _uiShown;

        void Awake()
        {
            if (!health)
                health = GetComponent<HealthSystem>()
                      ?? GetComponentInChildren<HealthSystem>(true)
                      ?? GetComponentInParent<HealthSystem>();

            _gameOver = FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include);
            if (!_gameOver)
                Debug.LogWarning("[DeathGameOverTrigger] Nenalezen GameOverUI ve scéně. Postav UI přes Obscurus/Build UI/Game Over Screen.");

            if (health != null)
                health.OnDied += HandleDied;
            else
                Debug.LogError("[DeathGameOverTrigger] Nenalezen HealthSystem – Game Over se nespustí.");
        }

        void Start()
        {
            if (showIfAlreadyDeadOnStart && health != null && health.Current <= 0f)
            {
                Debug.Log("[DeathGameOverTrigger] Player already dead on Start → showing Game Over.");
                HandleDied();
            }
        }

        void OnDestroy()
        {
            if (health != null) health.OnDied -= HandleDied;
            if (deathTimeline != null) deathTimeline.stopped -= OnTimelineStopped;
        }

        void HandleDied()
        {
            if (_uiShown) return;

            // 👉 zavři případnou konzoli okamžitě
            GameOverUI.TryCloseConsole();

            if (freezeGameplayOnDeath)
                GameOverUI.FreezeGameplay(true);

            if (deathTimeline)
            {
                deathTimeline.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                deathTimeline.stopped -= OnTimelineStopped;
                deathTimeline.stopped += OnTimelineStopped;
                deathTimeline.Play();

                if (forceShowAfterTimelineDuration)
                    StartCoroutine(ForceShowAfterDuration());
            }
            else
            {
                ShowUI();
            }
        }

        void OnTimelineStopped(PlayableDirector _)
        {
            if (_uiShown) return;
            ShowUI();
        }

        IEnumerator ForceShowAfterDuration()
        {
            float dur = 0.1f;
            if (deathTimeline && deathTimeline.playableAsset != null)
            {
                var d = (float)deathTimeline.duration;
                if (float.IsFinite(d) && d > 0f) dur = d + 0.05f;
            }
            yield return new WaitForSecondsRealtime(dur);
            if (!_uiShown) ShowUI();
        }

        void ShowUI()
        {
            _uiShown = true;

            if (!_gameOver)
                _gameOver = FindFirstObjectByType<GameOverUI>(FindObjectsInactive.Include);

            if (_gameOver) _gameOver.Show();
            else Debug.LogError("[DeathGameOverTrigger] GameOverUI stále nenalezen. Postav UI přes menu: Obscurus/Build UI/Game Over Screen.");
        }
    }
}
