using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Obscurus.Console
{
    public class ConsoleRegistry : MonoBehaviour
    {
        public static ConsoleRegistry I { get; private set; }

        [Header("Databáze příkazů")]
        public List<ConsoleDatabase> databases = new();

        // mapování "cmd" -> binding
        Dictionary<string, CommandBinding> _map = new(StringComparer.OrdinalIgnoreCase);

        // typ -> nalezená instance poskytovatele (IConsoleProvider) ve scéně
        readonly Dictionary<Type, UnityEngine.Object> _providers = new();

        void Awake()
        {
            if (I && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);

            RebuildMap();
            FindProvidersInScene();
        }

        void OnEnable()
        {
            // Hledat poskytovatele i po loadu scén
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => FindProvidersInScene();
        }

        void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= (_, __) => { };
        }

        public void RebuildMap()
        {
            _map.Clear();
            foreach (var db in databases)
            {
                if (!db) continue;
                foreach (var b in db.Commands)
                {
                    if (string.IsNullOrWhiteSpace(b.Command) || string.IsNullOrWhiteSpace(b.TypeName) || string.IsNullOrWhiteSpace(b.MethodName))
                        continue;
                    _map[b.Command] = b;
                }
            }
        }

        void FindProvidersInScene()
        {
            _providers.Clear();
            var monos = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var m in monos)
            {
                if (m is Obscurus.Console.IConsoleProvider)
                {
                    _providers[m.GetType()] = m;
                }
            }
            // ScriptableObject providery můžeš přidat ručně přes další API, ale pro začátek stačí MonoBehaviours
        }

        // ---- Public API ----

        public IEnumerable<string> AllCommands() => _map.Keys.OrderBy(k => k);

        public bool TryExecute(string line, out string output)
        {
            output = "";
            if (string.IsNullOrWhiteSpace(line)) return false;

            var tokens = Tokenize(line);
            if (tokens.Count == 0) return false;

            var cmd = tokens[0];
            tokens.RemoveAt(0);

            if (string.Equals(cmd, "help", StringComparison.OrdinalIgnoreCase))
            {
                output = string.Join("\n", AllCommands());
                return true;
            }

            if (!_map.TryGetValue(cmd, out var binding))
            {
                output = $"Unknown command: {cmd}";
                return false;
            }

            // zjisti metodu
            var type = Type.GetType(binding.TypeName);
            if (type == null) { output = $"Type not found: {binding.TypeName}"; return false; }

            var method = type.GetMethod(binding.MethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (method == null) { output = $"Method not found: {binding.MethodName} on {type.Name}"; return false; }

            // připrav cílový objekt (pro instanční metody)
            object target = null;
            if (!binding.IsStatic)
            {
                _providers.TryGetValue(type, out var obj);
                if (!obj)
                {
                    // fallback: zkus najít první instanci daného typu
                    var any = FindFirstObjectByType(type);
                    if (any) obj = any;
                }
                if (!obj) { output = $"No instance of {type.Name} found in scene."; return false; }
                target = obj;
            }

            // slož argumenty (defaulty + zadané)
            var argTokens = new List<string>();
            if (!string.IsNullOrWhiteSpace(binding.DefaultArgs))
                argTokens.AddRange(Tokenize(binding.DefaultArgs));
            argTokens.AddRange(tokens);

            // konverze argumentů
            var pars = method.GetParameters();
            var args = new object[pars.Length];

            try
            {
                for (int i = 0; i < pars.Length; i++)
                {
                    var p = pars[i];
                    if (p.ParameterType == typeof(string))
                    {
                        args[i] = i < argTokens.Count ? argTokens[i] : "";
                    }
                    else if (p.ParameterType == typeof(int))
                    {
                        args[i] = i < argTokens.Count && int.TryParse(argTokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
                    }
                    else if (p.ParameterType == typeof(float))
                    {
                        args[i] = i < argTokens.Count && float.TryParse(argTokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;
                    }
                    else if (p.ParameterType == typeof(bool))
                    {
                        if (i < argTokens.Count)
                        {
                            var s = argTokens[i].ToLowerInvariant();
                            args[i] = (s is "1" or "true" or "on" or "yes");
                        }
                        else args[i] = false;
                    }
                    else if (p.ParameterType.IsEnum)
                    {
                        args[i] = i < argTokens.Count ? Enum.Parse(p.ParameterType, argTokens[i], true) : Activator.CreateInstance(p.ParameterType);
                    }
                    else if (p.ParameterType == typeof(Vector3))
                    {
                        // očekáváme tvar "x,y,z" nebo tři po sobě jdoucí tokeny
                        Vector3 v = default;
                        if (i < argTokens.Count && TryParseVec3(argTokens[i], out v)) { }
                        else if (i + 2 < argTokens.Count &&
                                 float.TryParse(argTokens[i],   NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                                 float.TryParse(argTokens[i+1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                                 float.TryParse(argTokens[i+2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                        { v = new Vector3(x, y, z); i += 2; }
                        args[i] = v;
                    }
                    else
                    {
                        // nepodporovaný typ – zkusíme změnu typu jako string → T
                        args[i] = i < argTokens.Count ? Convert.ChangeType(argTokens[i], p.ParameterType, CultureInfo.InvariantCulture) : null;
                    }
                }

                var result = method.Invoke(target, args);
                output = result?.ToString() ?? "OK";
                return true;
            }
            catch (Exception ex)
            {
                output = $"Error: {ex.InnerException?.Message ?? ex.Message}";
                return false;
            }
        }

        // --- helpers ---

        public static List<string> Tokenize(string s)
        {
            // rozdělení na tokeny: uvozovky "" nebo ''
            var list = new List<string>();
            if (string.IsNullOrEmpty(s)) return list;
            bool inQuote = false; char quote = '\0'; var cur = new System.Text.StringBuilder();
            foreach (var ch in s)
            {
                if (!inQuote && (ch == '"' || ch == '\''))
                { inQuote = true; quote = ch; continue; }
                if (inQuote && ch == quote)
                { inQuote = false; continue; }
                if (!inQuote && char.IsWhiteSpace(ch))
                { if (cur.Length > 0) { list.Add(cur.ToString()); cur.Clear(); } }
                else cur.Append(ch);
            }
            if (cur.Length > 0) list.Add(cur.ToString());
            return list;
        }

        static bool TryParseVec3(string token, out Vector3 v)
        {
            v = default;
            var t = token.Replace("(", "").Replace(")", "");
            var parts = t.Split(',');
            if (parts.Length != 3) return false;
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            { v = new Vector3(x, y, z); return true; }
            return false;
        }

        static UnityEngine.Object FindFirstObjectByType(Type t)
        {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType(t);
#else
            return UnityEngine.Object.FindObjectOfType(t);
#endif
        }
    }
}
