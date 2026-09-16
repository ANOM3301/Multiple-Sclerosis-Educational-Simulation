using UnityEditor;
using UnityEngine;

namespace Convai.Editor.AI
{
    internal static class ConvaiMcpEntityRef
    {
        internal static long ToToolId(Object value) => value != null
            ? unchecked((long)EntityId.ToULong(value.GetEntityId()))
            : 0L;

        internal static Object Resolve(long id)
        {
            if (id == 0) return null;

            Object value = EditorUtility.EntityIdToObject(EntityId.FromULong(unchecked((ulong)id)));
            if (value != null || id < int.MinValue || id > int.MaxValue) return value;

#pragma warning disable CS0618 // Legacy-ID fallback accepts historical MCP instance IDs forever.
            return EditorUtility.InstanceIDToObject((int)id);
#pragma warning restore CS0618
        }

        internal static bool TryResolve<T>(long id, out T value) where T : Object
        {
            Object resolved = Resolve(id);
            if (resolved is T direct)
            {
                value = direct;
                return true;
            }

            if (resolved is GameObject gameObject &&
                typeof(Component).IsAssignableFrom(typeof(T)))
            {
                value = gameObject.GetComponent(typeof(T)) as T;
                return value != null;
            }

            if (resolved is Component component && component.gameObject is T carrier)
            {
                value = carrier;
                return true;
            }

            value = null;
            return false;
        }
    }
}
