using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetFriendsResult : CBSBaseResult
    {
        public List<ProfileEntity> Friends;
        public int MaxValue;
        public int CurrentCount;
    }
}
