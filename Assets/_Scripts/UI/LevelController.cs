using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelController : MonoBehaviour
{
    [SerializeField] private List<Button> _levelButtons;
    private GameData currentGameData;
    public void PrepareGameData(GeneralGameData generalGameData, GameData gameData)
    {
        currentGameData = gameData;

        if (currentGameData.Money > 3000)
            _levelButtons[2].interactable = true;
        else
            _levelButtons[2].interactable = false;

        if (currentGameData.Money > 1000)
            _levelButtons[1].interactable = true;
        else
            _levelButtons[1].interactable = false;


    }
}
