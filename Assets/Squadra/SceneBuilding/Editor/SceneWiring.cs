using UnityEditor;
using UnityEngine;

namespace Squadra.EditorTools
{
    /// <summary>
    /// Sets the serialized fields of components from builder code, by field name, the way the
    /// Inspector would. A field name that does not exist is an error, so a renamed field is noticed
    /// the first time a builder runs.
    /// </summary>
    static class SceneWiring
    {
        public static SerializedProperty Property(SerializedObject serialized, string field)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
                throw new System.InvalidOperationException(serialized.targetObject.GetType().Name + " has no field " + field);
            return property;
        }

        public static void Wire(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Wire(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = Property(serialized, field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Wire(Object target, string field, string value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireInt(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireEnum(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void WireRect(Object target, string field, Rect value)
        {
            var serialized = new SerializedObject(target);
            Property(serialized, field).rectValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Re-runs OnEnable now that the references are wired, so the edit-time layout is applied.</summary>
        public static void Relayout(Behaviour behaviour)
        {
            behaviour.enabled = false;
            behaviour.enabled = true;
        }
    }
}
