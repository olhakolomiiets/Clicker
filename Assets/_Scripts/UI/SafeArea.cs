using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    RectTransform panel;

    void Awake()
    {
        panel = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void ApplySafeArea()
    {
        bool isFullScreenDevice = Screen.safeArea.height < Screen.height || Screen.safeArea.width < Screen.width;

            Rect safeArea = Screen.safeArea;
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            if (isFullScreenDevice)
            {
                anchorMax.y /= Screen.height - 50;
            }
            else
            {
                anchorMax.y /= Screen.height;
            }
            
            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
    }

    void Update()
    {
        ApplySafeArea(); // на случай изменения ориентации
    }
}
