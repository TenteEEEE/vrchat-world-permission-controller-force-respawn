using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace TenteEEEE.PermissionControllerForceRespawn
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ForceRespawnRow : UdonSharpBehaviour
    {
        [Header("行テンプレート(パネル生成時に自動設定) / Row template (auto-wired)")]
        public ForceRespawnPanel panel;
        public Text displayNameText;
        public Text permissionText;
        public Image permissionBackground;
        public Button respawnButton;
        public Text respawnButtonText;
        public CanvasGroup rowCanvasGroup;
        public Color[] badgeColors;
        public Color[] badgeTextColors;

        private const string ButtonLabel = "リスポーン";
        private const string SentLabel = "送信済み";

        private int playerId = -1;
        private bool canOperate;
        private bool sentPending;

        public void Init(int id, string displayName)
        {
            playerId = id;
            displayNameText.text = displayName;
        }

        // Called once per refresh; Unity skips the rebuild when a value is unchanged.
        public void SetState(int permission, string permissionLabel, bool canUse)
        {
            int colorIndex = permission < 0 || permission > 2 ? 0 : permission + 1;
            permissionText.text = permissionLabel;
            permissionBackground.color = badgeColors[colorIndex];
            permissionText.color = badgeTextColors[colorIndex];
            canOperate = canUse;
            UpdateButtonState();
        }

        // Blocks a second tap until the receiving client's one-second guard has passed.
        public void MarkSent()
        {
            sentPending = true;
            respawnButtonText.text = SentLabel;
            UpdateButtonState();
            SendCustomEventDelayedSeconds(nameof(_RestoreButton), 1.5f);
        }

        public void _RestoreButton()
        {
            sentPending = false;
            respawnButtonText.text = ButtonLabel;
            UpdateButtonState();
        }

        public void OnRespawnClicked()
        {
            if (canOperate && !sentPending)
            {
                panel.OpenConfirmation(playerId);
            }
        }

        private void UpdateButtonState()
        {
            respawnButton.interactable = canOperate && !sentPending;
            rowCanvasGroup.alpha = canOperate ? 1f : 0.55f;
        }
    }
}
