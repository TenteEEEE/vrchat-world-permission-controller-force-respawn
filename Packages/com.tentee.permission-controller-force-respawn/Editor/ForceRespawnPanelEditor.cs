using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TenteEEEE.PermissionControllerForceRespawn
{
    // Labels are Japanese first with English alongside, matching the upstream PermissionController Inspector.
    [CustomEditor(typeof(ForceRespawnPanel))]
    internal sealed class ForceRespawnPanelEditor : Editor
    {
        internal static readonly string[] UiReferenceFields =
        {
            "listContent", "rowTemplate", "statusText", "statusBackground", "confirmationPanel", "confirmationText",
            "confirmationDestinationText", "operatorPermissionText", "operatorPermissionBackground", "playerCountText",
            "emptyStateText", "permissionColors", "permissionTextColors"
        };

        private static bool showUiReferences;

        public override void OnInspectorGUI()
        {
            if (UdonSharpEditor.UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) { return; }

            EditorGUILayout.HelpBox(
                "■強制リスポーン パネル■\n管理者・協力者が、選んだプレイヤーをその人の権限のリスポン地点へ戻します。\n" +
                "Admins and collaborators can send a selected player back to that player's respawn point.",
                MessageType.Info);

            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("permissionController"),
                new GUIContent("権限コントローラー / PermissionController"));
            DrawControllerValidation((ForceRespawnPanel)target);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("強制リスポーン先の上書き(任意) / Destination overrides (optional)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("authDestination"), new GUIContent("管理者 / Admin"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("collabDestination"), new GUIContent("協力者 / Collaborator"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("visitorDestination"), new GUIContent("一般 / Visitor"));
            EditorGUILayout.HelpBox(
                "空欄の権限は、その人の通常のリスポン地点へ戻ります。上書きは強制リスポーンだけに効き、メニューや落下でのリスポーンは変わりません。\n" +
                "Empty = the player's normal respawn point. Overrides apply to force respawns only.",
                MessageType.None);

            EditorGUILayout.Space();
            showUiReferences = EditorGUILayout.Foldout(showUiReferences,
                "UI参照(自動設定・通常は変更不要) / UI references (auto-wired)", true);
            if (showUiReferences)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < UiReferenceFields.Length; i++)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(UiReferenceFields[i]), true);
                }
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawControllerValidation(ForceRespawnPanel panel)
        {
            VRC.Udon.UdonBehaviour controller = panel.permissionController;
            if (controller == null)
            {
                EditorGUILayout.HelpBox(
                    "PermissionController を設定してください。未設定だと権限を判定できず、ビルドもエラーになります。\n" +
                    "Assign the world's PermissionController. Without it permissions cannot be read and the build fails.",
                    MessageType.Error);
            }
            else if (!ForceRespawnPanelBuilder.IsPermissionController(controller))
            {
                EditorGUILayout.HelpBox(
                    "設定されたものは PermissionController ではありません。\n" +
                    "The assigned UdonBehaviour is not Yodokorochan.PermissionController.PermissionController.",
                    MessageType.Error);
            }

            Type playerObjectType = ForceRespawnPanelBuilder.ResolvePlayerObjectType();
            if (playerObjectType == null)
            {
                EditorGUILayout.HelpBox(
                    "PermissionController(Yodokorochan)が導入されていません。\n" +
                    "Yodokorochan PermissionController is not installed.",
                    MessageType.Error);
                return;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            if (playerObjectType.GetField("Controller", flags) == null || playerObjectType.GetField("CurrentPermission", flags) == null)
            {
                EditorGUILayout.HelpBox(
                    "導入されている PermissionController の版にこのパッケージは対応していません(Controller / CurrentPermission が見つかりません)。\n" +
                    "The installed PermissionController version is incompatible (Controller / CurrentPermission fields are missing).",
                    MessageType.Error);
            }
        }
    }
}
