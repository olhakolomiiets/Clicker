using PlanetBuilder.Messages;
using PlanetBuilder.Messages.Dialogs;
using UnityEngine.EventSystems;

namespace PlanetBuilder.Messages.UI
{
    public class DialogView : MessageView, IPointerClickHandler
    {
        public new void OnPointerClick(PointerEventData eventData)
        {
            MessageManager messageManager = MessageManager.Instance;
            DialogManager dialogManager = messageManager != null
                ? messageManager.GetComponent<DialogManager>()
                : null;

            if (dialogManager != null && dialogManager.IsMetaDialog &&
                DisplayedMessage != null &&
                ReferenceEquals(DisplayedMessage.Owner, dialogManager.ActiveDialog))
            {
                dialogManager.ShowNext();
                return;
            }

            base.OnPointerClick(eventData);
        }

        public override void Hide()
        {
            MessageTutorialTrace.LogHide(
                "DialogView.Hide",
                gameObject,
                DisplayedMessage != null
                    ? $"messageId={DisplayedMessage.Id} type={DisplayedMessage.Type} channel={DisplayedMessage.Channel}"
                    : "message=null");

            base.Hide();
        }
    }
}
