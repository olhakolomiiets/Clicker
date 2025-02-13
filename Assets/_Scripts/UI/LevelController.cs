using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelController : MonoBehaviour
{
    [SerializeField] private List<Button> _levelButtons;
    private GeneralGameData generalData;
    private int activePlanets = 1;
    public void PrepareGameData(GeneralGameData generalGameData, GameData gameData)
    {
        generalData = generalGameData;

        if (generalData.ActivePlanet > 1)
            _levelButtons[1].interactable = true;
        else
            _levelButtons[1].interactable = false;

        if (generalData.ActivePlanet > 2)
            _levelButtons[2].interactable = true;
        else
            _levelButtons[2].interactable = false;

    }

    private void Start()
    {
        if(generalData.ActivePlanet > 1)
            _levelButtons[1].interactable = true;
        else
            _levelButtons[1].interactable = false;

        if (generalData.ActivePlanet > 2)
            _levelButtons[2].interactable = true;
        else
            _levelButtons[2].interactable = false;
    }
}
