using System;
using UnityEngine;

namespace CBS.UI
{
    public class ClanInventoryScreen : MonoBehaviour, IClanScreen
    {
        [SerializeField]
        private ProfileInventoryInsideClan ProfileInventory;
        [SerializeField]
        private ClanInventoryDrawer ClanInventory;

        public Action OnBack { get; set; }

        private void Start()
        {
            ProfileInventory.UpdateRequest = UpdateRequest;
            ClanInventory.UpdateRequest = UpdateRequest;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            UpdateRequest();
        }

        public void BackHandler()
        {
            OnBack?.Invoke();
        }

        private void UpdateRequest()
        {
            ProfileInventory.LoadInventory();
            ClanInventory.LoadInventory();
        }
    }
}
