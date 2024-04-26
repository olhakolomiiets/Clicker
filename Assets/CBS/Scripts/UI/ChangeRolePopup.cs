using CBS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class ChangeRolePopup : MonoBehaviour
    {
        [SerializeField]
        private Dropdown RoleDropdown;

        private Action<string> SelectAction { get; set; }
        private Action CancelAction { get; set; }
        private List<ClanRoleInfo> Roles { get; set; }

        // setup popup information
        public void Setup(ChangeRolePopupRequest request)
        {
            Clear();
            Roles = request.Roles;
            var roleID = request.CurrentRoleID;
            SelectAction = request.SelectAction;
            CancelAction = request.CancelAction;
            // setup Drop Down
            RoleDropdown.ClearOptions();
            RoleDropdown.AddOptions(Roles.Select(x => new Dropdown.OptionData { text = x.DisplayName }).ToList());
            var selectedRoleInfo = Roles.FirstOrDefault();
            var selectedIndex = selectedRoleInfo == null ? 0 : Roles.IndexOf(selectedRoleInfo);
            RoleDropdown.value = 1;
            RoleDropdown.value = selectedIndex;
        }

        // reset view
        private void Clear()
        {
            SelectAction = null;
            CancelAction = null;
        }

        // button event
        public void SaveHandler()
        {
            var newRole = Roles[RoleDropdown.value];
            SelectAction?.Invoke(newRole.RoleID);
            gameObject.SetActive(false);
        }

        public void NoHandler()
        {
            CancelAction?.Invoke();
            gameObject.SetActive(false);
        }
    }

    public struct ChangeRolePopupRequest
    {
        public string CurrentRoleID;
        public List<ClanRoleInfo> Roles;
        public Action<string> SelectAction;
        public Action CancelAction;
    }
}
