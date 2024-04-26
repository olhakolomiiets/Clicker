using UnityEngine;

namespace CBS.UI
{
    public class ProfileTab : BaseTab<string>
    {
        [SerializeField]
        private ProfileSection TabTitle;

        private void Start()
        {
            TabObject = TabTitle.ToString();
        }
    }
}
