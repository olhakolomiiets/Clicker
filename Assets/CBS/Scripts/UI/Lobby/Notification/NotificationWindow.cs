using CBS.Models;
using CBS.Scriptable;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class NotificationWindow : MonoBehaviour
    {
        [SerializeField]
        private NotificationTabListener TabListener;
        [SerializeField]
        private NotificationTitleScroller NotificationScroller;
        [SerializeField]
        private MessageDialogScroller DialogScroller;
        [SerializeField]
        private NotificationDrawer NotificationDrawer;
        [SerializeField]
        private ManualUpdateBadge MessageBadge;
        [SerializeField]
        private ChatView ChatView;
        [SerializeField]
        private ToggleGroup DialogGroup;
        [SerializeField]
        private string SpecificCategory;
        [SerializeField]
        private NotificationRequest Query;

        private NotificationSlot ActiveSlot { get; set; }
        private INotificationCenter Notification { get; set; }
        private ICBSChat CBSChat { get; set; }
        private NotificationPrefabs NotificationPrefabs { get; set; }
        private ChatPrefabs ChatPrefabs { get; set; }
        private List<CBSNotification> ActiveNotifications { get; set; }

        private void Awake()
        {
            Notification = CBSModule.Get<CBSNotificationModule>();
            CBSChat = CBSModule.Get<CBSChatModule>();
            NotificationPrefabs = CBSScriptable.Get<NotificationPrefabs>();
            ChatPrefabs = CBSScriptable.Get<ChatPrefabs>();
            TabListener.OnTabSelected += OnTabSelected;
            NotificationDrawer.OnModifyNotification = OnModifyNotification;
            NotificationDrawer.OnRemoveNotification = OnRemoveNotification;
        }

        private void OnDestroy()
        {
            TabListener.OnTabSelected -= OnTabSelected;
        }

        private void OnEnable()
        {
            ClearContent();
            var activeTab = TabListener.ActiveTab;
            DisplayTab(activeTab);
            UpdateMessagesBadge();
        }

        private void OnDisable()
        {
            ActiveSlot = null;
            ActiveNotifications = null;
        }

        private void DisplayTab(NotificationMenu tab)
        {
            NotificationScroller.HideAll();
            NotificationDrawer.Clear();
            DialogScroller.HideAll();
            NotificationScroller.gameObject.SetActive(tab == NotificationMenu.MESSAGES);
            DialogScroller.gameObject.SetActive(tab == NotificationMenu.PRIVATE);
            ChatView.Dispose();
            ChatView.gameObject.SetActive(false);
            if (tab == NotificationMenu.MESSAGES)
            {
                var notificationsRequest = new CBSGetNotificationsRequest
                {
                    SpecificCategory = SpecificCategory,
                    Request = Query
                };
                Notification.GetNotificationList(notificationsRequest, OnGetNotificationList);
            }
            else if (tab == NotificationMenu.PRIVATE)
            {
                NotificationDrawer.gameObject.SetActive(false);
                CBSChat.GetProfileDialogList(OnGetDialogList);
            }
        }

        private void DisplayNotification(NotificationSlot notificationSlot)
        {
            NotificationDrawer.Draw(notificationSlot.Notification, notificationSlot);
        }

        private void DrawNotificationList(List<CBSNotification> notifications)
        {
            var slotPrefab = NotificationPrefabs.NotificationTitle;
            var notificationRequest = notifications.Select(x => new NotificationSlotRequest
            {
                Notification = x,
                Active = ActiveSlot == null ? null : ActiveSlot.Notification,
                SelectAction = OnSelectNotification
            }).ToList();
            NotificationScroller.Spawn(slotPrefab, notificationRequest);
        }

        private void DrawDialogList(List<ChatDialogEntry> dialogList)
        {
            ClearContent();
            var slotPrefab = ChatPrefabs.DialogSlot;
            var slotRequest = dialogList.Select(x => new DialogSlotRequest
            {
                DialogEntry = x,
                SelectAction = OnSelectDialog,
                Group = DialogGroup
            }).ToList();
            DialogScroller.Spawn(slotPrefab, slotRequest);
        }

        private void UpdateMessagesBadge(List<CBSNotification> notifications = null)
        {
            if (notifications == null)
                MessageBadge.UpdateManual(0);
            else
            {
                var unreadCount = notifications.Where(x => x != null && !x.ReadAndRewarded()).Count();
                MessageBadge.UpdateManual(unreadCount);
            }
        }

        private void ClearContent()
        {
            DialogScroller.HideAll();
            NotificationScroller.HideAll();
            DialogGroup.SetAllTogglesOff();
            NotificationDrawer.Clear();
        }

        // events

        private void OnTabSelected(NotificationMenu tab)
        {
            DisplayTab(tab);
        }

        private void OnGetNotificationList(CBSGetNotificationsResult result)
        {
            if (result.IsSuccess)
            {
                ClearContent();
                ActiveNotifications = result.Notifications;
                DrawNotificationList(ActiveNotifications);
                UpdateMessagesBadge(ActiveNotifications);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnGetDialogList(CBSGetDialogListResult result)
        {
            if (result.IsSuccess)
            {
                var dialogList = result.DialogList;
                DrawDialogList(dialogList);
            }
            else
            {
                new PopupViewer().ShowFabError(result.Error);
            }
        }

        private void OnSelectNotification(NotificationSlot slot)
        {
            NotificationDrawer.gameObject.SetActive(true);
            NotificationDrawer.Clear();
            ActiveSlot?.DeSelect();
            ActiveSlot = slot;
            slot.Select();
            DisplayNotification(slot);
        }

        private void OnSelectDialog(ChatDialogEntry dialogEntry)
        {
            ChatView.gameObject.SetActive(true);
            ChatView.Dispose();
            var withProfileID = dialogEntry.InterlocutorProfile.ProfileID;
            var chatInstanse = CBSChat.GetOrCreatePrivateChatWithProfile(withProfileID);
            ChatView.Init(chatInstanse);
            CBSChat.ClearDialogBadgeWithProfile(withProfileID, onClear =>
            {
                if (!onClear.IsSuccess)
                {
                    new PopupViewer().ShowFabError(onClear.Error);
                }
            });
        }

        private void OnModifyNotification(CBSNotification notification)
        {
            var notificationID = notification.InstanceID;
            var listNotification = ActiveNotifications.FirstOrDefault(x => x.InstanceID == notificationID);
            var notificationIndex = ActiveNotifications.IndexOf(listNotification);
            ActiveNotifications[notificationIndex] = notification;
            DrawNotificationList(ActiveNotifications);
            UpdateMessagesBadge(ActiveNotifications);
        }

        private void OnRemoveNotification(CBSNotification notification)
        {
            var notificationID = notification.InstanceID;
            var listNotification = ActiveNotifications.FirstOrDefault(x => x.InstanceID == notificationID);
            ActiveNotifications.Remove(listNotification);
            ActiveNotifications.TrimExcess();
            ClearContent();
            DrawNotificationList(ActiveNotifications);
        }
    }
}

