using CBS.Scriptable;
using Ionic.Zip;
using System.IO;
using UnityEngine;

namespace CBS.Editor
{
    public static class ZipUtils
    {
        private static string AzureProjectPath = "/../CBS_Azure_Functions(Admin)";
        private static string ArchiveAzureProjectPath = "/CBS/AzureFunctionsProject/AzureFunctionsProject.zip";
        private static string UnzipAzureProjectPath = "/../CBSAzureFunctionsProject";

        public static void ZipAzureProject()
        {
            var buildsPath = Application.dataPath + "/" + Path.Combine(AzureProjectPath, "bin");
            if (Directory.Exists(buildsPath))
            {
                Directory.Delete(buildsPath, true);
            }
            var objPath = Application.dataPath + "/" + Path.Combine(AzureProjectPath, "obj");
  
            if (Directory.Exists(objPath))
            {
                Directory.Delete(objPath, true);
            }
            var fullPath = Application.dataPath + AzureProjectPath;

            using (ZipFile zip = new ZipFile())
            {
                var archivePath = Application.dataPath + ArchiveAzureProjectPath;

                zip.AddDirectory(fullPath);
                zip.Save(archivePath);
            }
        }

        public static void UnzipAzureProject()
        {
            var configeData = CBSScriptable.Get<PlayFabConfigData>();
            var fullPath = Application.dataPath + ArchiveAzureProjectPath;
            var unzipPath = Application.dataPath + configeData.GetAzureProjectPath();

            using (ZipFile zip = ZipFile.Read(fullPath))
            {
                foreach (ZipEntry e in zip)
                {
                    e.Extract(unzipPath, ExtractExistingFileAction.OverwriteSilently);  // overwrite == true
                }
            }
        }
    }
}
