using CBS.Core;
using CBS.Models;
using System.Collections;
using UnityEngine;

namespace CBS.UI
{
    public class BattlePassScroller : PreloadScroller<BattlePassLevelInfo>
    {
        protected override float DeltaToPreload => 0.7f;

        public void SetPoolCount(int count)
        {
            PoolCount = count;
        }

        public void SetScrollPosition(float position)
        {
            if (!gameObject.activeInHierarchy)
                return;
            StartCoroutine(OnSetScrollPosition(position));
        }

        private IEnumerator OnSetScrollPosition(float position)
        {
            yield return new WaitForEndOfFrame();
            if (Direction == ScrollerSide.HORISONTAL)
            {
                Scroll.horizontalNormalizedPosition = position;
            }
            else if (Direction == ScrollerSide.VERTICAL)
            {
                Scroll.verticalNormalizedPosition = position;
            }
        }
    }
}
