using System;
using System.Collections.Generic;
using System.Reflection;

namespace CBS.Utils
{
    public static class TypeUtils
    {
        public static bool IsSupportedList(FieldInfo f, object target)
        {
            return f.FieldType.IsGenericType && (f.GetValue(target) is List<string> || f.GetValue(target) is List<int> || f.GetValue(target) is List<float>);
        }

        public static bool IsSupportedList(object target)
        {
            if (target == null)
                return false;
            Type t = target.GetType();
            return t == typeof(List<string>) || t == typeof(List<int>) || t == typeof(List<float>);
        }

        public static bool IsFloat(object target)
        {
            if (target == null)
                return false;
            Type t = target.GetType();
            return t == typeof(float);
        }

        public static bool IsInt(object target)
        {
            if (target == null)
                return false;
            Type t = target.GetType();
            return t == typeof(int);
        }
    }
}
