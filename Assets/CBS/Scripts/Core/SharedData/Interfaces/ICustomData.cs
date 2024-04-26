namespace CBS.Core
{
    public interface ICustomData<TDataType> where TDataType : CBSBaseCustomData
    {
        string CustomDataClassName { get; set; }
        string CustomRawData { get; set; }
        bool CompressCustomData { get; }

        T GetCustomData<T>() where T : TDataType;
    }
}
