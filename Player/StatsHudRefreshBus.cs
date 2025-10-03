using System;

public static class StatsHudRefreshBus
{
    public static event Action OnRefreshRequested;

    /// <summary>Okamžitě požádej všechny HUDe o refresh (cur+max) bez animace.</summary>
    public static void Ping()
    {
        OnRefreshRequested?.Invoke();
    }
}