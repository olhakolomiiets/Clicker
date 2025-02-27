using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelController : MonoBehaviour
{
    [SerializeField] private List<Button> _levelButtons;
    private GeneralGameData generalData;
    public void PrepareGameData(GeneralGameData generalGameData, GameData gameData)
    {
        generalData = generalGameData;

        for (int i = 0; i < _levelButtons.Count; i++)
        {
            _levelButtons[i].interactable = generalData.ActivePlanet > i;
        }
    }

    private void Start()
    {
        for (int i = 0; i < _levelButtons.Count; i++)
        {
            _levelButtons[i].interactable = generalData.ActivePlanet > i;
        }
    }
}
