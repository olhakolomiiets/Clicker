using System;

namespace CBS.UI
{
    public interface IClanScreen
    {
        Action OnBack { get; set; }
        void Show();
        void Hide();
    }
}

