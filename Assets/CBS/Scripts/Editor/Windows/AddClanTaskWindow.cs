#if ENABLE_PLAYFABADMIN_API
using CBS.Models;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddClanTaskWindow : AddTaskWindow<CBSClanTask>
    {
        protected override void DrawConfigs()
        {
            base.DrawConfigs();
            // draw level
            GUILayout.Space(15);
            CurrentData.Weight = EditorGUILayout.IntSlider("Weight", CurrentData.Weight, 0, 100);
            EditorGUILayout.HelpBox("The greater the 'Weight' parameter, the greater the chance of a task drop out for the clan", MessageType.Info);
        }
    }
}
#endif