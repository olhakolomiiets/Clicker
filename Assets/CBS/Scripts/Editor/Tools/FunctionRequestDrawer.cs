using CBS.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class FunctionRequestDrawer<T> where T : ExecuteFunctionProfileArgs
    {
        private List<Type> AllDataTypes = new List<Type>();
        public string ClassName { get; set; }
        public int SelectedTypeIndex { get; set; }
        private bool ReachLimit { get; set; }

        private string LastSavedObject { get; set; }

        private Dictionary<string, string> RawDataCache;
        private ConfiguratorWindow Configurator;

        public bool AutoReset { get; set; }
        public float ProgressBarXOfseet = 3;

        public FunctionRequestDrawer()
        {
            AutoReset = false;
            Configurator = ConfiguratorWindow.Active;
            AllDataTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()).Where(t => t.IsSubclassOf(typeof(T))).ToList();
        }

        public void Reset()
        {
            SelectedTypeIndex = 0;
            ClassName = string.Empty;
            RawDataCache = new Dictionary<string, string>();
            GUI.FocusControl(null);
        }

        private bool HasProperty(dynamic obj, string name)
        {
            Type typeOfDynamic = obj.GetType();
            return typeOfDynamic.GetFields().Where(p => p.Name.Equals(name)).Any();
        }

        private object GetProperty(dynamic obj, string name)
        {
            try
            {
                return obj[name];
            }
            catch
            {
                return null;
            }
        }

        public string Draw(string objectToDraw)
        {
            ReachLimit = false;
            if (string.IsNullOrEmpty(objectToDraw))
                objectToDraw = JsonPlugin.EMPTY_JSON;
            if (objectToDraw != LastSavedObject && AutoReset)
            {
                LastSavedObject = objectToDraw;
                Reset();
            }

            var dynamicObj = JsonPlugin.FromJson<dynamic>(objectToDraw);
            var nameObject = GetProperty(dynamicObj, "ClassName");
            var className = nameObject == null ? string.Empty : (string)nameObject;

            var itemClassType = string.IsNullOrEmpty(className) ? AllDataTypes.FirstOrDefault() : AllDataTypes.Where(x => x.Name == className).FirstOrDefault();
            SelectedTypeIndex = AllDataTypes.IndexOf(itemClassType);

            SelectedTypeIndex = EditorGUILayout.Popup(SelectedTypeIndex, AllDataTypes.Select(x => x.Name).ToArray(), GUILayout.Width(200));

            if (SelectedTypeIndex < 0)
                SelectedTypeIndex = 0;

            var selectedType = AllDataTypes[SelectedTypeIndex];
            className = selectedType.Name;
            ClassName = className;

            var target = JsonUtility.FromJson(objectToDraw, selectedType);

            foreach (var f in selectedType.GetFields().Where(f => f.IsPublic))
            {
                if (f.Name == "ClassName")
                {
                    f.SetValue(target, ClassName);
                }
                else if (f.Name == "ProfileID")
                {
                    continue;
                }
                // draw string
                else if (f.FieldType == typeof(string))
                {
                    string stringTitle = f.Name;
                    string stringValue = f.GetValue(target) == null ? string.Empty : f.GetValue(target).ToString();
                    var text = EditorGUILayout.TextField(stringTitle, stringValue);
                    f.SetValue(target, text);
                }
                // draw int
                else if (f.FieldType == typeof(int))
                {
                    string stringTitle = f.Name;
                    int intValue = (int)f.GetValue(target);
                    var i = EditorGUILayout.IntField(stringTitle, intValue);
                    f.SetValue(target, i);
                }
                // draw float
                else if (f.FieldType == typeof(float))
                {
                    string stringTitle = f.Name;
                    float floatValue = (float)f.GetValue(target);
                    var fl = EditorGUILayout.FloatField(stringTitle, floatValue);
                    f.SetValue(target, fl);
                }
                // draw bool
                else if (f.FieldType == typeof(bool))
                {
                    string stringTitle = f.Name;
                    bool boolValue = (bool)f.GetValue(target);
                    var b = EditorGUILayout.Toggle(stringTitle, boolValue);
                    f.SetValue(target, b);
                }
                // draw enum
                else if (f.FieldType.IsEnum)
                {
                    var enumType = f.FieldType;
                    var enumList = Enum.GetNames(enumType);
                    string stringTitle = f.Name;
                    int enumValue = (int)f.GetValue(target);
                    var e = EditorGUILayout.Popup(enumValue, enumList);
                    f.SetValue(target, e);
                }
                // draw list
                else if (TypeUtils.IsSupportedList(f, target))
                {
                    var listName = f.Name;
                    var objValue = f.GetValue(target);
                    IList tempList;
                    Configurator.GetTempList(objValue, out tempList);
                    var tempListName = GetTempListName(objValue);

                    ScriptableObject config = Configurator;
                    SerializedObject so = new SerializedObject(config);
                    SerializedProperty stringsProperty = so.FindProperty(tempListName);

                    EditorGUILayout.PropertyField(stringsProperty, new GUIContent(listName), true); // True means show children
                    so.ApplyModifiedProperties(); // Remember to apply modified properties*/

                    f.SetValue(target, tempList);
                }
            }

            var resultRaw = JsonUtility.ToJson(target);
            return resultRaw;
        }

        private string GetTempListName(object target)
        {
            if (target is List<string>)
                return "TempStringList";
            if (target is List<int>)
                return "TempIntList";
            if (target is List<float>)
                return "TempFloatList";
            return null;
        }

        public bool IsInputValid()
        {
            return !ReachLimit;
        }
    }
}
