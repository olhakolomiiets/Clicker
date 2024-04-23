using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using GooglePlayGames;
using UnityEngine.SocialPlatforms;
using System.Collections.Generic;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
using DG.Tweening;
using System.Linq;
using System.Collections;

public class GPGSLeaderboard : MonoBehaviour
{
    [SerializeField] private GameObject leaderboardEntryPrefab;
    [SerializeField] private RectTransform leaderboardContainer;

    [Space(10)]
    [SerializeField] private RectTransform leaderboardPanel;
    [SerializeField] private float panelTopPosY, panelMiddlePosY;
    [SerializeField] private float tweenDuration;

    #region PRIVATE FIELDS

    GameData _currentGameData;
    private double _score;
    private bool mStandby = false;
    private string mStandbyMessage = string.Empty;
    private string _mStatus = "Ready";

    private string mStatus;
    private string logText;
    private bool isDisplayed;

    private List<GPGSLeaderboardEntry> leaderboardEntryList = new();

    Texture2D userImg;

    #endregion

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
    }

    public void LoadLeaderboard()
    {
        OnPressLeaderboardButton?.Invoke();
    }

    public void ShowLeaderboard()
    {
        Social.ShowLeaderboardUI();
    }

    public void DoLeaderboardPost(int _score)
    {
        Social.ReportScore(_score, GPGSIds.leaderboard_planet_builder_leaderboard,
            (bool success) =>
            {
                if (success)
                {
                    logText = "Score Posted of: " + _score;

                }
                else
                {
                    logText = "Score Failed to Post";
                }
            });

        //OnShowLeaderboard?.Invoke();

        DoLoadLeaderboard();
    }

    public void LeaderboardPostBtn()
    {
        DoLeaderboardPost((int)_score);
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
    }

    internal void DoLoadLeaderboard()
    {
        ILeaderboard lb = PlayGamesPlatform.Instance.CreateLeaderboard();
        lb.id = GPGSIds.leaderboard_planet_builder_leaderboard;
        lb.LoadScores(ok =>
        {
            if (ok)
            {
                LoadUsersAndDisplay(lb);
            }
            else
            {
                mStatus = "Leaderboard loading: " + lb.title + " ok = " + ok;
            }
        });
    }

    internal void LoadUsersAndDisplay(ILeaderboard lb)
    {
        List<string> userIds = new();       

        foreach (IScore score in lb.scores)
        {
            userIds.Add(score.userID);
        }

        Social.LoadUsers(userIds.ToArray(), (users) =>
        {
            mStatus = "Leaderboard loading: " + lb.title + " count = " +
                      lb.scores.Length;

            foreach (IScore score in lb.scores)
            {
                IUserProfile user = FindUser(users, score.userID);

                StartCoroutine(GetImg(user.image));

                mStatus += "\n" + score.formattedValue + " by " +
                           (string)(
                               (user != null) ? user.userName : "**unk_" + score.userID + "**");

                GPGSLeaderboardEntry entry = Instantiate(leaderboardEntryPrefab, leaderboardContainer).GetComponent<GPGSLeaderboardEntry>();
                entry.SetData(score.rank, user.userName, userImg, score.userID, score.value);
                leaderboardEntryList.Add(entry);                               

                float _scrollHeight = 220 * lb.scores.Length;
                leaderboardContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _scrollHeight);

                Debug.Log("!!!!!----- Leaderboard Status -----!!!!!" + " Username: " + user.userName + " User ID: " + score.userID + " Score Value: " + score.value + " Score Formatted Value: " + score.formattedValue);                
            }
        });

        ToggleLeaderboardPanel();
    }

    IEnumerator GetImg(Texture2D userTexture)
    {
        while (userTexture == null)
        {            
            yield return null;
        }

        yield return userImg;
    }

    private IUserProfile FindUser(IUserProfile[] users, string userid)
    {
        foreach (IUserProfile user in users)
        {
            if (user.id == userid)
            {
                return user;
            }
        }

        return null;
    }

    public void ToggleLeaderboardPanel()
    {
        if (isDisplayed)
        {
            leaderboardPanel.DOAnchorPosY(panelMiddlePosY, tweenDuration);
            isDisplayed = false;
            leaderboardEntryList.Clear();
        }
        else
        {
            leaderboardPanel.DOAnchorPosY(panelTopPosY, tweenDuration);
            isDisplayed = true;
        }
    }

    private void OnDisable()
    {
        OnShowLeaderboard.RemoveListener(ShowLeaderboard);
    }
}
