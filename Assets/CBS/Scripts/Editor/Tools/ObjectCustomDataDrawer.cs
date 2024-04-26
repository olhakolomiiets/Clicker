using CBS.Core;
using CBS.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class ObjectCustomDataDrawer<T> where T : CBSBaseCustomData
    {
        private List<Type> AllDataTypes = new List<Type>();
        public string ClassName { get; set; }
        public int SelectedTypeIndex { get; set; }
        private bool ReachLimit { get; set; }
        private string BaseClassName { get; set; }

        private object LastSavedObject { get; set; }

        private SerializedObject ScriptableTarget;

        private Dictionary<string, string> RawDataCache;
        private ConfiguratorWindow Configurator;
        private int MaxRawBytes;
        private float ProgressWidth;

        public bool AutoReset { get; set; }
        public bool DrawOnlyValues { get; set; }
        public float ProgressBarXOfseet = 3;

        public ObjectCustomDataDrawer(int maxBytes, float progressWidth)
        {
            AutoReset = true;
            ProgressWidth = progressWidth;
            MaxRawBytes = maxBytes;
            Configurator = ConfiguratorWindow.Active;
            AllDataTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()).Where(t => t.IsSubclassOf(typeof(T))).ToList();
            BaseClassName = typeof(T).Name;
            ScriptableObject target = Configurator;
            ScriptableTarget = new SerializedObject(target);
        }

        public void Reset()
        {
            SelectedTypeIndex = 0;
            ClassName = string.Empty;
            RawDataCache = new Dictionary<string, string>();
            GUI.FocusControl(null);
        }

        public string Draw<T>(ICustomData<T> objectToDraw) where T : CBSBaseCustomData
        {
            ReachLimit = false;
            if (objectToDraw != LastSavedObject && AutoReset)
            {
                LastSavedObject = objectToDraw;
                Reset();
            }
            var customRawData = objectToDraw.CustomRawData;
            var className = objectToDraw.CustomDataClassName;
            if (string.IsNullOrEmpty(customRawData))
                customRawData = string.Empty;
            if (AllDataTypes.Count == 0)
                return string.Empty;
            if (string.IsNullOrEmpty(className))
            {
                className = string.Empty;
                SelectedTypeIndex = 0;
            }
            var itemClassType = string.IsNullOrEmpty(className) ? AllDataTypes.FirstOrDefault() : AllDataTypes.Where(x => x.Name == className).FirstOrDefault();
            SelectedTypeIndex = AllDataTypes.IndexOf(itemClassType);

            if (!DrawOnlyValues)
                SelectedTypeIndex = EditorGUILayout.Popup(SelectedTypeIndex, AllDataTypes.Select(x => x.Name).ToArray(), GUILayout.Width(ProgressWidth));

            if (SelectedTypeIndex < 0)
                SelectedTypeIndex = 0;

            var selectedType = AllDataTypes[SelectedTypeIndex];
            className = selectedType.Name;

            ClassName = className;

            var cachedRawData = RawDataCache.ContainsKey(ClassName) ? RawDataCache[ClassName] : customRawData;
            int byteCount = System.Text.Encoding.UTF8.GetByteCount(cachedRawData);
            try
            {
                if (objectToDraw.CompressCustomData)
                    cachedRawData = Compressor.Decompress(cachedRawData);
            }
            catch { }
            cachedRawData = JsonPlugin.IsValidJson(cachedRawData) ? cachedRawData : JsonPlugin.EMPTY_JSON;
            var dataObject = JsonUtility.FromJson(cachedRawData, selectedType);
            if (dataObject == null)
            {
                dataObject = Activator.CreateInstance(selectedType);
            }

            // draw raw data progress bar

            if (!DrawOnlyValues)
            {
                float difValue = (float)byteCount / (float)MaxRawBytes;
                string progressTitle = byteCount.ToString() + "/" + MaxRawBytes.ToString() + " bytes";
                float lastY = GUILayoutUtility.GetLastRect().y;
                var lastColor = GUI.color;
                if (byteCount > MaxRawBytes)
                {
                    ReachLimit = true;
                    GUI.color = Color.red;
                }
                else
                {
                    ReachLimit = false;
                }
                EditorGUI.ProgressBar(new Rect(ProgressBarXOfseet, lastY + 25, ProgressWidth - 6, 20), difValue, progressTitle);
                GUI.color = lastColor;
                GUILayout.Space(30);
            }

            foreach (var f in selectedType.GetFields().Where(f => f.IsPublic))
            {
                // draw string
                if (f.FieldType == typeof(string))
                {
                    string stringTitle = f.Name;
                    string stringValue = f.GetValue(dataObject) == null ? string.Empty : f.GetValue(dataObject).ToString();
                    var text = EditorGUILayout.TextField(stringTitle, stringValue);
                    f.SetValue(dataObject, text);
                }
                // draw int
                else if (f.FieldType == typeof(int))
                {
                    string stringTitle = f.Name;
                    int intValue = (int)f.GetValue(dataObject);
                    var i = EditorGUILayout.IntField(stringTitle, intValue);
                    f.SetValue(dataObject, i);
                }
                // draw float
                else if (f.FieldType == typeof(float))
                {
                    string stringTitle = f.Name;
                    float floatValue = (float)f.GetValue(dataObject);
                    var fl = EditorGUILayout.FloatField(stringTitle, floatValue);
                    f.SetValue(dataObject, fl);
                }
                // draw bool
                else if (f.FieldType == typeof(bool))
                {
                    string stringTitle = f.Name;
                    bool boolValue = (bool)f.GetValue(dataObject);
                    var b = EditorGUILayout.Toggle(stringTitle, boolValue);
                    f.SetValue(dataObject, b);
                }
                // draw enum
                else if (f.FieldType.IsEnum)
                {
                    var enumType = f.FieldType;
                    var enumList = Enum.GetNames(enumType);
                    string stringTitle = f.Name;
                    int enumValue = (int)f.GetValue(dataObject);
                    var e = EditorGUILayout.Popup(enumValue, enumList);
                    f.SetValue(dataObject, e);
                }
                // draw list
                else if (TypeUtils.IsSupportedList(f, dataObject))
                {
                    var listName = f.Name;
                    var objValue = f.GetValue(dataObject);
                    IList tempList;
                    Configurator.GetTempList(objValue, out tempList);
                    var tempListName = GetTempListName(objValue);

                    SerializedProperty stringsProperty = ScriptableTarget.FindProperty(tempListName);

                    EditorGUILayout.PropertyField(stringsProperty, new GUIContent(listName), true); // True means show children
                    ScriptableTarget.ApplyModifiedProperties(); // Remember to apply modified properties*/
                    ScriptableTarget.Update();

                    f.SetValue(dataObject, tempList);
                }
            }
            var rawData = JsonUtility.ToJson(dataObject);
            if (objectToDraw.CompressCustomData)
                rawData = Compressor.Compress(rawData);
            RawDataCache[ClassName] = rawData;
            objectToDraw.CustomRawData = rawData;
            objectToDraw.CustomDataClassName = ClassName;

            // draw info
            EditorGUILayout.HelpBox(string.Format("To create your own 'Custom Data' - create a new class, inherit from the class '{0}' and your class will automatically appear in the drop-down list. Only fields and the following data types are supported - 'int', 'float', 'string', 'enum', 'List<string>', 'List<float>', 'List<int>'", BaseClassName), MessageType.Info);

            return rawData;
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