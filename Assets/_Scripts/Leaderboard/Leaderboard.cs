using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using GooglePlayGames;
using UnityEngine.SocialPlatforms;
using System.Collections.Generic;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;

public class Leaderboard : MonoBehaviour
{
    [SerializeField] private Text logText;

    GameData _currentGameData;
    private double _score;

    private bool mStandby = false;
    private string mStandbyMessage = string.Empty;
    private string _mStatus = "Ready";

    [HideInInspector] public UnityEvent OnPressLeaderboardButton, OnShowLeaderboard;

    private void OnEnable()
    {
        OnShowLeaderboard.AddListener(ShowLeaderboard);
    }

    void Start()
    {
        PlayGamesPlatform.DebugLogEnabled = true;
        PlayGamesPlatform.Activate();
        PlayGamesPlatform.Instance.Authenticate(OnSignInResult);
    }

    public void PrepareRewardData(GameData gameData)
    {
        _currentGameData = gameData;
        _score = _currentGameData.Money;
        LeaderboardPostBtn();
        Debug.Log("!!!!!!!!!!!--------- 2 PrepareRewardData Leaderboard ---------!!!!!!!!!!!");
    }

    public void LoadLeaderboard()
    {
        OnPressLeaderboardButton?.Invoke();
        Debug.Log("!!!!!!!!!!!--------- 1 Invoke OnPressLeaderboardButton Leaderboard ---------!!!!!!!!!!!");
    }

    public void ShowLeaderboard()
    {
        Social.ShowLeaderboardUI();
        Debug.Log("!!!!!!!!!!!--------- 5 ShowLeaderboard Leaderboard ---------!!!!!!!!!!!");
    }

    public void DoLeaderboardPost(int _score)
    {
        Social.ReportScore(_score, GPGSIds.leaderboard_planet_builder_leaderboard,
            (bool success) =>
            {
                if (success)
                {
                    logText.text = "Score Posted of: " + _score;

                }
                else
                {
                    logText.text = "Score Failed to Post";
                }
            });

        OnShowLeaderboard?.Invoke();
        Debug.Log("!!!!!!!!!!!--------- 4 DoLeaderboardPost Leaderboard ---------!!!!!!!!!!!");
    }

    public void LeaderboardPostBtn()
    {
        DoLeaderboardPost((int)_score);
        Debug.Log("!!!!!!!!!!!--------- 3 LeaderboardPostBtn Leaderboard ---------!!!!!!!!!!!");
    }

    internal void SetStandBy(string message)
    {
        mStandby = true;
        mStandbyMessage = message;
    }

    internal void EndStandBy()
    {
        mStandby = false;
    }

    internal string Status
    {
        get { return _mStatus; }
        set { _mStatus = value; }
    }

    private void OnSignInResult(SignInStatus signInStatus)
    {
        EndStandBy();
        if (signInStatus == SignInStatus.Success)
        {
            Status = "Authenticated. Hello, " + Social.localUser.userName + " (" + Social.localUser.id + ")";
        }
        else
        {
            Status = "*** Failed to authenticate with " + signInStatus;
        }

        //ShowEffect(signInStatus == SignInStatus.Success);
    }

    private void OnDisable()
    {
        OnShowLeaderboard.RemoveListener(ShowLeaderboard);
    }
}
