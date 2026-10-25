using System;
using System.Reflection;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

namespace TenteEEEE.PermissionControllerForceRespawn
{
    internal static class ForceRespawnPanelBuilder
    {
        private const string PermissionControllerTypeName = "Yodokorochan.PermissionController.PermissionController";
        private const string PlayerObjectTypeName = "Yodokorochan.PermissionController.PermissionControllerPlayerObject";

        [MenuItem("GameObject/TenteEEEE/PermissionController Force Respawn Panel", false, 10)]
        private static void CreateFromGameObjectMenu()
        {
            CreatePanel();
        }

        [MenuItem("Tools/TenteEEEE/PermissionController Force Respawn/Create Panel")]
        private static void CreateFromToolsMenu()
        {
            CreatePanel();
        }

        internal static Type ResolvePermissionControllerType()
        {
            return ResolveType(PermissionControllerTypeName);
        }

        internal static Type ResolvePlayerObjectType()
        {
            return ResolveType(PlayerObjectTypeName);
        }

        internal static bool IsPermissionController(UdonBehaviour behaviour)
        {
            if (behaviour == null) { return false; }
            UdonSharpBehaviour proxy = UdonSharpEditorUtility.GetProxyBehaviour(behaviour);
            return proxy != null && proxy.GetType().FullName == PermissionControllerTypeName;
        }

        private static void CreatePanel()
        {
            UdonBehaviour controller = FindPermissionController();
            Transform selected = Selection.activeTransform;
            Vector3 panelPosition;
            Quaternion panelRotation;
            ResolvePlacement(selected, controller, out panelPosition, out panelRotation);

            GameObject root = new GameObject(
                "PermissionController Force Respawn Panel",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(VRCUiShape));
            root.transform.position = panelPosition;
            root.transform.rotation = panelRotation;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(1100f, 900f);
            root.transform.localScale = Vector3.one * 0.001f;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.pixelPerfect = true;

            // Same look as the upstream canvas: accent frame, mid-teal surface, dark rows.
            Image frame = root.AddComponent<Image>();
            frame.color = Hex(30, 206, 171);
            GameObject surfaceObject = CreateImage(root.transform, "Surface", Hex(16, 79, 67), false);
            SetAnchoredRect(surfaceObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Color[] badgeColors = new Color[]
            {
                Hex(42, 58, 55),
                Hex(242, 193, 78),
                Hex(30, 206, 171),
                Hex(91, 107, 104)
            };
            Color[] badgeTextColors = new Color[]
            {
                new Color(1f, 1f, 1f, 0.7f),
                Hex(6, 44, 38),
                Hex(6, 44, 38),
                Color.white
            };

            Text title = CreateText(root.transform, "Title", "強制リスポーン", font, 54, TextAnchor.MiddleLeft, Color.white);
            title.fontStyle = FontStyle.Bold;
            SetAnchoredRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -100f), new Vector2(-390f, -10f));

