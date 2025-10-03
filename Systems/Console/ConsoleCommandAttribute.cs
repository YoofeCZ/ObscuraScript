using System;

namespace Obscurus.Console
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ConsoleCommandAttribute : Attribute
    {
        public readonly string DisplayName;
        public readonly string Help;

        public ConsoleCommandAttribute(string displayName = null, string help = null)
        {
            DisplayName = displayName;
            Help = help;
        }
    }

    // Marker rozhraní pro instanční příkazy – stačí implementovat na MonoBehaviour/ScriptableObject.
    public interface IConsoleProvider {}
}