using System;
using Squadra.MissionEditing;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// One editable text row. What is typed is applied to the mission at once when it is a valid
    /// value. Otherwise the text stays in the field, marked invalid with the reason under it, the
    /// mission keeps its value, and the session is told, so that a save or a change of mission
    /// cannot quietly lose what was typed.
    /// </summary>
    sealed class MissionTextEntry
    {
        readonly MissionEditorSession session;
        readonly string key;
        readonly string label;
        readonly bool ofSelection;
        readonly Func<string> read;
        readonly Func<string, string> apply;
        readonly Func<string, bool> shows;
        readonly TextField field;
        readonly Label error;

        /// <param name="read">The mission's value as field text.</param>
        /// <param name="apply">Applies typed text; returns why it could not, or null.</param>
        /// <param name="shows">True when typed text already stands for the mission's value.</param>
        public MissionTextEntry(MissionEditorSession session, VisualElement parent, string key, string label,
            bool ofSelection, Func<string> read, Func<string, string> apply, Func<string, bool> shows)
        {
            this.session = session;
            this.key = key;
            this.label = label;
            this.ofSelection = ofSelection;
            this.read = read;
            this.apply = apply;
            this.shows = shows;

            field = new TextField { name = key, value = read() };
            field.AddToClassList("row__control");
            MissionEditorRows.Row(parent, label).Add(field);

            error = new Label();
            error.AddToClassList("row__error");
            error.style.display = DisplayStyle.None;
            parent.Add(error);

            field.RegisterValueChangedCallback(changed => OnTyped(changed.newValue));
        }

        /// <summary>The reason an edit was refused as a field shows it; null when it was not refused.</summary>
        public static string Problem(EditResult result)
        {
            return result.Rejected ? result.Message : null;
        }

        void OnTyped(string text)
        {
            string problem = apply(text);
            if (problem == null) session.ClearInvalidInput(key);
            else session.ReportInvalidInput(key, label, ofSelection);
            Mark(problem);
        }

        /// <summary>Shows the mission's value, unless the author's invalid text is still waiting.</summary>
        public void Refresh()
        {
            if (session.IsInvalidInput(key)) return;

            Mark(null);
            if (!shows(field.value)) field.SetValueWithoutNotify(read());
        }

        /// <summary>The row is going away; whatever was typed in it goes with it.</summary>
        public void Forget()
        {
            session.ClearInvalidInput(key);
        }

        void Mark(string problem)
        {
            field.EnableInClassList(MissionEditorRows.InvalidClass, problem != null);
            error.text = problem ?? "";
            error.style.display = problem != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
