using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace TenteEEEE.PermissionControllerForceRespawn
{
    // Network events are refused on behaviours with sync mode None, so this stays Manual with nothing synced.
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class ForceRespawnPanel : UdonSharpBehaviour
    {
        [Header("接続先の権限コントローラー / PermissionController")]
        [Tooltip("このワールドの PermissionController。パネル生成時に自動で設定されます。\nThe world's PermissionController. Assigned automatically when the panel is created.")]
        public UdonBehaviour permissionController;

        [Header("操作範囲 / Operating range")]
        [Tooltip("パネルからこの距離(m)以内にいる管理者・協力者だけが操作できます。受信側でも送信者の位置を検査します。\nOnly admins and collaborators within this distance (m) of the panel can operate it. The receiving client checks the sender's position too.")]
        public float operateDistance = 2f;

        // UI references below are wired by the panel builder; the custom Inspector folds them away.
        public Transform listContent;
        public GameObject rowTemplate;
        public Text statusText;
        public Image statusBackground;
        public GameObject confirmationPanel;
        public Text confirmationText;
        public Text confirmationDestinationText;
        public Text operatorPermissionText;
        public Image operatorPermissionBackground;
        public Text playerCountText;
        public Text emptyStateText;
        public Color[] permissionColors;
        public Color[] permissionTextColors;

        [Header("強制リスポーン先の上書き(任意) / Force respawn destination overrides (optional)")]
        [Tooltip("管理者を強制リスポーンさせたときの行き先。空欄なら通常のリスポン地点。\nWhere a force-respawned admin goes. Empty = their normal respawn point.")]
        public Transform authDestination;
        [Tooltip("協力者を強制リスポーンさせたときの行き先。空欄なら通常のリスポン地点。\nWhere a force-respawned collaborator goes. Empty = their normal respawn point.")]
        public Transform collabDestination;
        [Tooltip("一般を強制リスポーンさせたときの行き先。空欄なら通常のリスポン地点。\nWhere a force-respawned visitor goes. Empty = their normal respawn point.")]
        public Transform visitorDestination;

        [HideInInspector] public ForceRespawnRow[] rows = new ForceRespawnRow[0];
        [HideInInspector] public int[] rowPlayerIds = new int[0];

        // Nobody reads the panel from farther away, so polling stops there; joins and leaves still apply.
        private const float ActiveDistance = 20f;
        private const float PollInterval = 1f;
        private const float NoticeSeconds = 4f;

        // The upstream PlayerObject behaviour that holds each player's permission, found once and reused.
        private UdonBehaviour[] rowSources = new UdonBehaviour[0];
        private UdonBehaviour localSource;

        private int pendingPlayerId = -1;
        private float lastForcedAt = -10f;
        private float noticeExpiresAt;
        private bool refreshQueued;

        private void Start()
        {
            rowTemplate.SetActive(false);
            confirmationPanel.SetActive(false);
            ClearNotice();
            QueueRefresh();
            SendCustomEventDelayedSeconds(nameof(_Poll), PollInterval);
        }

        public void _Poll()
        {
            if (IsLocalNearby())
            {
                RefreshPlayers();
            }
            SendCustomEventDelayedSeconds(nameof(_Poll), PollInterval);
        }

        // A late joiner receives OnPlayerJoined for everyone already here; coalesce them into one refresh.
        private void QueueRefresh()
        {
            if (refreshQueued) { return; }
            refreshQueued = true;
            SendCustomEventDelayedFrames(nameof(_RunQueuedRefresh), 1);
        }

        public void _RunQueuedRefresh()
        {
            refreshQueued = false;
            if (IsLocalNearby())
            {
                RefreshPlayers();
            }
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            QueueRefresh();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player)) { return; }
            int rowIndex = FindRowIndex(player.playerId);
            if (rowIndex >= 0)
            {
                RemoveRowAt(rowIndex);
                UpdateListSummary();
            }
            if (pendingPlayerId == player.playerId)
            {
                CloseConfirmation();
                ShowNotice(player.displayName + " が退室したため確認を閉じました", new Color(0.075f, 0.31f, 0.263f, 1f));
            }
        }

        public void OpenConfirmation(int targetPlayerId)
        {
            VRCPlayerApi target = VRCPlayerApi.GetPlayerById(targetPlayerId);
            if (!Utilities.IsValid(target) || !CanOperate(GetPermission(Networking.LocalPlayer)))
            {
                return;
            }
            if (!IsWithin(Networking.LocalPlayer, operateDistance))
            {
                ShowNotice("パネルの" + operateDistance.ToString("0.#") + "m以内で操作してください", new Color(0.075f, 0.31f, 0.263f, 1f));
                return;
            }
            pendingPlayerId = targetPlayerId;
            confirmationText.text = target.displayName;
            UpdateConfirmationDestination(target);
            confirmationPanel.SetActive(true);
        }

        public void ConfirmRespawn()
        {
            int targetPlayerId = pendingPlayerId;
            CloseConfirmation();

            VRCPlayerApi target = VRCPlayerApi.GetPlayerById(targetPlayerId);
            if (!Utilities.IsValid(target) || !CanOperate(GetPermission(Networking.LocalPlayer)) || !IsWithin(Networking.LocalPlayer, operateDistance))
            {
                return;
            }

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ForceRespawn), targetPlayerId);
            int rowIndex = FindRowIndex(targetPlayerId);
            if (rowIndex >= 0)
            {
                rows[rowIndex].MarkSent();
            }
            ShowNotice(target.displayName + " をリスポーンさせました", new Color(0.106f, 0.431f, 0.31f, 1f));
        }

        public void CancelConfirmation()
        {
            CloseConfirmation();
        }

        [NetworkCallable]
        public void ForceRespawn(int targetPlayerId)
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(localPlayer) || localPlayer.playerId != targetPlayerId)
            {
                return;
            }
            // Anyone can send a network event, so the sender's permission and position are checked here, not only on the button.
            VRCPlayerApi sender = NetworkCalling.CallingPlayer;
            if (!CanOperate(GetPermission(sender)) || !IsWithin(sender, operateDistance))
            {
                return;
            }
            if (Time.time - lastForcedAt < 1f)
            {
                return;
            }
            lastForcedAt = Time.time;

            Transform destination = GetDestinationOverride(GetPermission(localPlayer));
            if (destination != null)
            {
                localPlayer.TeleportTo(destination.position, destination.rotation);
            }
            else
            {
                localPlayer.Respawn();
            }
        }

        public void _ClearNotice()
        {
            // Several delayed calls may be pending; only the one that reaches the latest expiry clears.
            if (Time.time + 0.05f >= noticeExpiresAt)
            {
                ClearNotice();
            }
        }

        private void RefreshPlayers()
        {
            VRCPlayerApi localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(localPlayer)) { return; }

            int localPermission = GetPermission(localPlayer);
            bool canOperate = CanOperate(localPermission);
            UpdateOperatorChip(localPermission, canOperate);
            if (!canOperate && pendingPlayerId >= 0)
            {
                CloseConfirmation();
            }

            for (int i = rowPlayerIds.Length - 1; i >= 0; i--)
            {
                if (!Utilities.IsValid(VRCPlayerApi.GetPlayerById(rowPlayerIds[i])))
                {
                    RemoveRowAt(i);
                }
            }

            VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(players);
            for (int i = 0; i < players.Length; i++)
            {
                VRCPlayerApi player = players[i];
                if (!Utilities.IsValid(player) || player.playerId == localPlayer.playerId)
                {
                    continue;
                }
                int rowIndex = FindRowIndex(player.playerId);
                if (rowIndex < 0)
                {
                    rowIndex = AddRow(player);
                }
                int permission = GetPermission(player);
                rows[rowIndex].SetState(permission, PermissionLabel(permission), canOperate);
            }
            UpdateListSummary();

            if (pendingPlayerId >= 0)
            {
                VRCPlayerApi target = VRCPlayerApi.GetPlayerById(pendingPlayerId);
                if (Utilities.IsValid(target))
                {
                    UpdateConfirmationDestination(target);
                }
            }
        }

        private void UpdateListSummary()
        {
            playerCountText.text = rows.Length + "人";
            emptyStateText.enabled = rows.Length == 0;
        }

        private int AddRow(VRCPlayerApi player)
        {
            GameObject rowObject = Instantiate(rowTemplate);
            rowObject.transform.SetParent(listContent, false);
            rowObject.SetActive(true);
            ForceRespawnRow row = rowObject.GetComponent<ForceRespawnRow>();
            row.Init(player.playerId, player.displayName);

            int count = rows.Length;
            ForceRespawnRow[] newRows = new ForceRespawnRow[count + 1];
            int[] newIds = new int[count + 1];
            UdonBehaviour[] newSources = new UdonBehaviour[count + 1];
            for (int i = 0; i < count; i++)
            {
                newRows[i] = rows[i];
                newIds[i] = rowPlayerIds[i];
                newSources[i] = rowSources[i];
            }
            newRows[count] = row;
            newIds[count] = player.playerId;
            rows = newRows;
            rowPlayerIds = newIds;
            rowSources = newSources;
            return count;
        }

        private void RemoveRowAt(int index)
        {
            Destroy(rows[index].gameObject);
            int count = rows.Length - 1;
            ForceRespawnRow[] newRows = new ForceRespawnRow[count];
            int[] newIds = new int[count];
            UdonBehaviour[] newSources = new UdonBehaviour[count];
            int to = 0;
            for (int i = 0; i <= count; i++)
            {
                if (i == index) { continue; }
                newRows[to] = rows[i];
                newIds[to] = rowPlayerIds[i];
                newSources[to] = rowSources[i];
                to++;
            }
            rows = newRows;
            rowPlayerIds = newIds;
            rowSources = newSources;
        }

        private int FindRowIndex(int playerId)
        {
            for (int i = 0; i < rowPlayerIds.Length; i++)
            {
                if (rowPlayerIds[i] == playerId) { return i; }
            }
            return -1;
        }

        // Returns 0 = admin, 1 = collaborator, 2 = visitor, -1 = unknown (upstream enum, stored as int in Udon).
        private int GetPermission(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player)) { return -1; }

            UdonBehaviour source;
            if (player.isLocal)
            {
                if (!Utilities.IsValid(localSource)) { localSource = FindPermissionSource(player); }
                source = localSource;
            }
            else
            {
                int rowIndex = FindRowIndex(player.playerId);
                if (rowIndex < 0) { return ReadPermission(FindPermissionSource(player)); }
                // A just-joined player's PlayerObject may not exist yet, so a miss is retried next refresh.
                if (!Utilities.IsValid(rowSources[rowIndex])) { rowSources[rowIndex] = FindPermissionSource(player); }
                source = rowSources[rowIndex];
            }
            return ReadPermission(source);
        }

        private int ReadPermission(UdonBehaviour source)
        {
            if (!Utilities.IsValid(source)) { return -1; }
            return (int)source.GetProgramVariable("CurrentPermission");
        }

        // The upstream PermissionControllerPlayerObject whose Controller is ours. GetProgramVariableType is used
        // first because GetProgramVariable logs an error for a missing symbol on unrelated behaviours.
        private UdonBehaviour FindPermissionSource(VRCPlayerApi player)
        {
            if (permissionController == null) { return null; }
            GameObject[] playerObjects = Networking.GetPlayerObjects(player);
            for (int i = 0; i < playerObjects.Length; i++)
            {
                if (playerObjects[i] == null) { continue; }
                Component[] components = playerObjects[i].GetComponents(typeof(UdonBehaviour));
                for (int j = 0; j < components.Length; j++)
                {
                    UdonBehaviour behaviour = (UdonBehaviour)components[j];
                    if (behaviour.GetProgramVariableType("Controller") == null || behaviour.GetProgramVariableType("CurrentPermission") == null)
                    {
                        continue;
                    }
                    if ((UdonBehaviour)behaviour.GetProgramVariable("Controller") == permissionController)
                    {
                        return behaviour;
                    }
                }
            }
            return null;
        }

        private bool IsLocalNearby()
        {
            return IsWithin(Networking.LocalPlayer, ActiveDistance);
        }

        private bool IsWithin(VRCPlayerApi player, float distance)
        {
            return Utilities.IsValid(player)
                && Vector3.Distance(player.GetPosition(), transform.position) <= distance;
        }

        private bool CanOperate(int permission)
        {
            return permission == 0 || permission == 1;
        }

        private string PermissionLabel(int permission)
        {
            if (permission == 0) { return "管理者"; }
            if (permission == 1) { return "協力者"; }
            if (permission == 2) { return "一般"; }
            return "不明";
        }

        // Only the force-respawn destination is overridden; normal respawns stay with the upstream points.
        private Transform GetDestinationOverride(int permission)
        {
            if (permission == 0) { return authDestination; }
            if (permission == 1) { return collabDestination; }
            if (permission == 2) { return visitorDestination; }
            return null;
        }

        // Non-operators learn they cannot act from this chip plus the dimmed rows; no banner is spent on it.
        private void UpdateOperatorChip(int permission, bool canOperate)
        {
            int colorIndex = permission < 0 || permission > 2 ? 0 : permission + 1;
            operatorPermissionText.text = "あなた: " + PermissionLabel(permission) + (canOperate ? "" : "（閲覧のみ）");
            operatorPermissionText.color = permissionTextColors[colorIndex];
            operatorPermissionBackground.color = permissionColors[colorIndex];
        }

        private void UpdateConfirmationDestination(VRCPlayerApi target)
        {
            int permission = GetPermission(target);
            if (permission < 0)
            {
                confirmationDestinationText.text = "→ その人のリスポン地点へ移動します";
            }
            else if (GetDestinationOverride(permission) != null)
            {
                confirmationDestinationText.text = "→ " + PermissionLabel(permission) + "用の指定地点へ移動します";
            }
            else
            {
                confirmationDestinationText.text = "→ " + PermissionLabel(permission) + "のリスポン地点へ移動します";
            }
        }

        private void ShowNotice(string message, Color background)
        {
            noticeExpiresAt = Time.time + NoticeSeconds;
            statusText.text = message;
            statusText.enabled = true;
            statusBackground.color = background;
            SendCustomEventDelayedSeconds(nameof(_ClearNotice), NoticeSeconds);
        }

        private void ClearNotice()
        {
            statusText.text = "";
            statusText.enabled = false;
            statusBackground.color = new Color(0f, 0f, 0f, 0f);
        }

        private void CloseConfirmation()
        {
            pendingPlayerId = -1;
            confirmationPanel.SetActive(false);
        }
    }
}
