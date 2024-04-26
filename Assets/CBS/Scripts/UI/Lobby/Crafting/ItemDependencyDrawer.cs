using CBS.Models;
using CBS.Scriptable;
using UnityEngine;

namespace CBS.UI
{
    public class ItemDependencyDrawer : MonoBehaviour
    {
        [SerializeField]
        private ItemDependencyScroller Scroller;

        private CraftPrefabs CraftPrefabs { get; set; }

        private void Awake()
        {
            CraftPrefabs = CBSScriptable.Get<CraftPrefabs>();
        }

        public void Draw(CraftStateContainer dependencyState)
        {
            var list = dependencyState.GetDependencyList();
            var prefabUI = CraftPrefabs.DependencySlot;
            Scroller.Spawn(prefabUI, list);
        }
    }
}
