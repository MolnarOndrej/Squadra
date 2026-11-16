using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Squadra
{
    /// <summary>
    /// Builds the rows of the mission editor's property panel: a label on the left and a value or a
    /// control on the right, styled by the classes of <c>MissionEditor.uss</c>.
    /// </summary>
    static class MissionEditorRows
    {
        /// <summary>Marks a control whose text is not a valid value.</summary>
        public const string InvalidClass = "row__control--invalid";

        public static Label Note(string text)
        {
            var note = new Label(text);
            note.AddToClassList("section__note");
            return note;
        }

        /// <summary>A note under a row, in line with the row's control: what the value above it means.</summary>
        public static Label RowNote(VisualElement parent, string text)
        {
            Label note = Note(text);
            note.AddToClassList("row__note");
            parent.Add(note);
            return note;
        }

        public static VisualElement Row(VisualElement parent, string label)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            var name = new Label(label);
            name.AddToClassList("row__label");
            row.Add(name);
            parent.Add(row);
            return row;
        }

        public static Label ReadOnlyRow(VisualElement parent, string label, string value)
        {
            var text = new Label(value);
            text.AddToClassList("row__value");
            Row(parent, label).Add(text);
            return text;
        }

        public static DropdownField Dropdown(VisualElement parent, string label, List<string> choices)
        {
            var field = new DropdownField { choices = choices };
            field.AddToClassList("row__control");
            Row(parent, label).Add(field);
            return field;
        }
    }
}
