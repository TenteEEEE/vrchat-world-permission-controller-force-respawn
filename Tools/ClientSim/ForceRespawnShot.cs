using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Renders the upstream PermissionController canvas next to the force-respawn panel (edit mode, no Udon).
public static class ForceRespawnShot
{
    public static void Run()
    {
        string outDir = Environment.GetEnvironmentVariable("FRSHOT_OUT");
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Yodokorochan/PermissionController/PermissionController.prefab");
        var up = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        up.transform.position = Vector3.zero;

        Type builder = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TenteEEEE.PermissionControllerForceRespawn.ForceRespawnPanelBuilder", false)).First(t => t != null);
        builder.GetMethod("CreatePanel", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        var panelRoot = GameObject.Find("PermissionController Force Respawn Panel");

        // Fill sample rows (edit mode stand-in for runtime rows).
        var template = panelRoot.GetComponentsInChildren<Transform>(true).First(t => t.name == "Row Template").gameObject;
        string[] names = { "Tentee", "もちもちうさぎ", "VeryLongDisplayName_ABCDEFGHIJ", "協力者さん" };
        string[] perms = { "管理者", "一般", "一般", "協力者" };
        for (int i = 0; i < names.Length; i++)
        {
            var row = UnityEngine.Object.Instantiate(template, template.transform.parent);
            row.SetActive(true);
            SetRowText(row, names[i], perms[i]);
            Color32[] bc = { new Color32(242, 193, 78, 255), new Color32(30, 206, 171, 255), new Color32(91, 107, 104, 255) };
            int pi = perms[i] == "管理者" ? 0 : perms[i] == "協力者" ? 1 : 2;
            row.transform.Find("Permission").GetComponent<Image>().color = bc[pi];
            row.transform.Find("Permission/Label").GetComponent<Text>().color = pi == 2 ? Color.white : (Color)new Color32(6, 44, 38, 255);
        }
        foreach (var t in panelRoot.GetComponentsInChildren<Text>(true)) { if (t.name == "Empty State") t.enabled = false; if (t.name == "Label" && t.transform.parent.name == "Operator Permission") t.text = "あなた: 管理者"; if (t.name == "Player Count") t.text = "4人"; }
        foreach (var t in panelRoot.GetComponentsInChildren<Transform>(true)) if (t.name == "Operator Permission") t.GetComponent<Image>().color = new Color32(242, 193, 78, 255);
        string mode = Environment.GetEnvironmentVariable("FRSHOT_MODE") ?? "list";
        if (mode == "confirm")
        {
            var conf = panelRoot.GetComponentsInChildren<Transform>(true).First(t => t.name == "Confirmation").gameObject;
            conf.SetActive(true);
            foreach (var t in conf.GetComponentsInChildren<Text>(true))
            {
                if (t.name == "Target Name") t.text = "もちもちうさぎ";
                if (t.name == "Destination") t.text = "→ 一般のリスポン地点へ移動します";
            }
        }
        if (mode == "toast")
        {
            foreach (var t in panelRoot.GetComponentsInChildren<Transform>(true)) if (t.name == "Status Band") t.GetComponent<Image>().color = new Color32(27, 110, 79, 255);
            foreach (var t in panelRoot.GetComponentsInChildren<Text>(true)) if (t.name == "Status") { t.text = "もちもちうさぎ をリスポーンさせました"; t.enabled = true; }
        }
        if (mode == "locked")
        {
            foreach (var t in panelRoot.GetComponentsInChildren<Transform>(true)) if (t.name == "Operator Permission") t.GetComponent<Image>().color = new Color32(91, 107, 104, 255);
            foreach (var t in panelRoot.GetComponentsInChildren<Text>(true)) if (t.name == "Label" && t.transform.parent.name == "Operator Permission") { t.text = "あなた: 一般（閲覧のみ）"; t.color = Color.white; }
            foreach (var cg in panelRoot.GetComponentsInChildren<CanvasGroup>(true)) cg.alpha = 0.55f;
        }
        Canvas.ForceUpdateCanvases();
        foreach (var lg in panelRoot.GetComponentsInChildren<LayoutGroup>(true)) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)lg.transform);

        var cam = Camera.main;
        Debug.Log("[FRSHOT] panel pos=" + panelRoot.transform.position + " rot=" + panelRoot.transform.eulerAngles + " scale=" + panelRoot.transform.localScale + " size=" + ((RectTransform)panelRoot.transform).sizeDelta);
        float mid = (0f + panelRoot.transform.position.x) / 2f;
        cam.transform.position = new Vector3(mid, 0, -2.6f);
        cam.transform.rotation = Quaternion.identity;
        cam.fieldOfView = 40;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.35f, 0.35f, 0.38f);
        var rt = new RenderTexture(2400, 1200, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(outDir, "shot_" + mode + ".png"), tex.EncodeToPNG());
        Debug.Log("[FRSHOT] wrote " + mode);
        EditorApplication.Exit(0);
    }

    static void SetRowText(GameObject row, string name, string perm)
    {
        foreach (var t in row.GetComponentsInChildren<Text>(true))
        {
            if (t.name == "Display Name") t.text = name;
            if (t.name == "Label" && t.transform.parent.name == "Permission") t.text = perm;
        }
    }
}
