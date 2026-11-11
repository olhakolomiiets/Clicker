// Define PLANETBUILDER_USE_ADDRESSABLES in Player Settings > Scripting Define Symbols
// only when com.unity.addressables is installed in your project.
#if PLANETBUILDER_USE_ADDRESSABLES
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace PlanetBuilder.Loading
{
    public sealed partial class PlanetLoadingScreen
    {
        /// <summary>
        /// Loads an Addressables scene by address. The coroutine stays alive across scene changes
        /// because it runs on the DontDestroyOnLoad loading-screen object.
        /// </summary>
        public void LoadAddressableScene(string address)
        {
            if (sceneTransitionRunning)
            {
                Debug.LogWarning("[PlanetLoadingScreen] A scene transition is already in progress.", this);
                return;
            }
            if (string.IsNullOrWhiteSpace(address))
            {
                Debug.LogError("[PlanetLoadingScreen] Addressables scene address is empty.", this);
                return;
            }
            StartCoroutine(LoadAddressableSceneRoutine(address));
        }

        private IEnumerator LoadAddressableSceneRoutine(string address)
        {
            sceneTransitionRunning = true;
            Show();
            if (!showing)
            {
                sceneTransitionRunning = false;
                yield break;
            }
            yield return null;

            AsyncOperationHandle<SceneInstance> handle = default;
            bool started = false;
            try
            {
                handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Single, activateOnLoad: true);
                started = true;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }

            if (!started)
            {
                CancelAndHide();
                sceneTransitionRunning = false;
                yield break;
            }

            while (!handle.IsDone) yield return null;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[PlanetLoadingScreen] Addressables scene '{address}' failed: {handle.OperationException}", this);
                // Failed load does not own a live scene. Release its handle.
                if (handle.IsValid()) Addressables.Release(handle);
                CancelAndHide();
                sceneTransitionRunning = false;
                yield break;
            }

            // Do NOT Release(handle) here: it owns the loaded scene. Addressables handles
            // scene lifetime/unloading according to the installed package version and settings.
            yield return null;
            NotifyLoadCompleted();
            yield return WaitUntilHidden();
            sceneTransitionRunning = false;
        }
    }
}
#endif
