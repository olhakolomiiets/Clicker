using UnityEngine;

namespace CBS.UI
{
    public class SpriteFitter : MonoBehaviour
    {
        private void Start()
        {
            ResizeSpriteToScreen();
        }

        private void ResizeSpriteToScreen()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;

            transform.localScale = new Vector3(1, 1, 1);

            var width = sr.sprite.bounds.size.x;
            var height = sr.sprite.bounds.size.y;

            var worldScreenHeight = Camera.main.orthographicSize * 2.0f;
            var worldScreenWidth = worldScreenHeight / Screen.height * Screen.width;

            transform.localScale = new Vector2(worldScreenWidth / width, worldScreenHeight / height);
        }
    }
}

