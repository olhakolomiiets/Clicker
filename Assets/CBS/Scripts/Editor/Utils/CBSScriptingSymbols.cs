using System;
using UnityEditor;

namespace CBS.Utils
{
    public static class CBSScriptingSymbols
    {
        public const string IAP_SYMBOL = "CBS_IAP";

        public static void AddCompileDefine(string newDefineCompileConstant, BuildTargetGroup[] targetGroups = null)
        {
            if (targetGroups == null)
                targetGroups = (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup));

            foreach (BuildTargetGroup grp in targetGroups)
            {
                if (grp == BuildTargetGroup.Unknown)        //the unknown group does not have any constants location
                    continue;

                string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(grp);
                if (!defines.Contains(newDefineCompileConstant))
                {
                    if (defines.Length > 0)         //if the list is empty, we don't need to append a semicolon first
                        defines += ";";

                    defines += newDefineCompileConstant;
                    try
                    {
                        PlayerSettings.SetScriptingDefineSymbolsForGroup(grp, defines);
                    }
                    catch { }
                }
            }
        }

        public static void RemoveCompileDefine(string defineCompileConstant, BuildTargetGroup[] targetGroups = null)
        {
            if (targetGroups == null)
                targetGroups = (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup));

            foreach (BuildTargetGroup grp in targetGroups)
            {
                string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(grp);
                int index = defines.IndexOf(defineCompileConstant);
                if (index < 0)
                    continue;           //this target does not contain the define
                else if (index > 0)
                    index -= 1;         //include the semicolon before the define
                                        //else we will remove the semicolon after the define

                //Remove the word and it's semicolon, or just the word (if listed last in defines)
                int lengthToRemove = Math.Min(defineCompileConstant.Length + 1, defines.Length - index);

                //remove the constant and it's associated semicolon (if necessary)
                defines = defines.Remove(index, lengthToRemove);

                try
                {
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(grp, defines);
                }
                catch { }
            }
        }

        public static bool IsEnabledDefineSymbols(string defineCompileConstant)
        {
            var target = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            return defines.Contains(defineCompileConstant);
        }
    }
}
