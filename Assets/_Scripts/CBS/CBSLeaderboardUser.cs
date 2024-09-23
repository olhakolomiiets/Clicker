using CBS;
using CBS.Core;
using CBS.Models;
using UnityEngine;
using UnityEngine.UI;
using CBS.Scriptable;
using CBS.Utils;

public class LeaderboardUser : MonoBehaviour, IScrollableItem<ProfileLeaderboardEntry>
{
    [SerializeField] private Image Background;
    [SerializeField] private Text DisplayName;
    [SerializeField] private CBS.UI.PlaceDrawer Place;
    [SerializeField] private Text Value;

    [SerializeField] protected private Image OnlineImage;

    [Header("Colors")]
    [SerializeField] private Color DefaultColor;
    [SerializeField] private Color ActiveColor;

    protected ProfileConfigData Config { get; set; }
    protected bool UseOnline { get; set; }


    private void Awake()
    {
        Config = CBSScriptable.Get<ProfileConfigData>();
        UseOnline = Config.EnableOnlineStatus;
        OnlineImage?.gameObject.SetActive(false);
    }
    public void Display(ProfileLeaderboardEntry data)
    {
        DisplayName.text = data.DisplayName;
        Place.Draw(data.StatisticPosition);
        Value.text = data.StatisticValue.ToString();
        var cbsProfile = CBSModule.Get<CBSProfileModule>();
        string profileID = cbsProfile.ProfileID;
        bool isMine = data.ProfileID == profileID;
        DisplayName.fontStyle = isMine ? FontStyle.Bold : FontStyle.Normal;
        Value.fontStyle = isMine ? FontStyle.Bold : FontStyle.Normal;
        Background.color = isMine ? ActiveColor : DefaultColor;

        var onlineInfo = data.OnlineStatus;
        DrawOnlineStatus(onlineInfo);
    }

    public void DrawOnlineStatus(OnlineStatusData info)
    {
        if (!UseOnline || OnlineImage == null)
            return;
        OnlineImage.gameObject.SetActive(true);
        var isOnline = info != null && info.IsOnline;
        OnlineImage.color = isOnline ? Color.green : Color.red;
    }
}

