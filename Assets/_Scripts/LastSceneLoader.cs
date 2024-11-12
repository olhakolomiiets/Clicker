using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LastSceneLoader : MonoBehaviour
{
    private void Awake()
    {
        if (PlayerPrefs.HasKey("SavedScene"))
        {
            int sceneIndex = PlayerPrefs.GetInt("SavedScene");
            int activeScene = SceneManager.GetActiveScene().buildIndex;

            if (activeScene != sceneIndex)
                StartCoroutine(LoadYourAsyncScene(sceneIndex));

            Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! LastSceneManager /// Awake /// Scene Index: " + sceneIndex);
            Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! LastSceneManager /// Awake /// Active Scene Index: " + activeScene);
        }
    }

    IEnumerator LoadYourAsyncScene(int index)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(index);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
}
