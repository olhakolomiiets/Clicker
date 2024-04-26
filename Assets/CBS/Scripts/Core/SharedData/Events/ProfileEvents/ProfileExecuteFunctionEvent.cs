namespace CBS.Models
{
    public class ProfileExecuteFunctionEvent : ProfileEvent
    {
        public string FunctionName;
        public string RequestRaw;
    }
}
