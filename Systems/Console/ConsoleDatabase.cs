using System;
using System.Collections.Generic;
using UnityEngine;

namespace Obscurus.Console
{
    [CreateAssetMenu(fileName = "ConsoleDatabase", menuName = "Obscurus/Console/Database")]
    public class ConsoleDatabase : ScriptableObject
    {
        public List<CommandBinding> Commands = new();
    }

    [Serializable]
    public class CommandBinding
    {
        [Tooltip("Příkaz, který uživatel napíše (např. noclip)")]
        public string Command;

        [TextArea] public string Help;

        [Tooltip("AssemblyQualifiedName třídy, kde je metoda")]
        public string TypeName;

        [Tooltip("Název metody označené [ConsoleCommand]")]
        public string MethodName;

        [Tooltip("Je metoda static? (jinak se volá na instanci IConsoleProvider)")]
        public bool IsStatic;

        [Tooltip("Volitelné defaultní argumenty (oddělené mezerou)")]
        public string DefaultArgs = "";
    }
}