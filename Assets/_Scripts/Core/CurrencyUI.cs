using MoreMountains.Feedbacks;
using UnityEngine;

public class CurrencyUI : MonoBehaviour
{
    [SerializeField] private ScorePanel _coins;
    [SerializeField] private ScorePanel _diamonds;

    [SerializeField] private MMF_Player mmfPlayer;

    public void UpdateCurrency(GeneralGameData generalData, GameData gameData)
    {
        mmfPlayer.PlayFeedbacks();
        
        if (_coins != null && gameData != null)
            _coins.SetScore(gameData.Money);

        if (_diamonds != null)
            _diamonds.SetDiamondsScore(generalData.Diamonds);
    }
}