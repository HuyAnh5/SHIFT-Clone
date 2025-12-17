using UnityEditor;
using UnityEngine;

namespace DarkTonic.PoolBoss.EditorScript
{
	public static class UndoHelper
	{
		public static void CreateObjectForUndo(GameObject go, string actionName)
		{
			Undo.RegisterCreatedObjectUndo(go, actionName);
		}

		public static void SetTransformParentForUndo(Transform child, Transform newParent, string name)
		{
			Undo.SetTransformParent(child, newParent, name);
		}

		public static void RecordObjectPropertyForUndo(ref bool isDirty, Object objectProperty, string actionName)
		{
			isDirty = true;

			Undo.RecordObject(objectProperty, actionName);
		}

		public static void RecordObjectsForUndo(Object[] objects, string actionName)
		{
			Undo.RecordObjects(objects, actionName);

			foreach (Object o in objects)
			{
				EditorUtility.SetDirty(o);
			}
		}

		public static void DestroyForUndo(GameObject go)
		{
			Undo.DestroyObjectImmediate(go);
		}
	}
}