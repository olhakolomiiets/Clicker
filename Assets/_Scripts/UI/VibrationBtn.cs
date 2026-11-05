
using UnityEngine;
using UnityEngine.UI;
using Lofelt.NiceVibrations;

public class VibrationBtn : MonoBehaviour
{
    private const string PrefsKey = "HapticsEnabled";

    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private Sprite enabledSprite;
    [SerializeField] private Sprite disabledSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreHapticsBeforeSceneLoad()
    {
        bool enabled = PlayerPrefs.GetInt(PrefsKey, 1) == 1;

        if (HapticController.hapticsEnabled != enabled)
        {
            HapticController.hapticsEnabled = enabled;
        }
    }

    private void OnEnable()
    {
        RefreshIcon();
    }

    public void ToggleVibration()
    {
        bool enabled = !HapticController.hapticsEnabled;

        HapticController.hapticsEnabled = enabled;

        PlayerPrefs.SetInt(PrefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        RefreshIcon();
    }

    private void RefreshIcon()
    {
        if (icon == null)
            return;

        icon.sprite = HapticController.hapticsEnabled
            ? enabledSprite
            : disabledSprite;
    }
}
