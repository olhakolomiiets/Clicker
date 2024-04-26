using PlayFab;

namespace CBS
{
    public class JsonPlugin
    {
        public const string EMPTY_JSON = "{}";

        private static ISerializerPlugin Plugin { get; set; }

        static JsonPlugin()
        {
            Plugin = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
        }

        public static string ToJson(object obj)
        {
            return Plugin.SerializeObject(obj);
        }

        public static string ToJsonCompress(object obj)
        {
            var raw = Plugin.SerializeObject(obj);
            return Compressor.Compress(raw);
        }

        public static T FromJson<T>(string rawData)
        {
            return Plugin.DeserializeObject<T>(rawData);
        }

        public static T FromJsonDecompress<T>(string rawData)
        {
            return Plugin.DeserializeObject<T>(Compressor.Decompress(rawData));
        }

        public static bool IsValidJson(string strInput)
        {
            if (string.IsNullOrWhiteSpace(strInput)) return false;

            strInput = strInput.Trim();
            if ((strInput.StartsWith("{") && strInput.EndsWith("}")) ||
                (strInput.StartsWith("[") && strInput.EndsWith("]")))
            {
                try
                {
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }
}