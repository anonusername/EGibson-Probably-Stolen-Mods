using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FiftyAppraisal
{
    /// <summary>
    /// The vanilla ±1/±5/±10/±100 buttons share one row with the value field under
    /// InspectionUIManager.appraisalPanel, each with a persistent onClick call to
    /// ChangeAppraisalServicePrice(int). Their int argument is not readable through the public API,
    /// so the buttons are found by their label text.
    /// </summary>
    [HarmonyPatch(typeof(InspectionUIManager), nameof(InspectionUIManager.OnInspectItem))]
    public static class InspectItemPatch
    {
        // The manager the buttons were added to. A new scene load creates a new manager.
        private static InspectionUIManager injectedFor;

        static void Postfix()
        {
            InspectionUIManager manager = InspectionUIManager.Instance;
            if (manager == null || manager == injectedFor) return;
            // The panel is only shown for appraisal items. While it is hidden its layout sizes can't be measured,
            // so wait for the first appraisal item.
            if (manager.appraisalPanel == null || !manager.appraisalPanel.activeInHierarchy) return;
            injectedFor = manager; // set first so a failure is logged once, not on every inspection
            try
            {
                AppraisalButtons.Inject(manager);
            }
            catch (Exception e)
            {
                ModEntry.Instance.Log.Error($"Could not add the ±50 buttons: {e}");
            }
        }
    }

    /// <summary>
    /// Splits the single vanilla row into three: plus buttons, a larger value field, minus buttons.
    /// Matching amounts share a column. The new rows are clones of the vanilla row, so they keep whatever
    /// layout component the game uses (it has its own, e.g. AdvancedHorizontalLayoutGroup).
    /// </summary>
    public static class AppraisalButtons
    {
        private const string CLONE_PREFIX = "FiftyAppraisal_";
        private const string PRICE_METHOD = nameof(InspectionUIManager.ChangeAppraisalServicePrice);
        private static readonly string[] PLUS_ROW = { "+1", "+5", "+10", "+50", "+100" };
        private static readonly string[] MINUS_ROW = { "-1", "-5", "-10", "-50", "-100" };
        private const float FIELD_HEIGHT_SCALE = 1.3f; // relative to a button
        private const float FIELD_FONT_SCALE = 1.5f;

        public static void Inject(InspectionUIManager manager)
        {
            Transform panel = manager.appraisalPanel.transform;
            var buttons = new Dictionary<string, Button>();

            foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            {
                if (button.name.StartsWith(CLONE_PREFIX)) return; // already added
                if (CallsPriceMethod(button)) buttons[Label(button)] = button;
            }

            if (!buttons.TryGetValue("+10", out Button plus10) || !buttons.TryGetValue("-10", out Button minus10))
            {
                ModEntry.Instance.Log.Warning("±10 buttons not found by label; use F9 > Dump Appraisal Panel.\n" + DumpPanel());
                return;
            }

            RectTransform row = (RectTransform)plus10.transform.parent;
            if (CanSplit(row, manager.apprisalServiceInputField, buttons))
            {
                SplitRows(row, manager.apprisalServiceInputField, buttons);
                return;
            }

            // Unexpected layout: keep the single row and put ±50 between ±10 and ±100.
            // The minus side runs -100 -10 -5 -1, so -50 goes before -10.
            ModEntry.Instance.Log.Warning("Unexpected appraisal panel layout; adding ±50 to the existing row.\n" + DumpPanel());
            AddButton(plus10, plus10.transform.parent, 50, "+50").transform.SetSiblingIndex(plus10.transform.GetSiblingIndex() + 1);
            AddButton(minus10, minus10.transform.parent, -50, "-50").transform.SetSiblingIndex(minus10.transform.GetSiblingIndex());
        }

        // The split needs every vanilla button and the field in one row that a layout group arranges.
        private static bool CanSplit(RectTransform row, TMP_InputField field, Dictionary<string, Button> buttons)
        {
            LayoutGroup layout = row.GetComponent<LayoutGroup>();
            if (layout == null || !layout.enabled || field == null || field.transform.parent != row) return false;
            foreach (string label in new[] { "+100", "+10", "+5", "+1", "-100", "-10", "-5", "-1" })
            {
                if (!buttons.TryGetValue(label, out Button button) || button.transform.parent != row) return false;
            }
            return true;
        }

        private static void SplitRows(RectTransform row, TMP_InputField field, Dictionary<string, Button> buttons)
        {
            // The panel was just shown, so its layout is still dirty. Lay it out before measuring.
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
            float gap = Mathf.Max(0f, Left(buttons["-1"]) - Right(buttons["-5"]));
            float buttonHeight = Rect(buttons["+10"]).rect.height;
            float contentWidth = Mathf.Max(RowWidth(PLUS_ROW, "+10", buttons, gap), RowWidth(MINUS_ROW, "-10", buttons, gap));
            RectOffset padding = row.GetComponent<LayoutGroup>().padding;

            // Clone the rows before adding ±50, so the clones start empty.
            RectTransform plusRow = CloneEmptyRow(row, "PlusRow");
            RectTransform minusRow = CloneEmptyRow(row, "MinusRow");
            plusRow.SetSiblingIndex(row.GetSiblingIndex());
            minusRow.SetSiblingIndex(row.GetSiblingIndex() + 1);

            buttons["+50"] = AddButton(buttons["+10"], plusRow, 50, "+50");
            buttons["-50"] = AddButton(buttons["-10"], minusRow, -50, "-50");
            Fill(plusRow, PLUS_ROW, buttons);
            Fill(minusRow, MINUS_ROW, buttons);

            // The field spans a full button row, so the three rows line up.
            float fieldHeight = Mathf.Round(buttonHeight * FIELD_HEIGHT_SCALE);
            SetSize(Rect(field), contentWidth, fieldHeight);
            TMP_Text text = field.textComponent;
            if (text != null)
            {
                if (text.enableAutoSizing) text.fontSizeMax *= FIELD_FONT_SCALE;
                field.pointSize = text.fontSize * FIELD_FONT_SCALE; // also sets the placeholder
            }

            float rowWidth = contentWidth + padding.horizontal;
            SetSize(plusRow, rowWidth, row.rect.height);
            SetSize(minusRow, rowWidth, row.rect.height);
            SetSize(row, rowWidth, Mathf.Max(row.rect.height, fieldHeight + padding.vertical));

            // A layout group on the parent stacks the rows by sibling order. Otherwise place them by hand.
            LayoutGroup parentLayout = row.parent.GetComponent<LayoutGroup>();
            if (parentLayout == null || !parentLayout.enabled)
            {
                Vector2 middle = Center(row);
                MoveCenter(plusRow, middle + new Vector2(0f, row.rect.height / 2f + gap + plusRow.rect.height / 2f));
                MoveCenter(minusRow, middle - new Vector2(0f, row.rect.height / 2f + gap + minusRow.rect.height / 2f));
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)row.parent);
        }

        // Width of a row's buttons plus the gaps between them. A ±50 clone is as wide as its ±10 template.
        private static float RowWidth(string[] labels, string template, Dictionary<string, Button> buttons, float gap)
        {
            float width = gap * (labels.Length - 1);
            foreach (string label in labels)
                width += Rect(buttons[buttons.ContainsKey(label) ? label : template]).rect.width;
            return width;
        }

        private static RectTransform CloneEmptyRow(RectTransform row, string name)
        {
            RectTransform clone = UnityEngine.Object.Instantiate(row, row.parent);
            clone.name = CLONE_PREFIX + name;
            // DestroyImmediate, so the layout rebuild doesn't still see the copied children.
            for (int i = clone.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(clone.GetChild(i).gameObject);
            return clone;
        }

        private static void Fill(RectTransform row, string[] labels, Dictionary<string, Button> buttons)
        {
            foreach (string label in labels)
            {
                buttons[label].transform.SetParent(row, false);
                buttons[label].transform.SetAsLastSibling();
            }
        }

        // Clones a ±10 button into parent. The clone calls ChangeAppraisalServicePrice(amount).
        private static Button AddButton(Button template, Transform parent, int amount, string label)
        {
            Button clone = UnityEngine.Object.Instantiate(template, parent);
            clone.name = CLONE_PREFIX + label;

            // RemoveAllListeners does not touch persistent (scene-wired) calls, so switch the cloned ones off.
            for (int i = 0; i < clone.onClick.GetPersistentEventCount(); i++)
                clone.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            clone.onClick.AddListener(() => InspectionUIManager.Instance.ChangeAppraisalServicePrice(amount));

            TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
            return clone;
        }

        // Resizes around the current center. Sets both sizeDelta and a LayoutElement, because a parent layout
        // group sizes children from LayoutElement and ignores sizeDelta. A size fitter would undo it, so it's off.
        private static void SetSize(RectTransform rect, float width, float height)
        {
            Vector2 center = Center(rect);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            MoveCenter(rect, center);

            LayoutElement element = rect.GetComponent<LayoutElement>();
            if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
            element.minWidth = element.preferredWidth = width;
            element.minHeight = element.preferredHeight = height;

            ContentSizeFitter fitter = rect.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
        }

        private static RectTransform Rect(Component component) => (RectTransform)component.transform;

        // Edges and center in the parent's space, independent of pivot and anchors.
        private static float Left(Component component) => Rect(component).localPosition.x + Rect(component).rect.xMin;
        private static float Right(Component component) => Rect(component).localPosition.x + Rect(component).rect.xMax;
        private static Vector2 Center(RectTransform rect) => (Vector2)rect.localPosition + rect.rect.center;
        private static void MoveCenter(RectTransform rect, Vector2 center) => rect.anchoredPosition += center - Center(rect);

        private static bool CallsPriceMethod(Button button)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) is InspectionUIManager
                    && button.onClick.GetPersistentMethodName(i) == PRICE_METHOD)
                    return true;
            }
            return false;
        }

        private static string Label(Button button)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null) return "";
            // Normalize "− 10" / "- 10" to "-10".
            return text.text.Replace('−', '-').Replace(" ", "").Trim();
        }

        /// <summary>The appraisal panel's hierarchy, sizes, layout settings and button wiring.</summary>
        public static string DumpPanel()
        {
            InspectionUIManager manager = InspectionUIManager.Instance;
            if (manager == null || manager.appraisalPanel == null)
                return "No InspectionUIManager in this scene (load a run first).";

            Canvas canvas = manager.appraisalPanel.GetComponentInParent<Canvas>();
            StringBuilder sb = new StringBuilder("Appraisal panel");
            if (canvas != null) sb.Append($" (canvas scaleFactor={canvas.rootCanvas.scaleFactor})");
            sb.Append(":\n");
            Dump(manager.appraisalPanel.transform, 0, sb);
            return sb.ToString();
        }

        private static void Dump(Transform node, int depth, StringBuilder sb)
        {
            sb.Append(' ', depth * 2).Append(node.name).Append(node.gameObject.activeSelf ? "" : " [inactive]");
            if (node is RectTransform rect)
                sb.Append($" pos={rect.anchoredPosition} size={rect.rect.size} pivot={rect.pivot} anchors={rect.anchorMin}-{rect.anchorMax}");

            LayoutGroup layout = node.GetComponent<LayoutGroup>();
            if (layout != null) sb.Append(" layout=").Append(Describe(layout));

            LayoutElement element = node.GetComponent<LayoutElement>();
            if (element != null)
                sb.Append($" element(min={element.minWidth}x{element.minHeight} pref={element.preferredWidth}x{element.preferredHeight} flex={element.flexibleWidth}x{element.flexibleHeight})");

            ContentSizeFitter fitter = node.GetComponent<ContentSizeFitter>();
            if (fitter != null) sb.Append($" fitter({fitter} h={fitter.horizontalFit} v={fitter.verticalFit} enabled={fitter.enabled})");

            Image image = node.GetComponent<Image>();
            if (image != null)
            {
                sb.Append(" image=").Append(image.type);
                if (image.sprite != null) sb.Append(" border=").Append(image.sprite.border);
            }

            TMP_Text text = node.GetComponent<TMP_Text>();
            if (text != null)
                sb.Append($" text=\"{text.text}\" font={text.fontSize} auto={text.enableAutoSizing} wrap={text.enableWordWrapping}");

            Button button = node.GetComponent<Button>();
            if (button != null)
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    UnityEngine.Object target = button.onClick.GetPersistentTarget(i);
                    string targetName = target is InspectionUIManager ? "InspectionUIManager" : target != null ? target.name : "null";
                    sb.Append($" call[{i}]={targetName}.{button.onClick.GetPersistentMethodName(i)}");
                }
            }
            sb.Append('\n');

            foreach (Transform child in node)
                Dump(child, depth + 1, sb);
        }

        // ToString gives "name (Type)". The settings that decide child sizes depend on the type.
        private static string Describe(LayoutGroup layout)
        {
            RectOffset p = layout.padding;
            string common = $"{layout} padding=({p.left},{p.right},{p.top},{p.bottom}) enabled={layout.enabled}";
            if (layout is HorizontalOrVerticalLayoutGroup hv)
                return common + $" spacing={hv.spacing} align={hv.childAlignment} control={hv.childControlWidth}/{hv.childControlHeight} expand={hv.childForceExpandWidth}/{hv.childForceExpandHeight}";
            if (layout is AdvancedHorizontalLayoutGroup adv)
                return common + $" spacing={adv.spacing} justify={adv.justifyContent} align={adv.childAlignment} control={adv.childControlWidth}/{adv.childControlHeight} expand={adv.childForceExpandWidth}/{adv.childForceExpandHeight}";
            return common + $" align={layout.childAlignment}";
        }
    }
}
