using CBS.Models;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ClanMetaIcon : MonoBehaviour
    {
        [SerializeField]
        private Text DisplayName;

        public void Init(ClanEntity clanEntity)
        {
            DisplayName.text = clanEntity.DisplayName;
        }
    }
}