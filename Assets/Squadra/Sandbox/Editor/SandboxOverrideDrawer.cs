using UnityEditor;
using UnityEngine;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Shows a <see cref="SandboxOverride"/> on one line: a tick to override, then either the value used
    /// instead of the game's or, unticked, a note that the game's default applies.
    /// </summary>
    [CustomPropertyDrawer(typeof(SandboxOverride))]
    sealed class SandboxOverrideDrawer : PropertyDrawer
    {
        const float TickWidth = 18f;

        static readonly GUIContent DefaultNote = new GUIContent("Game default",
            "Not overridden: the value the game's code has now, whatever it becomes.");

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty overridden = property.FindPropertyRelative("overridden");
            SerializedProperty value = property.FindPropertyRelative("value");

            EditorGUI.BeginProperty(position, label, property);
            Rect field = EditorGUI.PrefixLabel(position, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var tick = new Rect(field.x, field.y, TickWidth, field.height);
            overridden.boolValue = EditorGUI.Toggle(tick, overridden.boolValue);

            var rest = new Rect(field.x + TickWidth, field.y, field.width - TickWidth, field.height);
            if (overridden.boolValue)
            {
                EditorGUI.PropertyField(rest, value, GUIContent.none);
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUI.LabelField(rest, DefaultNote);
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
