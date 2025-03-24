using UnityEngine;
using System;

public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        string wrappedJson = "{ \"items\": " + json + " }";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrappedJson);
        return wrapper.items;
    }

    [Serializable]
    private class Wrapper<T>
    {
        public T[] items;
    }
}
