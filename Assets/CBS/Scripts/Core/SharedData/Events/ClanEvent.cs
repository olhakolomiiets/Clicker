
namespace CBS.Models
{
    public class ClanEvent
    {
        public ClanEventType EventType;
        public string ContentBodyRaw;

        public string GetRawData()
        {
            return string.IsNullOrEmpty(ContentBodyRaw) ? JsonPlugin.EMPTY_JSON : ContentBodyRaw;
        }

        public T GetContent<T>() where T : ClanEvent
        {
            return JsonPlugin.FromJson<T>(GetRawData());
        }

        public void SaveContent(object data)
        {
            ContentBodyRaw = JsonPlugin.ToJson(data);
        }
    }
}
