using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Obscurus.Console
{
    public class DevConsole : MonoBehaviour
    {
        [Header("UI")]
        public CanvasGroup canvasGroup;
        public TMP_InputField input;
        public TextMeshProUGUI output;

        [Header("Behavior")]
        public bool startHidden = true;
        public int maxLines = 200;

        [Header("Sources")]
        public ConsoleRegistry registry;         // můžeš přetáhnout ručně; jinak se založí sám
        public List<ConsoleDatabase> databases;  // volitelné – přidá do registry na startu

        readonly List<string> _history = new();
        int _histIndex = -1;
        readonly StringBuilder _log = new();

        bool visible;

        void Awake()
        {
            // Najdi / vytvoř registry
            if (!registry) registry = FindFirstObjectByType<ConsoleRegistry>();
            if (!registry)
            {
                var go = new GameObject("ConsoleRegistry");
                registry = go.AddComponent<ConsoleRegistry>();
            }
            // Přidej databáze
            foreach (var db in databases)
                if (db && !registry.databases.Contains(db))
                    registry.databases.Add(db);
            registry.RebuildMap();

            // Bezpečně připoj události z TMP_InputField
            if (input)
            {
                // Některé verze TMP nezobrazují On Submit v Inspectoru, ale event existuje
                try { input.onSubmit?.AddListener(SubmitFromEvent); } catch { /* ignore if not present */ }
                // Fallback – vždy existuje
                input.onEndEdit.AddListener(EndEditFromEvent);
                // doporučení pro konzoli
                input.lineType = TMP_InputField.LineType.SingleLine;
                input.richText = false;
            }

            SetVisible(!startHidden);
            Application.logMessageReceived += OnLog;
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            // Toggle: ~ (backquote) nebo F10
            if (kb.backquoteKey.wasPressedThisFrame || kb.f10Key.wasPressedThisFrame)
                SetVisible(!visible);

            if (!visible) return;

            // Enter submit – jistota i bez UI eventu
            if (input && input.isFocused &&
                (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                SubmitLine();
                return;
            }

            // historie
            if (kb.upArrowKey.wasPressedThisFrame)   History(-1);
            if (kb.downArrowKey.wasPressedThisFrame) History(+1);
        }

        void SetVisible(bool v)
        {
            visible = v;
            if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
            if (!canvasGroup) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = v ? 1f : 0f;
            canvasGroup.blocksRaycasts = v;
            canvasGroup.interactable = v;

            if (v)
            {
                if (input)
                {
                    EventSystem.current?.SetSelectedGameObject(input.gameObject);
                    input.text = "";
                    input.ActivateInputField();
                }
            }
            else
            {
                EventSystem.current?.SetSelectedGameObject(null);
            }
        }

        // ==== UI event handlers (string signatura kvůli UnityEvents) ====
        public void SubmitFromEvent(string _) => SubmitLine();

        public void EndEditFromEvent(string _)
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                SubmitLine();
        }

        // ==== skutečné provedení příkazu ====
        void SubmitLine()
        {
            if (!input || string.IsNullOrWhiteSpace(input.text)) return;

            var line = input.text;
            AppendLine($"> {line}");

            _history.Add(line);
            if (_history.Count > 128) _history.RemoveAt(0);
            _histIndex = _history.Count;

            if (registry.TryExecute(line, out var result))
                AppendLine(result);
            else
                AppendLine(result); // vypíše chybu / unknown

            input.text = "";
            input.ActivateInputField();
        }

        void History(int dir)
        {
            if (_history.Count == 0 || !input) return;
            _histIndex = Mathf.Clamp(_histIndex + dir, 0, _history.Count);
            input.text = _histIndex < _history.Count ? _history[_histIndex] : "";
            input.caretPosition = input.text.Length;
        }

        void AppendLine(string s)
        {
            _log.AppendLine(s);

            // omez počet řádků
            var text = _log.ToString();
            var lines = text.Split('\n');
            if (lines.Length > maxLines)
            {
                int skip = lines.Length - maxLines;
                var sb = new StringBuilder();
                for (int i = skip; i < lines.Length; i++) sb.AppendLine(lines[i]);
                _log.Clear(); _log.Append(sb.ToString());
            }

            if (output) output.text = _log.ToString();
        }

        void OnLog(string condition, string stacktrace, LogType type)
        {
            AppendLine($"[{type}] {condition}");
        }
    }
}
