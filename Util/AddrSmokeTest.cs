using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddrSmokeTest : MonoBehaviour
{
    [Header("Přetáhni sem Cube.prefab z Project okna")]
    public AssetReferenceGameObject cubeRef;

    AsyncOperationHandle<GameObject> handle;

    async void Start()
    {
        handle = cubeRef.InstantiateAsync(new Vector3(0, 1.2f, 0), Quaternion.identity);
        await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded)
            Debug.LogError($"Addressables spawn fail: {cubeRef}");
        else
            Debug.Log("[AddrSmokeTest] Cube spawned OK");
    }

    void OnDestroy()
    {
        if (handle.IsValid()) Addressables.ReleaseInstance(handle.Result);
    }
}