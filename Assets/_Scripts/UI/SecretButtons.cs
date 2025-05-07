using UnityEngine;
using UnityEngine.UI;

public class SecretButtons : MonoBehaviour
{
    [SerializeField] private InputField inputField;
    [SerializeField] private GameObject inpinputPanel, secretButtons;
    private const string secretCode = "#25251805";

    private void OnEnable()
    {
        if (PlayerPrefs.GetInt("SecretButtonsShown") == 1)
        {
            inpinputPanel.SetActive(false);
            secretButtons.SetActive(true);
        }
    }

    public void CheckCodeAndShowButtons()
    {
        if (inputField.text == secretCode)
        {
            inpinputPanel.SetActive(false);
            secretButtons.SetActive(true);
            PlayerPrefs.SetInt("SecretButtonsShown", 1);
        }
        else
        {
            Debug.Log("Incorrect code.");
        }
    }
}