            GameObject operatorChip = CreateImage(root.transform, "Operator Permission", badgeColors[0], false);
            RectTransform operatorChipRect = operatorChip.GetComponent<RectTransform>();
            SetAnchoredRect(operatorChipRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-362f, -86f), new Vector2(-32f, -26f));
            Text operatorPermissionText = CreateText(operatorChip.transform, "Label", "あなた: 不明", font, 34, TextAnchor.MiddleCenter, badgeTextColors[0]);
            operatorPermissionText.fontStyle = FontStyle.Bold;
            operatorPermissionText.resizeTextForBestFit = true;
            operatorPermissionText.resizeTextMinSize = 22;
            operatorPermissionText.resizeTextMaxSize = 34;
            SetAnchoredRect(operatorPermissionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));

            Text description = CreateText(root.transform, "Description", "選んだプレイヤーを、その人の権限のリスポン地点へ戻します", font, 30, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.7f));
            SetAnchoredRect(description.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -166f), new Vector2(-32f, -110f));

            // Transient notices float over the bottom of the list instead of reserving a row of the layout.
            GameObject statusBand = CreateImage(root.transform, "Status Band", new Color(0f, 0f, 0f, 0f), false);
            SetAnchoredRect(statusBand.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(44f, 44f), new Vector2(-72f, 116f));
            Text status = CreateText(statusBand.transform, "Status", "", font, 32, TextAnchor.MiddleLeft, Color.white);
            SetAnchoredRect(status.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
            status.enabled = false;

            Text listTitle = CreateText(root.transform, "Player List Title", "プレイヤー", font, 30, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.7f));
            SetAnchoredRect(listTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -224f), new Vector2(-220f, -176f));
            Text playerCount = CreateText(root.transform, "Player Count", "0人", font, 30, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.7f));
            SetAnchoredRect(playerCount.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-190f, -224f), new Vector2(-32f, -176f));
            GameObject listRule = CreateImage(root.transform, "Player List Rule", Hex(30, 206, 171), false);
            SetAnchoredRect(listRule.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -228f), new Vector2(-32f, -226f));

            GameObject scrollObject = new GameObject("Player List", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollObject.transform.SetParent(root.transform, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            SetAnchoredRect(scrollRect, Vector2.zero, Vector2.one, new Vector2(32f, 32f), new Vector2(-32f, -236f));
            Image scrollImage = scrollObject.GetComponent<Image>();
            scrollImage.color = Hex(12, 63, 54);

            // RectMask2D clips without a graphic; a Mask over an alpha-0 Image hides every row.
            GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            SetAnchoredRect(viewportRect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-28f, 0f));

            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            GameObject scrollbarObject = new GameObject("Vertical Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarObject.transform.SetParent(scrollObject.transform, false);
            RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
            SetAnchoredRect(scrollbarRect, new Vector2(1f, 0f), Vector2.one, new Vector2(-28f, 0f), Vector2.zero);
            Image scrollbarImage = scrollbarObject.GetComponent<Image>();
            scrollbarImage.color = Hex(6, 44, 38);
            Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.numberOfSteps = 0;

            GameObject handleObject = CreateImage(scrollbarObject.transform, "Handle", Hex(30, 206, 171), false);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            SetAnchoredRect(handleRect, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            scrollbar.targetGraphic = handleObject.GetComponent<Image>();
            scrollbar.handleRect = handleRect;
            scrollbar.size = 0.5f;
            scrollbar.value = 1f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.verticalScrollbarSpacing = 0f;

            Text emptyState = CreateText(viewportObject.transform, "Empty State", "ほかのプレイヤーはいません", font, 34, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
            SetAnchoredRect(emptyState.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-28f, 0f));
            emptyState.raycastTarget = false;
            emptyState.enabled = true;

            statusBand.transform.SetSiblingIndex(scrollObject.transform.GetSiblingIndex() + 1);

            GameObject template = CreateRowTemplate(contentRect, font, badgeColors, badgeTextColors);

            GameObject confirmationObject = new GameObject("Confirmation", typeof(RectTransform), typeof(Image));
            confirmationObject.transform.SetParent(root.transform, false);
            SetAnchoredRect(confirmationObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image confirmationImage = confirmationObject.GetComponent<Image>();
            confirmationImage.color = new Color(0f, 0f, 0f, 0.75f);
            confirmationImage.raycastTarget = true;

            GameObject borderObject = CreateImage(confirmationObject.transform, "Dialog Border", Hex(30, 206, 171), true);
            RectTransform borderRect = borderObject.GetComponent<RectTransform>();
            SetAnchoredRect(borderRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-410f, -235f), new Vector2(410f, 235f));

            GameObject dialogObject = CreateImage(borderObject.transform, "Dialog", Hex(16, 79, 67), true);
            RectTransform dialogRect = dialogObject.GetComponent<RectTransform>();
            SetAnchoredRect(dialogRect, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            Text dialogTitle = CreateText(dialogObject.transform, "Dialog Title", "リスポーンの確認", font, 34, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f));
            SetAnchoredRect(dialogTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -78f), new Vector2(-30f, -24f));

            Text confirmationText = CreateText(dialogObject.transform, "Target Name", "", font, 52, TextAnchor.MiddleCenter, Color.white);
            confirmationText.fontStyle = FontStyle.Bold;
            confirmationText.resizeTextForBestFit = true;
            confirmationText.resizeTextMinSize = 28;
            confirmationText.resizeTextMaxSize = 52;
            confirmationText.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetAnchoredRect(confirmationText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -148f), new Vector2(-32f, -78f));

            Text destinationText = CreateText(dialogObject.transform, "Destination", "", font, 34, TextAnchor.MiddleCenter, Color.white);
            SetAnchoredRect(destinationText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -220f), new Vector2(-24f, -150f));

            Text privacyNote = CreateText(dialogObject.transform, "Privacy Note", "相手には確認が表示されません", font, 28, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
            SetAnchoredRect(privacyNote.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -278f), new Vector2(-24f, -220f));

            Button noButton = CreateButton(dialogObject.transform, "No", "キャンセル", font, 36, Hex(42, 58, 55), Color.white, false);
            SetAnchoredRect(noButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-370f, 28f), new Vector2(-30f, 138f));

            Button yesButton = CreateButton(dialogObject.transform, "Yes", "リスポーンさせる", font, 36, Hex(30, 206, 171), Hex(6, 44, 38), true);
            SetAnchoredRect(yesButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(30f, 28f), new Vector2(370f, 138f));

            ForceRespawnPanel panel = UdonSharpUndo.AddComponent<ForceRespawnPanel>(root);
            panel.listContent = contentRect;
            panel.rowTemplate = template;
            panel.statusText = status;
            panel.statusBackground = statusBand.GetComponent<Image>();
            panel.confirmationPanel = confirmationObject;
            panel.confirmationText = confirmationText;
            panel.confirmationDestinationText = destinationText;
            panel.operatorPermissionText = operatorPermissionText;
            panel.operatorPermissionBackground = operatorChip.GetComponent<Image>();
            panel.playerCountText = playerCount;
            panel.emptyStateText = emptyState;
            panel.permissionColors = badgeColors;
            panel.permissionTextColors = badgeTextColors;
            panel.permissionController = controller;
            UdonSharpEditorUtility.CopyProxyToUdon(panel);

            ForceRespawnRow rowProxy = UdonSharpUndo.AddComponent<ForceRespawnRow>(template);
            rowProxy.panel = panel;
            rowProxy.displayNameText = template.transform.Find("Display Name").GetComponent<Text>();
            rowProxy.permissionText = template.transform.Find("Permission/Label").GetComponent<Text>();
            rowProxy.permissionBackground = template.transform.Find("Permission").GetComponent<Image>();
            rowProxy.respawnButton = template.transform.Find("Respawn Button").GetComponent<Button>();
            rowProxy.respawnButtonText = template.transform.Find("Respawn Button/Label").GetComponent<Text>();
            rowProxy.rowCanvasGroup = template.GetComponent<CanvasGroup>();
            rowProxy.badgeColors = badgeColors;
            rowProxy.badgeTextColors = badgeTextColors;

            UdonSharpEditorUtility.CopyProxyToUdon(rowProxy);
            UdonBehaviour rowBehaviour = template.GetComponent<UdonBehaviour>();
            UnityEventTools.AddStringPersistentListener(rowProxy.respawnButton.onClick, rowBehaviour.SendCustomEvent, "OnRespawnClicked");
            template.SetActive(false);

            UdonBehaviour panelBehaviour = root.GetComponent<UdonBehaviour>();
            UnityEventTools.AddStringPersistentListener(yesButton.onClick, panelBehaviour.SendCustomEvent, "ConfirmRespawn");
            UnityEventTools.AddStringPersistentListener(noButton.onClick, panelBehaviour.SendCustomEvent, "CancelConfirmation");
            confirmationObject.SetActive(false);

            Undo.RegisterCreatedObjectUndo(root, "Create PermissionController Force Respawn Panel");
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private static GameObject CreateRowTemplate(Transform content, Font font, Color[] badgeColors, Color[] badgeTextColors)
        {
            GameObject row = new GameObject("Row Template", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(CanvasGroup));
            row.transform.SetParent(content, false);
            Image rowImage = row.GetComponent<Image>();
            rowImage.color = Hex(6, 44, 38);
            rowImage.raycastTarget = false;
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = 100f;
            rowLayout.minHeight = 100f;

            HorizontalLayoutGroup horizontal = row.GetComponent<HorizontalLayoutGroup>();
            horizontal.padding = new RectOffset(16, 16, 12, 12);
            horizontal.spacing = 18f;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;
            horizontal.childAlignment = TextAnchor.MiddleLeft;

            // A GameObject can hold only one Graphic, so the badge is an Image with the label as a child.
            GameObject permissionObject = new GameObject("Permission", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            permissionObject.transform.SetParent(row.transform, false);
            Text permission = CreateText(permissionObject.transform, "Label", "不明", font, 30, TextAnchor.MiddleCenter, badgeTextColors[0]);
            permission.fontStyle = FontStyle.Bold;
            SetAnchoredRect(permission.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image permissionImage = permissionObject.GetComponent<Image>();
            permissionImage.color = badgeColors[0];
            permissionImage.raycastTarget = false;
            LayoutElement permissionLayout = permissionObject.GetComponent<LayoutElement>();
            permissionLayout.preferredWidth = 150f;
            permissionLayout.minWidth = 150f;
            permissionLayout.preferredHeight = 60f;
            permissionLayout.flexibleWidth = 0f;

            Text displayName = CreateText(row.transform, "Display Name", "Player", font, 40, TextAnchor.MiddleLeft, Color.white);
            displayName.horizontalOverflow = HorizontalWrapMode.Wrap;
            displayName.verticalOverflow = VerticalWrapMode.Truncate;
            displayName.resizeTextForBestFit = true;
            displayName.resizeTextMinSize = 24;
            displayName.resizeTextMaxSize = 40;
            LayoutElement nameLayout = displayName.gameObject.AddComponent<LayoutElement>();
            nameLayout.minWidth = 100f;
            nameLayout.flexibleWidth = 1f;
            nameLayout.preferredHeight = 76f;

            Button respawnButton = CreateButton(row.transform, "Respawn Button", "リスポーン", font, 34, Hex(30, 206, 171), Hex(6, 44, 38), true);
            LayoutElement buttonLayout = respawnButton.gameObject.AddComponent<LayoutElement>();
            buttonLayout.preferredWidth = 240f;
            buttonLayout.minWidth = 240f;
            buttonLayout.preferredHeight = 76f;
            buttonLayout.flexibleWidth = 0f;
            return row;
        }

        private static Text CreateText(Transform parent, string name, string value, Font font, int fontSize, TextAnchor alignment, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Font font, int fontSize, Color background, Color textColor, bool bold)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.GetComponent<Image>();
            image.color = background;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            Text text = CreateText(buttonObject.transform, "Label", label, font, fontSize, TextAnchor.MiddleCenter, textColor);
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            SetAnchoredRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 2f), new Vector2(-8f, -2f));
            return button;
        }

        private static GameObject CreateImage(Transform parent, string name, Color color, bool raycastTarget)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return imageObject;
        }

        private static Color Hex(byte red, byte green, byte blue)
        {
            return new Color32(red, green, blue, 255);
        }

        private static void SetAnchoredRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void ResolvePlacement(Transform selected, UdonBehaviour controller, out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            // Selecting the PermissionController itself (e.g. right-click in the Hierarchy) still means "place beside it".
            if (selected != null && (controller == null || !selected.IsChildOf(controller.transform)))
            {
                position = selected.position;
                return;
            }
            if (controller == null)
            {
                position = Vector3.zero;
                return;
            }

            // Sit to the right of every upstream canvas (the keypad canvas is right of MainCanvas), 0.1 m apart.
            Canvas[] canvases = controller.GetComponentsInChildren<Canvas>(true);
            Canvas main = null;
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == "MainCanvas") { main = canvases[i]; break; }
            }
            if (main == null)
            {
                position = controller.transform.position;
                return;
            }
            Vector3 right = main.transform.right;
            float maxRight = float.MinValue;
            Vector3[] corners = new Vector3[4];
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] == null || !canvases[i].isRootCanvas) { continue; }
                ((RectTransform)canvases[i].transform).GetWorldCorners(corners);
                for (int c = 0; c < 4; c++)
                {
                    maxRight = Mathf.Max(maxRight, Vector3.Dot(corners[c] - main.transform.position, right));
                }
            }
            const float panelHalfWidth = 0.55f;
            position = main.transform.position + right * (maxRight + 0.1f + panelHalfWidth);
            rotation = main.transform.rotation;
        }

        private static UdonBehaviour FindPermissionController()
        {
            Type controllerType = ResolvePermissionControllerType();
            if (controllerType == null)
            {
                Debug.LogWarning("[PermissionController Force Respawn] PermissionController が見つかりません。導入後にパネルの Inspector で設定してください。 / PermissionController is not installed. Assign it on the panel Inspector.");
                return null;
            }

            UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(controllerType, FindObjectsSortMode.None);
            if (found.Length == 0)
            {
                Debug.LogWarning("[PermissionController Force Respawn] シーンに PermissionController がありません。設置後にパネルの Inspector で設定してください。 / No PermissionController in the open scene. Assign it on the panel Inspector.");
                return null;
            }
            if (found.Length > 1)
            {
                Debug.LogWarning("[PermissionController Force Respawn] PermissionController が複数あります。最初の1つを設定しました。 / Multiple PermissionControllers found; the first one was assigned.");
            }
            return UdonSharpEditorUtility.GetBackingUdonBehaviour((UdonSharpBehaviour)found[0]);
        }

        private static Type ResolveType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }
    }
}
