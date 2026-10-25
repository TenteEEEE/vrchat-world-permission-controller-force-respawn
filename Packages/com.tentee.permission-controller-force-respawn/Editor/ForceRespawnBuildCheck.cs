using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TenteEEEE.PermissionControllerForceRespawn
{
    // The runtime trusts its wiring instead of null-checking every UI reference, so a broken panel stops the build here.
    internal sealed class ForceRespawnBuildCheck : IProcessSceneWithReport
    {
        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                ForceRespawnPanel[] panels = roots[i].GetComponentsInChildren<ForceRespawnPanel>(true);
                for (int j = 0; j < panels.Length; j++)
                {
                    string problem = FindProblem(panels[j]);
                    if (problem == null) { continue; }

                    string message = "[PermissionController Force Respawn] '" + panels[j].name + "': " + problem;
                    if (report != null)
                    {
                        throw new BuildFailedException(message);
                    }
                    Debug.LogError(message, panels[j]);
                }
            }
        }

        private static string FindProblem(ForceRespawnPanel panel)
        {
            if (!ForceRespawnPanelBuilder.IsPermissionController(panel.permissionController))
            {
                return "PermissionController が設定されていません。パネルの Inspector で設定してください。 / No valid PermissionController is assigned.";
            }
            SerializedObject serialized = new SerializedObject(panel);
            string[] fields = ForceRespawnPanelEditor.UiReferenceFields;
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = serialized.FindProperty(fields[i]);
                bool missing = property.isArray ? property.arraySize < 4 : property.objectReferenceValue == null;
                if (missing)
                {
                    return "UI参照 '" + fields[i] + "' が欠けています。パネルを作り直してください。 / UI reference '" + fields[i] + "' is missing; recreate the panel.";
                }
            }
            return null;
        }
    }
}
