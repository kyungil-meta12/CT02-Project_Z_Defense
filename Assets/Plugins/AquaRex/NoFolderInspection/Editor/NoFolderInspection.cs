using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.IO;

namespace NoFolderInspection
{

	[InitializeOnLoad]
	public class NoFolderInspection
	{
		private static Object _lastNonFolderSelection;
		private const string _prefKey = "NoFolderInspection_Enabled";
		private const string _sessionLockedByScriptKey = "NoFolderInspection_LockedByScript";
		private const int _leftMouseButton = 0;
		private static bool _isEnabled;

		private static bool LockedByScript
		{
			get => SessionState.GetBool(_sessionLockedByScriptKey, false);
			set => SessionState.SetBool(_sessionLockedByScriptKey, value);
		}

		static NoFolderInspection()
		{
			_isEnabled = EditorPrefs.GetBool(_prefKey, true);
			EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
			Selection.selectionChanged += OnSelectionChanged;
			EditorApplication.delayCall += ReconcileLockState;
		}

		private static void ReconcileLockState()
		{
			if (!_isEnabled) return;
			if (LockedByScript && !IsFolder(Selection.activeObject))
			{
				LockInspectors(false);
				LockedByScript = false;
			}
		}

		[MenuItem("Tools/No Folder Inspector")]
		private static void ToggleFeature()
		{
			_isEnabled = !_isEnabled;
			EditorPrefs.SetBool(_prefKey, _isEnabled);
			if (!_isEnabled)
			{
				LockInspectors(false);
				LockedByScript = false;
			}
		}

		[MenuItem("Tools/No Folder Inspector", true)]
		private static bool ToggleFeatureValidate()
		{
			Menu.SetChecked("Tools/No Folder Inspector", _isEnabled);
			return true;
		}

		private static bool IsAnyInspectorLockedByUser()
		{
			EditorWindow[] inspectors = Resources.FindObjectsOfTypeAll<EditorWindow>();
			foreach (EditorWindow window in inspectors)
			{
				if (window.GetType().Name == "InspectorWindow")
				{
					PropertyInfo isLockedProperty = window.GetType().GetProperty("isLocked",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (isLockedProperty != null)
					{
						bool isLocked = (bool)isLockedProperty.GetValue(window);
						if (isLocked) return true;
					}
				}
			}
			return false;
		}

		private static void OnProjectWindowItemGUI(string guid, Rect rect)
		{
			if (!_isEnabled) return;
			Event e = Event.current;
			if (e.type != EventType.MouseDown || e.button != _leftMouseButton) return;
			if (!rect.Contains(e.mousePosition)) return;

			string path = AssetDatabase.GUIDToAssetPath(guid);
			if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

			if (LockedByScript || !IsAnyInspectorLockedByUser())
			{
				LockInspectors(true);
				LockedByScript = true;
			}
		}

		private static void OnSelectionChanged()
		{
			if (!_isEnabled) return;

			Object currentSelection = Selection.activeObject;
			if (currentSelection == null || IsFolder(currentSelection)) return;

			if (LockedByScript)
			{
				LockInspectors(false);
				LockedByScript = false;
			}
			_lastNonFolderSelection = currentSelection;
		}

		private static void LockInspectors(bool lockState)
		{
			EditorWindow[] inspectors = Resources.FindObjectsOfTypeAll<EditorWindow>();
			foreach (EditorWindow window in inspectors)
			{
				if (window.GetType().Name == "InspectorWindow")
				{
					PropertyInfo isLockedProperty = window.GetType().GetProperty("isLocked",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

					if (isLockedProperty != null)
					{
						isLockedProperty.SetValue(window, lockState);
						window.Repaint();
					}
				}
			}
		}

		private static bool IsFolder(Object obj)
		{
			if (obj == null) return false;
			string path = AssetDatabase.GetAssetPath(obj);
			if (string.IsNullOrEmpty(path)) return false;
			return Directory.Exists(path);
		}
	}
}