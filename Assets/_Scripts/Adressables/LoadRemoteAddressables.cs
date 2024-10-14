using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.ResourceManagement.ResourceProviders;

public class LoadRemoteAddressables : MonoBehaviour
{
    private GameObject _mMyGameObject;
    private AsyncOperationHandle<SceneInstance> loadHandle;

    public void InstantiateGameobjectUsingAssetReference(string key)
    {
        Addressables.InstantiateAsync(key).Completed += OnLoadDone;
    }

    private void OnLoadDone(AsyncOperationHandle<GameObject> obj)
    {
        _mMyGameObject = obj.Result;
    }

    public void ReleaseGameobjectUsingAssetReference()
    {
        Addressables.Release(_mMyGameObject);
    }

    public void LoadRemoteScene(string key)
    {
        AsyncOperationHandle downloadDependencies = Addressables.DownloadDependenciesAsync(key);

        //loadHandle = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive);
    }
}