using PlanetBuilder.Messages;

namespace PlanetBuilder.Messages.UI
{
    public class DialogView : MessageView
    {
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
