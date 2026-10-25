using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

// Tier 0 simulated-remote ClientSim test for the force-respawn panel (one editor process).
[InitializeOnLoad]
public static class ForceRespawnSim
{
    const string Running = "FRSim.Running";
    const string Result = "FRSim.Result";
    const string Deadline = "FRSim.Deadline";
    const string ScenePath = "Assets/_TmpForceRespawnSim/FRSim.unity";
    static readonly Vector3 AuthPos = new Vector3(10, 0, 0);
    static readonly Vector3 CollabPos = new Vector3(0, 0, 10);
    static readonly Vector3 VisitorPos = new Vector3(-10, 0, 0);
    static readonly Vector3 FarPos = new Vector3(5, 0, -6);
    static readonly Vector3 OverridePos = new Vector3(0, 0, -10);

    static int phase;
    static double next;
    static int lastFrame = -1;
    static UdonBehaviour panel;
    static VRCPlayerApi rVisitor, rCollab, rFar;
    static readonly List<string> errors = new List<string>();
    static int passCount;

    static ForceRespawnSim()
    {
        if (!SessionState.GetBool(Running, false)) return;
        ConfigureClientSim();
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayState;
        Application.logMessageReceived += OnLog;
    }

    static void Log(string s) { Debug.Log("[FRSIM] " + s); }

