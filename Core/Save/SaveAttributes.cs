using System;

namespace Obscurus.Save
{
    /// <summary>Na field/komponentu: nikdy neukládat.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class SaveIgnoreAttribute : Attribute { }

    /// <summary>Na UnityEngine.Object field: uložit jako SaveId (scénový odkaz), ne inline.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SaveAsIdAttribute : Attribute { }

    /// <summary>Na field: vynutit inline serializaci i když není public/[SerializeField] (POZOR na reference!).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SaveInlineAttribute : Attribute { }
}