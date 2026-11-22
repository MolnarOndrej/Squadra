using UnityEngine;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// Shows a control's tooltip beside the pointer once it has rested on the control for a moment, and
    /// hides it when the pointer leaves or presses. UI Toolkit draws tooltips only in the editor's own
    /// windows, never on a game's panel, so the sandbox draws its own. It reads the control's tooltip when
    /// it shows, so a tooltip that changes with the state is shown as it is then.
    /// </summary>
    public sealed class SandboxTooltip
    {
        const long DelayMilliseconds = 450;
        const float Gap = 18f;

        readonly VisualElement root;
        readonly Label label;
        VisualElement owner;
        Vector2 pointer;
        IVisualElementScheduledItem pending;

        public SandboxTooltip(VisualElement root)
        {
            this.root = root;
            label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("sandbox-tooltip");
            label.style.display = DisplayStyle.None;
            root.Add(label);

            root.RegisterCallback<PointerMoveEvent>(OnMove, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerOutEvent>(OnOut, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(_ => Hide(), TrickleDown.TrickleDown);
        }

        void OnMove(PointerMoveEvent e)
        {
            pointer = root.WorldToLocal(e.position);
            VisualElement over = WithTooltip(e.target as VisualElement);
            if (over == owner) return;

            Hide();
            owner = over;
            if (owner != null) pending = root.schedule.Execute(Show).StartingIn(DelayMilliseconds);
        }

        // Leaving the control for anything outside it, or for nothing at all.
        void OnOut(PointerOutEvent e)
        {
            if (owner == null || owner.panel == null) return;
            VisualElement now = owner.panel.Pick(e.position);
            if (now != null && (now == owner || owner.Contains(now))) return;

            Hide();
            owner = null;
        }

        // The control itself or the nearest one around it that has a tooltip.
        static VisualElement WithTooltip(VisualElement element)
        {
            for (VisualElement at = element; at != null; at = at.parent)
            {
                if (!string.IsNullOrEmpty(at.tooltip)) return at;
            }

            return null;
        }

        void Show()
        {
            pending = null;
            if (owner == null || owner.panel == null || string.IsNullOrEmpty(owner.tooltip)) return;

            // Under the pointer, and to its left on the right half of the screen, where the controls are.
            bool leftOfPointer = pointer.x > root.layout.width * 0.5f;
            label.text = owner.tooltip;
            label.style.left = pointer.x + (leftOfPointer ? -Gap * 0.5f : Gap * 0.5f);
            label.style.top = pointer.y + Gap;
            label.style.translate = new Translate(Length.Percent(leftOfPointer ? -100f : 0f), 0f);
            label.style.display = DisplayStyle.Flex;
            label.BringToFront();
        }

        void Hide()
        {
            pending?.Pause();
            pending = null;
            label.style.display = DisplayStyle.None;
        }
    }
}
