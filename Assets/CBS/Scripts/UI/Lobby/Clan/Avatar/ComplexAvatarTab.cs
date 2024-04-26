using UnityEngine;

namespace CBS.UI
{
    public class ComplexAvatarTab : MonoBehaviour
    {
        [SerializeField]
        private AvatarComplexPart TabType;

        public AvatarComplexPart GetTabType()
        {
            return TabType;
        }
    }
}
