using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LastSceneManager : MonoBehaviour
{
    private bool isSceneSaved = false;
    private void OnEnable()
    {
        LoadSavedScene();
    }
    private void SaveScene()
    {
        PlayerPrefs.SetInt("SavedScene", SceneManager.GetActiveScene().buildIndex);
        isSceneSaved = true;
    }
    private void LoadSavedScene()
    {
        if (PlayerPrefs.HasKey("SavedScene"))
        {
            SceneManager.LoadScene(PlayerPrefs.GetInt("SavedScene"));
        }
    }

    private void OnApplicationFocus(bool focusStatus)
    {
        if (focusStatus)
        {
            if (isSceneSaved)
            {
                LoadSavedScene();
            }
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveScene();
        }
        else
        {
            if (isSceneSaved)
            {
                LoadSavedScene();
            }
        }
    }

    private void OnApplicationQuit()
    {
        if (!isSceneSaved)
            SaveScene();
    }

    private void OnDisable()
    {
        if (!isSceneSaved)
            SaveScene();
    }

    private void OnDestroy()
    {
        if (!isSceneSaved)
            SaveScene();
    }
}
