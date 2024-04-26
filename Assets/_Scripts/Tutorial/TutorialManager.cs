using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> tips = new List<GameObject>();
    [SerializeField] private GameObject background;
    [SerializeField] private Button nextTipButton;

    private int currentTipIndex = 0;

    void Start()
    {
        if (PlayerPrefs.GetInt("FirstTutorial") == 0)
        {
            nextTipButton.gameObject?.SetActive(true);
            ShowTip(currentTipIndex);
            PlayerPrefs.SetInt("FirstTutorial", 1);
        }

        if (nextTipButton != null)
        {
            nextTipButton.onClick.AddListener(OnNextTipButtonClicked);
        }
    }

    void OnNextTipButtonClicked()
    {
        // Disable the current tip
        if (currentTipIndex < tips.Count)
        {
            tips[currentTipIndex].SetActive(false);
        }

        // Enable the next tip (if it exists)
        currentTipIndex++;
        if (currentTipIndex < tips.Count)
        {
            tips[currentTipIndex].SetActive(true);
        }
        else nextTipButton.gameObject?.SetActive(false);
    }

    void ShowTip(int index)
    {
        if (index >= 0 && index < tips.Count)
        {
            // Disable all tips except the one at the specified index
            for (int i = 0; i < tips.Count; i++)
            {
                tips[i].SetActive(i == index);
            }
        }      
    }
}
