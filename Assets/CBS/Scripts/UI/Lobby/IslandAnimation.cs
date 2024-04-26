using UnityEngine;

namespace CBS.UI
{
    public class IslandAnimation : MonoBehaviour
    {
        [SerializeField]
        private float Speed;
        [SerializeField]
        private float Amount;
        [SerializeField]
        private bool Up;

        private Vector2 StartPosition { get; set; }
        private Vector2 UpBorder { get; set; }
        private Vector2 BottomBorder { get; set; }

        private void Start()
        {
            StartPosition = transform.position;
            UpBorder = new Vector2
            {
                x = StartPosition.x,
                y = StartPosition.y + Amount / 2f
            };
            BottomBorder = new Vector2
            {
                x = StartPosition.x,
                y = StartPosition.y - Amount / 2f
            };
        }

        private void Update()
        {
            AnimateLoop();
        }

        private void AnimateLoop()
        {
            Vector2 position = transform.position;
            if (Up)
            {
                position = Vector2.Lerp(position, UpBorder, Time.deltaTime * Speed);
                if (Vector2.Distance(position, UpBorder) < 0.05f)
                    Up = !Up;
            }
            else
            {
                position = Vector2.Lerp(position, BottomBorder, Time.deltaTime * Speed);
                if (Vector2.Distance(position, BottomBorder) < 0.05f)
                    Up = !Up;
            }
            transform.position = position;
        }
    }
}

