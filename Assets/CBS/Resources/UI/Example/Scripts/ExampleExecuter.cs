using CBS.UI;
using CBS.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CBS.Example
{
    public class ExampleExecuter : MonoBehaviour
    {
        [SerializeField]
        private string LobbyScene;
        [SerializeField]
        private InputField ExpInput;
        [SerializeField]
        private InputField ClanExpInput;
        [SerializeField]
        private InputField LeaderboardInput;
        [SerializeField]
        private InputField TournamentsInput;
        [SerializeField]
        private InputField TimeInput;

        private CBSProfileModule Profile { get; set; }
        private CBSClanModule Clan { get; set; }
        private CBSLeaderboardModule Leaderboard { get; set; }

        private string PlayerID { get; set; }

        private void Start()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
            Leaderboard = CBSModule.Get<CBSLeaderboardModule>();
            Clan = CBSModule.Get<CBSClanModule>();
            PlayerID = Profile.ProfileID;
        }

        public void BackToLobby()
        {
            SceneManager.LoadScene(LobbyScene);
        }

        public void AddExpPoints()
        {
            int val = 0;
            string input = ExpInput.text;
            var result = int.TryParse(input, out val);
            if (result)
            {
                Profile.AddExpirienceToProfile(val, onAdd =>
                {
                    if (onAdd.IsSuccess)
                    {
                        new PopupViewer().ShowSimplePopup(new PopupRequest
                        {
                            Title = "Success",
                            Body = "You are successful add exp points"
                        });
                    }
                    else
                    {
                        new PopupViewer().ShowFabError(onAdd.Error);
                    }
                });
            }
        }

        public void AddClanExpPoints()
        {
            int val = 0;
            string input = ExpInput.text;
            var result = int.TryParse(input, out val);
            if (result)
            {
                Clan.AddExpirienceToClan(Profile.ClanID, val, onAdd =>
                {
                    if (onAdd.IsSuccess)
                    {
                        new PopupViewer().ShowSimplePopup(new PopupRequest
                        {
                            Title = "Success",
                            Body = "You are successful add exp points"
                        });
                    }
                    else
                    {
                        new PopupViewer().ShowFabError(onAdd.Error);
                    }
                });
            }
        }

        public void AddClanLeaderboardPoint()
        {
            int val = 0;
            string input = LeaderboardInput.text;
            var result = int.TryParse(input, out val);
            if (result)
            {

            }
        }

        public void AddTimeOffset()
        {
            int val = 0;
            string input = TimeInput.text;
            var result = int.TryParse(input, out val);
#if UNITY_EDITOR
            if (result)
            {
                DateUtils.AddTestHoursToCuurentSession(val);

                new PopupViewer().ShowSimplePopup(new PopupRequest
                {
                    Title = "Success",
                    Body = string.Format("You are successful added {0} hours to time offset for current session. Working only in Unity Editor", val)
                });
            }
#endif
        }
    }
}
