using PlanetBuilder.Messages;

namespace PlanetBuilder.Messages.UI
{
    public class TutorialView : TypewriterMessageView
    {
        public override void Hide()
        {
            MessageTutorialTrace.LogHide(
                "TutorialView.Hide",
                gameObject,
                DisplayedMessage != null
                    ? $"messageId={DisplayedMessage.Id} type={DisplayedMessage.Type} channel={DisplayedMessage.Channel}"
                    : "message=null");

            base.Hide();
        }
    }
}
