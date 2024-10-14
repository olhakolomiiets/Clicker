using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    private AsyncOperationHandle<SceneInstance> loadHandle;

    public void LoadSceene(int level)
    {
        SceneManager.LoadScene(level);
    }

    public void LoadRemoteSceene(string key) 
    {
        loadHandle = Addressables.LoadSceneAsync(key, LoadSceneMode.Single);
    }
}
