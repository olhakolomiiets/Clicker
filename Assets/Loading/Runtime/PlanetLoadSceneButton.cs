using UnityEngine;

namespace PlanetBuilder.Loading
{
    /// <summary>
    /// Optional helper for Unity UI Button > On Click().
    /// Attach to any GameObject, set the scene address, and bind Load() to the button.
    /// </summary>
    public sealed class PlanetLoadSceneButton : MonoBehaviour
    {
        [SerializeField] private string sceneNameOrAddress = "Planet1";
        [SerializeField] private bool useAddressables;

        public void Load()
        {
            if (PlanetLoadingScreen.Instance == null)
            {
                Debug.LogError("[PlanetLoadSceneButton] Put the PlanetLoadingScreen prefab in your bootstrap scene first.", this);
                return;
            }

            if (useAddressables)
            {
#if PLANETBUILDER_USE_ADDRESSABLES
                PlanetLoadingScreen.Instance.LoadAddressableScene(sceneNameOrAddress);
#else
                Debug.LogError("[PlanetLoadSceneButton] Install Addressables and define PLANETBUILDER_USE_ADDRESSABLES in Player Settings.", this);
#endif
            }
            else
            {
                PlanetLoadingScreen.Instance.LoadBuiltInScene(sceneNameOrAddress);
            }
        }
    }
}
