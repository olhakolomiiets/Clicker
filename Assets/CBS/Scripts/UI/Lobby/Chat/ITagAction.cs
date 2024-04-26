
using CBS.Models;
using System;

namespace CBS.UI
{
    public interface ITagAction
    {
        Action<ChatMember> TagAction { get; set; }
    }
}
