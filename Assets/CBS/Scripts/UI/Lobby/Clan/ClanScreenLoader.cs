using System.Collections.Generic;
using UnityEngine;

namespace CBS.UI
{
    public class ClanScreenLoader : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private ClanGeneral GeneralScreen;
        [SerializeField] private CreateClanForm CreateScreen;
        [SerializeField] private ClanSearchForm SearchScreen;
        [SerializeField] private ProfileInvitations InvitationsScreen;
        [SerializeField] private ClanRequests RequestsScreen;
        [SerializeField] private ClanMetaScreen MetaScreen;
        [SerializeField] private ClanMembers MembersScreen;
        [SerializeField] private ClanChat ChatScreen;
        [SerializeField] private ClanInventoryScreen InventoryScreen;
        [SerializeField] private ClanTasksScreen TasksScreen;

        private Dictionary<ClanScreen, IClanScreen> Screens { get; set; }
        private IProfile Profile { get; set; }
        private IClanScreen LastScreen { get; set; }

        private void Awake()
        {
            Profile = CBSModule.Get<CBSProfileModule>();
            RegisterScreens();
        }

        private void OnEnable()
        {
            GeneralScreen.OnLoadScreen += OnLoadScreen;
            InitStartScene();
        }

        private void OnDisable()
        {
            GeneralScreen.OnLoadScreen -= OnLoadScreen;
        }

        private void RegisterScreens()
        {
            Screens = new Dictionary<ClanScreen, IClanScreen>();
            Screens[ClanScreen.CREATE_CLAN] = CreateScreen;
            Screens[ClanScreen.SEARCH_CLAN] = SearchScreen;
            Screens[ClanScreen.INVATIONS] = InvitationsScreen;
            Screens[ClanScreen.REQUESTS] = RequestsScreen;
            Screens[ClanScreen.CLAN_META] = MetaScreen;
            Screens[ClanScreen.MEMBERS] = MembersScreen;
            Screens[ClanScreen.CHAT] = ChatScreen;
            Screens[ClanScreen.INVENTORY] = InventoryScreen;
            Screens[ClanScreen.TASKS] = TasksScreen;

            foreach (var screen in Screens.Values)
                screen.OnBack = InitStartScene;
        }

        private void InitStartScene()
        {
            foreach (var screen in Screens.Values)
                screen.Hide();
            GeneralScreen.gameObject.SetActive(true);
            GeneralScreen.Load(Profile.ExistInClan);
        }

        // events
        private void OnLoadScreen(ClanScreen screen)
        {
            LastScreen?.Hide();
            GeneralScreen.gameObject.SetActive(false);
            Screens[screen].Show();
        }
    }
}

