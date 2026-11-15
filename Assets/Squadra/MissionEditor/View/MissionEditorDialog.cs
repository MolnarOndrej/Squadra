using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>One answer a mission editor dialog offers.</summary>
    public readonly struct DialogChoice
    {
        public DialogChoice(string label, Action chosen)
        {
            Label = label;
            Chosen = chosen;
        }

        public string Label { get; }
        public Action Chosen { get; }
    }

    /// <summary>
    /// The mission editor's question box: a title, a message and up to three answers, over a layer that
    /// covers the whole editor so nothing beneath can be clicked. The last answer is always the safe
    /// one, the one that changes nothing; it has the focus when the dialog opens and is what Escape
    /// picks. Exactly one answer is given per dialog.
    /// </summary>
    public sealed class MissionEditorDialog
    {
        const string HiddenClass = "hidden";

        readonly VisualElement layer;
        readonly Label title;
        readonly Label message;
        readonly VisualElement buttonRow;
        readonly Action opened;
        readonly Action closed;
        readonly List<Button> buttons = new List<Button>();
        readonly List<Action> answers = new List<Action>();

        public MissionEditorDialog(VisualElement layer, Action onOpened, Action onClosed)
        {
            this.layer = layer;
            opened = onOpened;
            closed = onClosed;
            title = layer.Q<Label>("dialogTitle");
            message = layer.Q<Label>("dialogMessage");
            buttonRow = layer.Q("dialogButtons");

            // The layer takes every click that misses the dialog, so none reaches the editor under it.
            layer.RegisterCallback<PointerDownEvent>(pressed => pressed.StopPropagation());
            layer.RegisterCallback<WheelEvent>(scrolled => scrolled.StopPropagation());
        }

        public bool IsOpen { get; private set; }
        public string Title => title.text;
        public string Message => message.text;
        public int ChoiceCount => buttons.Count;

        public string ChoiceLabel(int index)
        {
            return buttons[index].text;
        }

        public void Show(string heading, string body, params DialogChoice[] choices)
        {
            if (choices == null || choices.Length == 0) throw new ArgumentException("A dialog needs an answer.", nameof(choices));

            buttonRow.Clear();
            buttons.Clear();
            answers.Clear();
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                var button = new Button(() => Choose(index)) { text = choices[i].Label };
                button.AddToClassList("tool-button");
                button.AddToClassList("dialog__button");
                if (i == 0 && choices.Length > 1) button.AddToClassList("dialog__button--primary");
                buttonRow.Add(button);
                buttons.Add(button);
                answers.Add(choices[i].Chosen);
            }

            title.text = heading;
            message.text = body;
            IsOpen = true;
            layer.RemoveFromClassList(HiddenClass);
            opened?.Invoke();

            // Focus only takes once the dialog is laid out.
            Button safe = buttons[buttons.Count - 1];
            layer.schedule.Execute(() =>
            {
                if (IsOpen && safe.panel != null && safe.canGrabFocus) safe.Focus();
            }).StartingIn(20);
        }

        /// <summary>Gives the answer at an index, as clicking its button does.</summary>
        public void Choose(int index)
        {
            if (!IsOpen || index < 0 || index >= answers.Count) return;

            Action answer = answers[index];
            IsOpen = false;
            layer.AddToClassList(HiddenClass);
            buttons.Clear();
            answers.Clear();
            buttonRow.Clear();
            closed?.Invoke();
            answer?.Invoke();
        }

        /// <summary>What Escape does: gives the last, safe answer.</summary>
        public void Escape()
        {
            Choose(answers.Count - 1);
        }
    }
}
