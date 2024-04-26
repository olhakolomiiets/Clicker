using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;

namespace CBS.UI
{
    public class PopupViewer
    {
        public void ShowSimplePopup(PopupRequest request)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var popupPrefab = uiData.SimplePopup;
            var popupObject = UIView.ShowWindow(popupPrefab);
            popupObject.GetComponent<SimplePopup>().Setup(request);
        }

        public void ShowYesNoPopup(YesOrNoPopupRequest request)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var popupPrefab = uiData.YesNoPopup;
            var popupObject = UIView.ShowWindow(popupPrefab);
            popupObject.GetComponent<YesNoPopup>().Setup(request);
        }

        public void ShowEditTextPopup(EditTextPopupRequest request)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var popupPrefab = uiData.EditTextPopup;
            var popupObject = UIView.ShowWindow(popupPrefab);
            popupObject.GetComponent<EditTextPopup>().Setup(request);
        }

        public void ShowChangeRolePopup(ChangeRolePopupRequest request)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var popupPrefab = uiData.ChangeRolePopup;
            var popupObject = UIView.ShowWindow(popupPrefab);
            popupObject.GetComponent<ChangeRolePopup>().Setup(request);
        }

        public void ShowFabError(CBSError error)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var popupPrefab = uiData.SimplePopup;
            var popupObject = UIView.ShowWindow(popupPrefab);

            var request = new PopupRequest
            {
                Title = AuthTXTHandler.ErrorTitle,
                Body = error.Message
            };

            popupObject.GetComponent<SimplePopup>().Setup(request);
        }

        public void ShowStackError(CBSError error)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var popupPrefab = uiData.SimplePopup;
            var popupObject = UIView.ShowWindow(popupPrefab);

            var request = new PopupRequest
            {
                Title = AuthTXTHandler.ErrorTitle,
                Body = error.Message
            };

            popupObject.GetComponent<SimplePopup>().Setup(request);
        }

        public void ShowUserInfo(string profileIDToShow)
        {
            if (string.IsNullOrEmpty(profileIDToShow))
                return;
            var profile = CBSModule.Get<CBSProfileModule>();
            string profileID = profile.ProfileID;
            if (profileID == profileIDToShow)
                return;

            profile.GetProfileDetail(new CBSGetProfileRequest
            {
                ProfileID = profileIDToShow,
                Constraints = new CBSProfileConstraints
                {
                    LoadClan = true,
                    LoadLevel = true,
                    LoadAvatar = true,
                    LoadOnlineStatus = true
                }
            }, onGet =>
            {
                if (onGet.IsSuccess)
                {
                    var uiData = CBSScriptable.Get<PopupPrefabs>();
                    var formPrefab = uiData.UserInfoForm;
                    var formObject = UIView.ShowWindow(formPrefab);
                    var formUI = formObject.GetComponent<UserInfoForm>();
                    formUI.Display(onGet);
                }
                else
                {
                    new PopupViewer().ShowFabError(onGet.Error);
                }
            });
        }

        public void ShowClanInfo(string clanID)
        {
            if (string.IsNullOrEmpty(clanID))
                return;
            var clan = CBSModule.Get<CBSClanModule>();

            clan.GetClanEntity(clanID, CBSClanConstraints.Full(), onGet =>
            {
                if (onGet.IsSuccess)
                {
                    var uiData = CBSScriptable.Get<PopupPrefabs>();
                    var formPrefab = uiData.ClanInfoForm;
                    var formObject = UIView.ShowWindow(formPrefab);
                    var formUI = formObject.GetComponent<ClanInfoForm>();
                    formUI.Display(onGet.ClanEntity);
                }
            });
        }

        public void ShowLoadingPopup()
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var loadingPrefab = uiData.LoadingPopup;
            UIView.ShowWindow(loadingPrefab);
        }

        public void HideLoadingPopup()
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var loadingPrefab = uiData.LoadingPopup;
            UIView.HideWindow(loadingPrefab);
        }

        public void ShowRewardPopup(RewardObject prize)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var rewardPrefab = uiData.RewardPopup;
            var popupObject = UIView.ShowWindow(rewardPrefab);
            popupObject.GetComponent<RewardDrawer>().Display(prize);
        }

        public void ShowItemPopup(string itemID, string title, Action onOk = null)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var rewardPrefab = uiData.ItemPopup;
            var popupObject = UIView.ShowWindow(rewardPrefab);
            popupObject.GetComponent<ItemPopup>().Setup(itemID, title, onOk);
        }

        public void ShowMessagePopup(ChatMessage message, Action<ChatMessage> onModify = null)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var messagePrefab = uiData.MessagePopup;
            var popupObject = UIView.ShowWindow(messagePrefab);
            popupObject.GetComponent<MessagePopup>().Show(message, onModify);
        }

        public void ShowStickersPopup(Action<ChatSticker> onSelect)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var stickersPrefab = uiData.StickersPopup;
            var popupObject = UIView.ShowWindow(stickersPrefab);
            popupObject.GetComponent<SelectStickerPopup>().Show(onSelect);
        }

        public void ShowItemsPopup(Action<CBSInventoryItem> onSelect)
        {
            var uiData = CBSScriptable.Get<PopupPrefabs>();
            var stickersPrefab = uiData.ItemsPopup;
            var popupObject = UIView.ShowWindow(stickersPrefab);
            popupObject.GetComponent<SelectItemPopup>().Show(onSelect);
        }
    }
}
