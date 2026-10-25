#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Batch-mode Unity may not pump EditorApplication.update in Play Mode; this ticks the test from Update.
public sealed class ForceRespawnSimDriver : MonoBehaviour
{
    private Action tick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!SessionState.GetBool("FRSim.Running", false)) return;
        var go = new GameObject("FRSim driver");
        DontDestroyOnLoad(go);
        go.AddComponent<ForceRespawnSimDriver>();
    }

    private void Awake()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ForceRespawnSim", false)).First(t => t != null);
        tick = (Action)Delegate.CreateDelegate(typeof(Action), type.GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Static));
    }

    private void Update() { tick(); }
}
#endif
