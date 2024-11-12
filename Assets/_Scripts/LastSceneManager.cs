using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LastSceneManager : MonoBehaviour
{
    [SerializeField] private List<Button> _levelButtons;

    private GameData currentGameData;
    private bool isSceneSaved = true;
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

    private void SaveScene()
    {
        PlayerPrefs.SetInt("SavedScene", SceneManager.GetActiveScene().buildIndex);
        isSceneSaved = true;
    }

    private void LoadSavedScene()
    {
        isSceneSaved = false;
        if (PlayerPrefs.HasKey("SavedScene"))
            SceneManager.LoadScene(PlayerPrefs.GetInt("SavedScene"));
    }

    private void HandleApplicationState(bool status)
    {
        if (status) SaveScene();
        else if (isSceneSaved) LoadSavedScene();
    }

    private void OnDestroy()
    {

    }
}
