using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem; // NOVÉ

public class AddrSceneLoader : MonoBehaviour
{
    [Header("Addressables adresa scény")]
    public string sceneAddress = "Levels/Example"; // musí sedět s Address v Groups

    public async Task LoadAdditive()
    {
        var handle = Addressables.LoadSceneAsync(sceneAddress, LoadSceneMode.Additive, true);
        var s = await handle.Task;
        if (!s.Scene.IsValid()) Debug.LogError($"Scene load failed: {sceneAddress}");
        else SceneManager.SetActiveScene(s.Scene);
    }

    public async Task Unload()
    {
        var s = SceneManager.GetSceneByName("Example");
        if (s.IsValid())
            await SceneManager.UnloadSceneAsync(s);
    }

    void Update()
    {
        if (Keyboard.current == null) return; // ochrana, když není device

        if (Keyboard.current.f6Key.wasPressedThisFrame) _ = LoadAdditive();
        if (Keyboard.current.f7Key.wasPressedThisFrame) _ = Unload();
    }
}