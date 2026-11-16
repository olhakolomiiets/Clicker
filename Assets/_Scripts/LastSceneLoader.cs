using UnityEngine;
using PlanetBuilder.Loading;

public class LastSceneLoader : MonoBehaviour
{
    private void Start()
    {
        int savedSceneId = PlayerPrefs.GetInt("SavedScene", 0);

        string sceneName = GetSceneName(savedSceneId);

        if (string.IsNullOrEmpty(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            sceneName = "PlanetMeta";
        }

        if (PlanetLoadingScreen.Instance == null)
        {
            Debug.LogError(
                "[LastSceneLoader] PlanetLoadingScreen is missing.",
                this
            );

            return;
        }

        PlanetLoadingScreen.Instance.LoadBuiltInScene(sceneName);
    }

    public static string GetSceneName(int sceneId)
    {
        switch (sceneId)
        {
            case 0:
                return "PlanetMeta";
            case 1:
                return "PlanetLevel1";
            case 2:
                return "PlanetLevel3";
            case 3:
                return "PlanetLevel4";
            default:
                return null;
        }
    }

    public static bool TryGetSceneId(string sceneName, out int sceneId)
    {
        switch (sceneName)
        {
            case "PlanetMeta":
                sceneId = 0;
                return true;

            case "PlanetLevel1":
                sceneId = 1;
                return true;

            case "PlanetLevel3":
                sceneId = 2;
                return true;

            case "PlanetLevel4":
                sceneId = 3;
                return true;

            default:
                sceneId = -1;
                return false;
        }
    }
}
