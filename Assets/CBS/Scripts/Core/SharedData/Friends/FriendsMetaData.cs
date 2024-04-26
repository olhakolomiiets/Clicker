

namespace CBS.Models
{
    public class FriendsMetaData
    {
        public static int MAX_FRIEND_VALUE = 800;
        public static int MAX_REQUESTED_VALUE = 200;

        public static int MAX_FRIEND_DEFAULT = 80;
        public static int MAX_REQUESTED_DEFAULT = 20;

        public int MaxFriend;
        public int MaxRequested;

        public static FriendsMetaData Default()
        {
            return new FriendsMetaData
            {
                MaxFriend = MAX_FRIEND_DEFAULT,
                MaxRequested = MAX_REQUESTED_DEFAULT
            };
        }
    }
}
