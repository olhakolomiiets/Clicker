using System.Collections;
using UnityEngine;

namespace CBS.Context
{
    public interface ICoroutineRunner
    {
        Coroutine StartCoroutine(IEnumerator coroutine);

        void StopAllCoroutines();

        void StopCoroutine(Coroutine coroutine);
    }
}
