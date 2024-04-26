using CBS.Core;
using System;
using UnityEngine;

namespace CBS.UI
{
    public class CraftDropArea : MonoBehaviour, IDropContainer
    {
        public event Action<DraggableInventorySlot> OnDropSlot;

        public void OnDropItem(GameObject item)
        {
            var draggableInventory = item.GetComponent<DraggableInventorySlot>();
            if (draggableInventory != null)
            {
                OnDropSlot?.Invoke(draggableInventory);
            }
        }
    }
}
