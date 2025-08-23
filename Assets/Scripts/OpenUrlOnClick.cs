using UnityEngine;
using UnityEngine.UI;

public class OpenUrlOnClick : MonoBehaviour
{
    void Start()
    {
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(LogWorks);
        }
        else
        {
            Debug.LogError("Button component not found on this GameObject.");
        }
    }

    public void LogWorks()
    {
        Debug.Log("Works");
}
}
