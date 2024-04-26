namespace CBS.Models
{
    public class CBSGetTitleDataResult<T> : CBSBaseResult where T : TitleCustomData
    {
        public T Data;
        public CBSTitleData CBSTitleData;
    }
}
