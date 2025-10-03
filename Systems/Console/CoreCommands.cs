using Obscurus.Console;
using UnityEngine;
using UnityEngine.AddressableAssets;

public static class CoreCommands
{
    [ConsoleCommand("echo", "Vypíše argumenty zpět.")]
    public static string Echo(params string[] words) => string.Join(" ", words);

    [ConsoleCommand("timescale", "Nastaví Time.timeScale (např. timescale 0.2)")]
    public static string TimeScale(float value = 1f)
    {
        Time.timeScale = Mathf.Max(0f, value);
        return $"TimeScale = {Time.timeScale:0.###}";
    }

    [ConsoleCommand("spawn", "Addressables spawn: spawn <address> [x y z]")]
    public static async System.Threading.Tasks.Task<string> Spawn(string address, float x=0, float y=1, float z=0)
    {
        var h = Addressables.InstantiateAsync(address, new Vector3(x,y,z), Quaternion.identity);
        await h.Task;
        return h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded
            ? $"Spawned {address} at ({x},{y},{z})"
            : $"Spawn failed: {address}";
    }
}