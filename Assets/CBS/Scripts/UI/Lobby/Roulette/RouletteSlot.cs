using CBS.Core;
using CBS.Models;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.UI
{
    public class RouletteSlot : MonoBehaviour, IScrollableItem<RoulettePosition>
    {
        [SerializeField]
        private Image Icon;
        [SerializeField]
        private Image Background;

        private RoulettePosition Position { get; set; }

        public string ID => Position.ID;

        public void Display(RoulettePosition data)
        {
            Position = data;
            var id = Position.ID;
            Icon.sprite = data.GetSprite();
            ToDefault();
        }

        public void ToDefault()
        {
            Background.enabled = false;
        }

        public void Activate()
        {
            Background.enabled = true;
        }
    }
}
