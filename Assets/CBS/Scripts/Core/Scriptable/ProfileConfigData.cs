using CBS.Models;
using CBS.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "ProfileConfigData", menuName = "CBS/Add new Profile Config Data")]
    public class ProfileConfigData : CBSScriptable
    {
        public override string ResourcePath => "Scriptable/Core/ProfileConfigData";

        [Header("Online Status")]
        public bool EnableOnlineStatus;
        public OnlineStatusBehavior OnlineUpdateBehavior;
        public int ConsiderInactiveAfter = 60;
        public int UpdateInterval = 10;
        public List<string> TriggerMethods;

        [Header("Avatars")]
        public AvatarDisplayOptions AvatarDisplay;
        public bool UseCacheForAvatars;
        public Sprite DefaultAvatar;

        public bool UseImageCache
        {
            get
            {
#if UNITY_WEBGL
                return false;
#endif
                return UseCacheForAvatars;
            }
        }
    }
}
