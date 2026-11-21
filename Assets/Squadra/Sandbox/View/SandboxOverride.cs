using System;
using UnityEngine;

namespace Squadra
{
    /// <summary>
    /// One behavior value the sandbox may play by instead of the game's own. Unticked, the value is the
    /// game's default as the code has it now, whatever is typed in the field; ticked, the field is used.
    /// The field starts at the default the code had when the sandbox was made, as a starting point only.
    /// </summary>
    [Serializable]
    public struct SandboxOverride
    {
        [SerializeField] bool overridden;
        [SerializeField] float value;

        /// <summary>Not overridden, with <paramref name="start"/> in the field for when it is ticked.</summary>
        public SandboxOverride(float start)
        {
            overridden = false;
            value = start;
        }

        /// <summary>The overriding value, or null for the game's default.</summary>
        public float? Value => overridden ? value : (float?)null;

        /// <summary>The overriding value as a whole number, or null for the game's default.</summary>
        public int? WholeValue => overridden ? Mathf.RoundToInt(value) : (int?)null;
    }
}