    public static void RunBatch()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var descriptorGo = new GameObject("World");
        var descriptor = descriptorGo.AddComponent<VRCSceneDescriptor>();
        var spawn = new GameObject("Spawn"); spawn.transform.position = Vector3.zero;
        descriptor.spawns = new[] { spawn.transform };
        descriptor.spawnRadius = 0f;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, -.1f, 0); floor.transform.localScale = new Vector3(60, .2f, 60);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Yodokorochan/PermissionController/PermissionController.prefab");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Type ctrlType = FindType("Yodokorochan.PermissionController.PermissionController");
        var ctrl = (UdonSharpBehaviour)inst.GetComponentInChildren(ctrlType, true);
        ctrlType.GetField("AuthRespawnPoint").SetValue(ctrl, Point("AuthPoint", AuthPos));
        ctrlType.GetField("CollabRespawnPoint").SetValue(ctrl, Point("CollabPoint", CollabPos));
        ctrlType.GetField("VisitorRespawnPoint").SetValue(ctrl, Point("VisitorPoint", VisitorPos));
        UdonSharpEditorUtility.CopyProxyToUdon(ctrl);

        if (Environment.GetEnvironmentVariable("FRSIM_NOPANEL") != "1")
        FindType("TenteEEEE.PermissionControllerForceRespawn.ForceRespawnPanelBuilder")
            .GetMethod("CreatePanel", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);

        SessionState.SetBool(Running, true);
        SessionState.SetInt(Result, 1);
        SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 240);
        ConfigureClientSim();
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayState; EditorApplication.playModeStateChanged += OnPlayState;
        Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        Log("entering play mode");
        EditorApplication.EnterPlaymode();
    }

    static Transform Point(string name, Vector3 p) { var g = new GameObject(name); g.transform.position = p; return g.transform; }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (!EditorApplication.isPlaying) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            // Known baseline noise: also raised with no panel in the scene (upstream + ClientSim scene load).
            if (!msg.StartsWith("[FRSIM]") && !msg.Contains("IsNetworkingSupported cannot be changed")) errors.Add(msg.Split('\n')[0]);
    }

    static void ConfigureClientSim()
    {
        var type = FindType("VRC.SDK3.ClientSim.ClientSimSettings");
        var settings = type.GetProperty("Instance").GetValue(null);
        type.GetField("enableClientSim").SetValue(settings, true);
        type.GetField("initializationDelay").SetValue(settings, 0f);
        type.GetField("spawnPlayer").SetValue(settings, true);
        type.GetField("localPlayerIsMaster").SetValue(settings, true);
    }

    // Called from EditorApplication.update and from the runtime driver; at most once per frame.
    internal static void Tick()
    {
        if (!SessionState.GetBool(Running, false)) return;
        try
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0)) throw new TimeoutException("timeout at phase " + phase);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (EditorApplication.timeSinceStartup < next) return;
            Step();
        }
        catch (Exception e)
        {
            Debug.LogError("[FRSIM] FAIL at phase " + phase + ": " + e.Message);
            Finish(1);
        }
    }

    static void Step()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        switch (phase)
        {
            case 0:
                if (!Utilities.IsValid(local) || LocalPlayerObject() == null || !(bool)LocalPlayerObject().GetProgramVariable("IsInitialized")) return;
                panel = UnityEngine.Object.FindObjectsOfType<UdonBehaviour>(true).FirstOrDefault(b => Is(b, "ForceRespawnPanel"));
                if (panel == null) { Log("control run without panel, errors=" + errors.Count + ": " + string.Join(" | ", errors)); Finish(errors.Count == 0 ? 0 : 2); return; }
                SpawnRemote("Remote Visitor"); rVisitor = LastRemote();
                SpawnRemote("Remote Collab"); rCollab = LastRemote();
                Advance(1, 1.0);
                break;
            case 1:
                Check(PlayerObjectOf(rVisitor) != null && PlayerObjectOf(rCollab) != null, "remote players have PlayerObjects");
                // Remote senders stand at the panel; the 3 m operating range is tested separately.
                rVisitor.gameObject.transform.position = panel.transform.position + new Vector3(1, 0, -1);
                rCollab.gameObject.transform.position = panel.transform.position + new Vector3(-1, 0, -1);
                PlayerObjectOf(rVisitor).SetProgramVariable("CurrentPermission", 2);
                PlayerObjectOf(rCollab).SetProgramVariable("CurrentPermission", 1);
                LocalPlayerObject().SendCustomEvent("RequestSetAuth");
                Advance(2, 1.6);
                break;
            case 2:
            {
                int[] ids = (int[])panel.GetProgramVariable("rowPlayerIds");
                Check(ids.Length == 2 && ids.Contains(rVisitor.playerId) && ids.Contains(rCollab.playerId) && !ids.Contains(local.playerId), "rows = two remotes, self excluded (" + string.Join(",", ids) + ")");
                Check(RowLabel(rVisitor) == "一般" && RowLabel(rCollab) == "協力者", "runtime permission labels read from upstream heap (" + RowLabel(rVisitor) + "/" + RowLabel(rCollab) + ")");
                Check(PanelText("operatorPermissionText") == "あなた: 管理者", "operator chip (" + PanelText("operatorPermissionText") + ")");
                Check(PanelText("playerCountText") == "2人", "player count (" + PanelText("playerCountText") + ")");
                Check(!((Text)panel.GetProgramVariable("emptyStateText")).enabled || !((Text)panel.GetProgramVariable("emptyStateText")).gameObject.activeInHierarchy, "empty state hidden with rows");
                Check(RowButton(rVisitor).interactable && RowButton(rCollab).interactable, "operator sees enabled buttons (local perm=" + LocalPlayerObject().GetProgramVariable("CurrentPermission") + ", status=" + StatusText() + ")");
                Pass("rows and runtime permission read");
                local.TeleportTo(FarPos, Quaternion.identity);
                Advance(3, 0.3);
                break;
            }
            case 3:
                RemoteCall(rCollab, local.playerId);
                Advance(4, 0.3);
                break;
            case 4:
                CheckNear(local.GetPosition(), AuthPos, "collab caller respawns Auth target at the Auth point");
                Pass("accept from collaborator");
                Advance(5, 1.2); // past the 1 s guard
                break;
            case 5:
                local.TeleportTo(FarPos, Quaternion.identity);
                Advance(6, 0.3);
                break;
            case 6:
                RemoteCall(rVisitor, local.playerId);
                RemoteCall(rCollab, rVisitor.playerId);
                Advance(7, 0.3);
                break;
            case 7:
                CheckNear(local.GetPosition(), FarPos, "visitor caller and other-target calls are ignored");
                Pass("reject visitor caller / non-target");
                RemoteCall(rCollab, local.playerId);
                Advance(8, 0.3);
                break;
            case 8:
                CheckNear(local.GetPosition(), AuthPos, "accepted again after guard window");
                local.TeleportTo(FarPos, Quaternion.identity);
                Advance(9, 0.2);
                break;
            case 9:
                RemoteCall(rCollab, local.playerId);
                Advance(10, 0.3);
                break;
            case 10:
                CheckNear(local.GetPosition(), FarPos, "second call within 1 s is dropped");
                Pass("rate guard");
                LocalPlayerObject().SendCustomEvent("RequestSetVisitor");
                Advance(11, 1.6);
                break;
            case 11:
                Check(!RowButton(rVisitor).interactable && !RowButton(rCollab).interactable, "visitor sees disabled buttons");
                Check(!((Text)panel.GetProgramVariable("statusText")).enabled, "no banner is spent on the view-only state (" + StatusText() + ")");
                Check(PanelText("operatorPermissionText") == "あなた: 一般（閲覧のみ）", "visitor chip (" + PanelText("operatorPermissionText") + ")");
                RowButton(rVisitor).onClick.Invoke();
                Check(!ConfirmationObject().activeSelf, "visitor cannot open confirmation");
                local.TeleportTo(FarPos, Quaternion.identity);
                Advance(12, 1.2);
                break;
            case 12:
                RemoteCall(rCollab, local.playerId);
                Advance(13, 0.3);
                break;
            case 13:
                CheckNear(local.GetPosition(), VisitorPos, "visitor target respawns at the Visitor point (own permission)");
                Pass("sender-side gate and own-permission destination");
                LocalPlayerObject().SendCustomEvent("RequestSetAuth");
                Advance(32, 1.6);
                break;
            case 32:
                // Still at the Visitor point, well beyond 3 m: the click explains instead of opening the dialog.
                RowButton(rVisitor).onClick.Invoke();
                Check(!ConfirmationObject().activeSelf, "operator beyond 3 m cannot open confirmation");
                Check(StatusText() == "パネルの3m以内で操作してください", "range notice (" + StatusText() + ")");
                local.TeleportTo(NearPos(), Quaternion.identity);
                Advance(14, 0.5);
                break;
            case 14:
            {
                int before = errors.Count;
                RowButton(rVisitor).onClick.Invoke();
                Check(ConfirmationObject().activeSelf, "operator opens confirmation from the row button");
                string q = PanelText("confirmationText");
                Check(q == rVisitor.displayName, "confirmation shows the target name (" + q + ")");
                string dest = PanelText("confirmationDestinationText");
                Check(dest == "→ 一般のリスポン地点へ移動します", "confirmation shows the target's destination (" + dest + ")");
                ButtonNamed("No").onClick.Invoke();
                Check(!ConfirmationObject().activeSelf, "No closes confirmation");
                RowButton(rVisitor).onClick.Invoke();
                ButtonNamed("Yes").onClick.Invoke();
                Check(!ConfirmationObject().activeSelf, "Yes closes confirmation");
                Check(StatusText() == rVisitor.displayName + " をリスポーンさせました", "sent status (" + StatusText() + ")");
                Check(RowButtonLabel(rVisitor) == "送信済み" && !RowButton(rVisitor).interactable, "row shows sent and is disabled (" + RowButtonLabel(rVisitor) + ")");
                Check(errors.Count == before, "network event sent without errors: " + string.Join(" | ", errors.Skip(before)));
                Advance(15, 0.3);
                break;
            }
            case 15:
                CheckNear(Networking.LocalPlayer.GetPosition(), NearPos(), "operator is not moved by its own request for another player");
                Pass("UI path: row -> confirm -> network event");
                Advance(30, 1.4);
                break;
            case 30:
                Check(RowButtonLabel(rVisitor) == "リスポーン" && RowButton(rVisitor).interactable, "row button restored after 1.5 s (" + RowButtonLabel(rVisitor) + ")");
                Advance(31, 2.6);
                break;
            case 31:
                Check(!((Text)panel.GetProgramVariable("statusText")).enabled, "success band cleared after 4 s (" + StatusText() + ")");
                Pass("sent feedback and auto-clear");
                RowButton(rVisitor).onClick.Invoke();
                Check(ConfirmationObject().activeSelf, "confirmation open before departure");
                RemovePlayer(rVisitor);
                Advance(16, 1.6);
                break;
            case 16:
            {
                int[] ids = (int[])panel.GetProgramVariable("rowPlayerIds");
                Check(ids.Length == 1 && ids[0] == rCollab.playerId, "departed row removed (" + string.Join(",", ids) + ")");
                Check(!ConfirmationObject().activeSelf, "confirmation for departed player closed");
                Pass("departure");
                var overrideGo = new GameObject("FRSim auth override");
                overrideGo.transform.position = OverridePos;
                var collabGo = new GameObject("FRSim collab override");
                collabGo.transform.position = new Vector3(3, 0, 3);
                panel.SetProgramVariable("authDestination", overrideGo.transform);
                panel.SetProgramVariable("collabDestination", collabGo.transform);
                RowButton(rCollab).onClick.Invoke();
                string dest = PanelText("confirmationDestinationText");
                Check(dest == "→ 協力者用の指定地点へ移動します", "confirmation names the override (" + dest + ")");
                ButtonNamed("No").onClick.Invoke();
                Networking.LocalPlayer.TeleportTo(FarPos, Quaternion.identity);
                Advance(40, 1.2);
                break;
            }
            case 40:
                RemoteCall(rCollab, Networking.LocalPlayer.playerId);
                Advance(41, 0.3);
                break;
            case 41:
                CheckNear(Networking.LocalPlayer.GetPosition(), OverridePos, "auth target goes to the auth override point");
                Pass("per-permission destination override");
                Networking.LocalPlayer.TeleportTo(new Vector3(28, 0, 0), Quaternion.identity);
                Advance(42, 0.5);
                break;
            case 42:
                PlayerObjectOf(rCollab).SetProgramVariable("CurrentPermission", 2);
                Advance(43, 2.2);
                break;
            case 43:
                Check(RowLabel(rCollab) == "協力者", "far from the panel, polling is paused (" + RowLabel(rCollab) + ")");
                Networking.LocalPlayer.TeleportTo(FarPos, Quaternion.identity);
                Advance(44, 2.2);
                break;
            case 44:
                Check(RowLabel(rCollab) == "一般", "back near the panel, polling resumes (" + RowLabel(rCollab) + ")");
                Pass("distance-gated polling");
                // Real use: the target is far from the panel and has never cached the sender.
                Networking.LocalPlayer.TeleportTo(new Vector3(28, 0, 0), Quaternion.identity);
                Advance(45, 0.5);
                break;
            case 45:
                SpawnRemote("Remote Far Sender"); rFar = LastRemote();
                rFar.gameObject.transform.position = panel.transform.position + new Vector3(0, 0, -2);
                Advance(46, 1.2);
                break;
            case 46:
                Check(Array.IndexOf((int[])panel.GetProgramVariable("rowPlayerIds"), rFar.playerId) < 0, "far target has no cached row for the new sender");
                Check(PlayerObjectOf(rFar) != null, "new sender has a PlayerObject");
                PlayerObjectOf(rFar).SetProgramVariable("CurrentPermission", 2);
                RemoteCall(rFar, Networking.LocalPlayer.playerId);
                Advance(47, 0.3);
                break;
            case 47:
                CheckNear(Networking.LocalPlayer.GetPosition(), new Vector3(28, 0, 0), "uncached visitor sender is ignored");
                PlayerObjectOf(rFar).SetProgramVariable("CurrentPermission", 1);
                Advance(48, 1.2);
                break;
            case 48:
                RemoteCall(rFar, Networking.LocalPlayer.playerId);
                Advance(49, 0.3);
                break;
            case 49:
                CheckNear(Networking.LocalPlayer.GetPosition(), OverridePos, "uncached collaborator sender is accepted far from the panel");
                Pass("far target, uncached sender");
                // A sender beyond the operating range is refused even with permission.
                rFar.gameObject.transform.position = new Vector3(-28, 0, 0);
                Networking.LocalPlayer.TeleportTo(FarPos, Quaternion.identity);
                Advance(50, 1.2);
                break;
            case 50:
                Check(Vector3.Distance(rFar.GetPosition(), new Vector3(-28, 0, 0)) < 1f, "remote sender moved out of range (" + rFar.GetPosition() + ")");
                RemoteCall(rFar, Networking.LocalPlayer.playerId);
                Advance(51, 0.3);
                break;
            case 51:
                CheckNear(Networking.LocalPlayer.GetPosition(), FarPos, "collaborator sender beyond 3 m is refused");
                rFar.gameObject.transform.position = panel.transform.position + new Vector3(0, 0, -4);
                Advance(52, 1.2);
                break;
            case 52:
                RemoteCall(rFar, Networking.LocalPlayer.playerId);
                Advance(53, 0.3);
                break;
            case 53:
                CheckNear(Networking.LocalPlayer.GetPosition(), FarPos, "collaborator sender at 4 m is refused");
                Pass("sender range limit");
                Check(errors.Count == 0, "no errors during play: " + string.Join(" | ", errors));
                Log("PASS all " + passCount + " groups");
                Finish(0);
                break;
        }
    }

    // Emulates a network call that arrives from `caller`, the way ClientSim dispatches [NetworkCallable] events.
    static void RemoteCall(VRCPlayerApi caller, int targetPlayerId)
    {
        var meta = panel.GetNetworkCallingMetadata("ForceRespawn");
        Check(meta != null && meta.Parameters.Length == 1, "ForceRespawn is NetworkCallable with one parameter");
        string pname = meta.Parameters[0].Name;
        Type nc = FindType("VRC.SDK3.UdonNetworkCalling.NetworkCalling");
        MethodInfo with = nc.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).First(m => m.Name == "WithNetworkCallingContext" && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(Action));
        Action run = () => panel.RunEventAdvanced<object>("ForceRespawn", false, true, (pname, (object)targetPlayerId));
        with.Invoke(null, new object[] { caller, run });
    }

    static UdonBehaviour LocalPlayerObject() { return PlayerObjectOf(Networking.LocalPlayer); }
    static UdonBehaviour PlayerObjectOf(VRCPlayerApi p)
    {
        foreach (var go in Networking.GetPlayerObjects(p))
            foreach (var ub in go.GetComponentsInChildren<UdonBehaviour>(true))
                if (Is(ub, "PermissionControllerPlayerObject")) return ub;
        return null;
    }
    static GameObject RowOf(VRCPlayerApi p)
    {
        int[] ids = (int[])panel.GetProgramVariable("rowPlayerIds");
        Component[] rows = (Component[])panel.GetProgramVariable("rows");
        int i = Array.IndexOf(ids, p.playerId);
        Check(i >= 0, "row exists for " + p.displayName);
        return rows[i].gameObject;
    }
    static string RowLabel(VRCPlayerApi p) { return RowOf(p).transform.Find("Permission/Label").GetComponent<Text>().text; }
    static string RowButtonLabel(VRCPlayerApi p) { return RowOf(p).transform.Find("Respawn Button/Label").GetComponent<Text>().text; }
    static string PanelText(string field) { var t = (Text)panel.GetProgramVariable(field); return t == null ? null : t.text; }
    static Button RowButton(VRCPlayerApi p) { return RowOf(p).transform.Find("Respawn Button").GetComponent<Button>(); }
    static string StatusText() { return ((Text)panel.GetProgramVariable("statusText")).text; }
    static GameObject ConfirmationObject() { return (GameObject)panel.GetProgramVariable("confirmationPanel"); }
    static Button ButtonNamed(string n) { return panel.GetComponentsInChildren<Button>(true).First(b => b.name == n); }

    static void SpawnRemote(string name) { FindType("VRC.SDK3.ClientSim.ClientSimMain").GetMethod("SpawnRemotePlayer", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { name }); }
    static void RemovePlayer(VRCPlayerApi p) { FindType("VRC.SDK3.ClientSim.ClientSimMain").GetMethod("RemovePlayer", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { p }); }
    static VRCPlayerApi LastRemote() { return VRCPlayerApi.AllPlayers.Where(x => x != null && !x.isLocal).OrderBy(x => x.playerId).Last(); }

    static Vector3 NearPos() { return panel.transform.position + new Vector3(0, 0, -1.5f); }
    static void CheckNear(Vector3 a, Vector3 b, string msg) { Vector3 d = a - b; d.y = 0; Check(d.magnitude < 0.5f, msg + " (pos " + a + ", expected " + b + ")"); }
    static void Pass(string s) { passCount++; Log("ok: " + s); }
    static void Advance(int p, double delay) { phase = p; next = EditorApplication.timeSinceStartup + delay; }
    static void Check(bool c, string m) { if (!c) throw new InvalidOperationException(m); }
    static bool Is(UdonBehaviour b, string cls) { var pa = b.programSource as UdonSharpProgramAsset; return pa != null && pa.sourceCsScript != null && pa.sourceCsScript.GetClass() != null && pa.sourceCsScript.GetClass().Name == cls; }
    static Type FindType(string n) { return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(n, false)).First(t => t != null); }

    static void Finish(int code)
    {
        SessionState.SetInt(Result, code);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else OnPlayState(PlayModeStateChange.EnteredEditMode);
    }

    static void OnPlayState(PlayModeStateChange s)
    {
        if (s != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Running, false)) return;
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        Log("exit " + SessionState.GetInt(Result, 1));
        EditorApplication.Exit(SessionState.GetInt(Result, 1));
    }
}
