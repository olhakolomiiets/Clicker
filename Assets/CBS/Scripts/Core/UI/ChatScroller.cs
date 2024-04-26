using CBS.Models;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CBS.Core
{
    public class ChatScroller : PreloadScroller<ChatMessage>
    {
        [SerializeField]
        private float AutoScrollSpeed = 10f;
        protected override float DeltaToPreload => 0.7f;

        public GameObject GetContentRoot()
        {
            return Scroll.content.gameObject;
        }

        public void PushNewMessage(ChatMessage message)
        {
            PushNew(message);
            SetScrollPosition(0);
        }

        public void SetScrollPosition(float position)
        {
            StartCoroutine(ApplyScrollPosition(Scroll, position));
        }

        public void SetPoolCount(int count)
        {
            PoolCount = count;
        }

        IEnumerator ApplyScrollPosition(ScrollRect sr, float verticalPos)
        {
            yield return new WaitUntil(() => Scroll != null);
            yield return new WaitForEndOfFrame();
            if (NeedToExtend)
            {
                while (sr.verticalNormalizedPosition > verticalPos && NeedToExtend)
                {
                    sr.verticalNormalizedPosition -= AutoScrollSpeed * Time.deltaTime;
                    yield return null;
                    MoveNext();
                }
            }
            else
            {
                while (sr.verticalNormalizedPosition > verticalPos)
                {
                    sr.verticalNormalizedPosition -= AutoScrollSpeed * Time.deltaTime;
                    yield return null;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)sr.transform);
        }
    }
}
