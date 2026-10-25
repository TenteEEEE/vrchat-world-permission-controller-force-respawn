using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FRBuildCheckTest
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Yodokorochan/PermissionController/PermissionController.prefab"));
        Type builder = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TenteEEEE.PermissionControllerForceRespawn.ForceRespawnPanelBuilder", false)).First(t => t != null);
        builder.GetMethod("CreatePanel", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        Type check = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TenteEEEE.PermissionControllerForceRespawn.ForceRespawnBuildCheck", false)).First(t => t != null);
        MethodInfo find = check.GetMethod("FindProblem", BindingFlags.NonPublic | BindingFlags.Static);
        var panel = UnityEngine.Object.FindObjectsOfType<TenteEEEE.PermissionControllerForceRespawn.ForceRespawnPanel>(true)[0];
        string ok = (string)find.Invoke(null, new object[] { panel });
        var so = new SerializedObject(panel);
        so.FindProperty("emptyStateText").objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo();
        string missingRef = (string)find.Invoke(null, new object[] { panel });
        so.FindProperty("permissionController").objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo();
        string missingCtrl = (string)find.Invoke(null, new object[] { panel });
        Debug.Log("[FRB] valid=" + (ok ?? "null") + "\n[FRB] missingRef=" + missingRef + "\n[FRB] missingCtrl=" + missingCtrl);
        EditorApplication.Exit(ok == null && missingRef != null && missingRef.Contains("emptyStateText") && missingCtrl != null && missingCtrl.Contains("PermissionController") ? 0 : 1);
    }
}
