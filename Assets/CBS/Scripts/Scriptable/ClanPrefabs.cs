using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "ClanPrefabs", menuName = "CBS/Add new Clan Prefabs")]
    public class ClanPrefabs : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/ClanPrefabs";

        public GameObject WindowLoader;
        public GameObject ClanInviteResult;
        public GameObject ClanRequestedUser;
        public GameObject ClanMember;
        public GameObject ClanTask;
        public GameObject ColorButton;
    }
}
