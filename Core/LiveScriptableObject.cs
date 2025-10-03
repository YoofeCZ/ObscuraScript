using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ScriptableObject, který v Play módu vyšle událost při každé změně hodnot
/// (inspector edit, Undo/Redo, programová změna přes RaiseChanged()).
/// </summary>
public abstract class LiveScriptableObject : ScriptableObject
{
    public event Action<LiveScriptableObject> Changed;

    protected virtual void OnValidate()
    {
        // V editoru se volá po změně v Inspectoru (funguje i v Play módu)
        RaiseChanged();
    }

#if UNITY_EDITOR
    // Podpora Undo/Redo (také má „hot-apply“)
    void OnEnable()
    {
        Undo.undoRedoPerformed -= RaiseChanged;
        Undo.undoRedoPerformed += RaiseChanged;
    }
    void OnDisable()
    {
        Undo.undoRedoPerformed -= RaiseChanged;
    }
#endif

    /// <summary> Programově vyvolej hot-apply (např. po změně z kódu). </summary>
    [ContextMenu("Notify Hot-Apply")]
    public void RaiseChanged()
    {
        if (!Application.isPlaying) return; // mimo Play mód nemá smysl „aplikovat za běhu“
        try { Changed?.Invoke(this); }
        catch (Exception e) { Debug.LogException(e); }
    }
}