using UnityEngine;

/// <summary>
/// Připoj na objekt, který má číst data ze SO za běhu.
/// Kdykoli se SO změní, zavolá se Apply(data).
/// </summary>
public abstract class HotAppliedBehaviour<T> : MonoBehaviour where T : LiveScriptableObject
{
    public T data;

    protected virtual void OnEnable()
    {
        if (data) data.Changed += OnChanged;
        ApplyIfPossible();
    }
    protected virtual void OnDisable()
    {
        if (data) data.Changed -= OnChanged;
    }

    void OnChanged(LiveScriptableObject _) { if (enabled && data) Apply(data); }
    protected void ApplyIfPossible() { if (Application.isPlaying && data) Apply(data); }

    /// <summary> Sem napiš, jak má komponenta aplikovat hodnoty do běhu. </summary>
    protected abstract void Apply(T src);

    // volitelné – pokud chceš přepínat asset za běhu:
    public void SetData(T newData)
    {
        if (data) data.Changed -= OnChanged;
        data = newData;
        if (data) { data.Changed += OnChanged; ApplyIfPossible(); }
    }
}