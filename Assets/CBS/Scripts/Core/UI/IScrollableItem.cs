namespace CBS.Core
{
    public interface IScrollableItem<T> where T : class
    {
        void Display(T data);
    }
}
