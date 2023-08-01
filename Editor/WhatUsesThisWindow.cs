#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace Neuston.WhatUsesThis
{
	public class WhatUsesThisWindow : EditorWindow
	{
		class InvolvedAsset
		{
			public InvolvedAsset(string assetPath)
			{
				AssetPath = assetPath;
			}

			public string AssetPath { get; set; }
			public string FileName => Path.GetFileName(AssetPath);
			public bool IsChecked { get; set; }
			public bool IsInResources => AssetPath.Contains("/Resources/");
		}

		AssetDependencyGraph? assetDependencyGraph;
		List<InvolvedAsset> involvedAssets = new List<InvolvedAsset>();
		int expectedProjectChanges;
		Vector2 scrollPosition;
		GUIStyle redTextStyle = null!;

		[MenuItem("Assets/What Uses This", priority = 2500)]
		public static void OpenWhatUsesThisWindowAndStartSearch()
		{
			var window = GetWindow<WhatUsesThisWindow>();
			window.Start();

			if (Selection.activeObject != null)
			{
				window.FindUsagesOf(Selection.activeObject);
			}
		}

		[MenuItem("Tools/Neuston/What Uses This")]
		public static void OpenWhatUsesThisWindow()
		{
			var window = GetWindow<WhatUsesThisWindow>();
			window.Start();
		}

		void Start()
		{
			titleContent = new GUIContent("What Uses This");
			redTextStyle = new GUIStyle { normal = { textColor = Color.red }, alignment = TextAnchor.MiddleCenter };
		}

		void OnProjectChange()
		{
			// After deleting files from this tool, we predict one project change for each deleted asset.
			if (expectedProjectChanges > 0)
			{
				expectedProjectChanges--;
			}
			else
			{
				// If we get a project change that we didn't expect (by our own delete op) we clear the state.
				ClearState();
			}
		}

		void OnDestroy()
		{
			// If we close the window we better clear the state.
			ClearState();
		}

		void ClearState()
		{
			assetDependencyGraph = null;
			involvedAssets.Clear();
			expectedProjectChanges = 0;
		}

		void OnGUI()
		{
			if (expectedProjectChanges > 0)
			{
				GUILayout.Label("Waiting for expected project changes...");
				return;
			}

			if (Selection.activeObject != null)
			{
				if (GUILayout.Button($"Find Usages of {Selection.activeObject.name}"))
				{
					FindUsagesOf(Selection.activeObject);
				}
			}
			else
			{
				GUILayout.Label("Select an object in the Project window for options.");
			}

			DrawInvolvedAssetRows();
		}

		void DrawInvolvedAssetRows()
		{
			if (involvedAssets.Count <= 0)
			{
				return;
			}

			GUILayout.Label("The asset is used by the following assets (directly or indirectly):");

			scrollPosition = GUILayout.BeginScrollView(scrollPosition);

			foreach (var involvedAsset in involvedAssets)
			{
				DrawInvolvedAssetRow(involvedAsset);
			}

			GUILayout.Space(32);

			GUILayout.BeginHorizontal();

			if (GUILayout.Button("Select All", GUILayout.Width(120)))
			{
				SelectAllAssets();
			}

			if (GUILayout.Button("Delete Selected", GUILayout.Width(120)))
			{
				DeleteSelectedAssets();
			}

			GUILayout.EndHorizontal();

			GUILayout.EndScrollView();
		}

		void SelectAllAssets()
		{
			if (involvedAssets.Any(a => !a.IsChecked))
			{
				involvedAssets.ForEach(a => a.IsChecked = true);
			}
			else
			{
				involvedAssets.ForEach(a => a.IsChecked = false);
			}
		}

		void DeleteSelectedAssets()
		{
			var assetsToDelete = involvedAssets.Where(a => a.IsChecked).Select(a => a.AssetPath).ToList();
			foreach (string assetPath in assetsToDelete)
			{
				expectedProjectChanges++;
				AssetDatabase.DeleteAsset(assetPath);
				RemoveDeletedAssetFromState(assetPath);
				Debug.Log($"Deleted {assetPath}");
			}
		}

		void DrawInvolvedAssetRow(InvolvedAsset involvedAsset)
		{
			GUILayout.BeginHorizontal();

			var height = GUILayout.Height(18);

			involvedAsset.IsChecked = GUILayout.Toggle(involvedAsset.IsChecked, string.Empty, GUILayout.Width(16), height);

			var resContent = new GUIContent(involvedAsset.IsInResources ? "RES" : string.Empty)
			{
				tooltip = "This asset is in Resources and could potentially be used from code."
			};
			GUILayout.Label(resContent, redTextStyle, GUILayout.Width(32), height);

			var resultPath = involvedAsset.AssetPath;
			var type = AssetDatabase.GetMainAssetTypeAtPath(resultPath);
			var guiContent = EditorGUIUtility.ObjectContent(null, type);
			string fileName = Path.GetFileName(resultPath);
			guiContent.text = fileName;
			guiContent.tooltip = fileName;

			var before = GUI.skin.button.alignment;
			GUI.skin.button.alignment = TextAnchor.MiddleLeft;
			if (GUILayout.Button(guiContent, GUILayout.Width(240), height))
			{
				EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(involvedAsset.AssetPath));
			}

			GUI.skin.button.alignment = before;

			GUILayout.Label(resultPath, height);

			GUILayout.EndHorizontal();
		}

		void RemoveDeletedAssetFromState(string deletedAssetPath)
		{
			involvedAssets.RemoveAll(a => a.AssetPath == deletedAssetPath);
			assetDependencyGraph?.OnDeletedAsset(deletedAssetPath);
		}

		void FindUsagesOf(Object activeObject)
		{
			assetDependencyGraph ??= new AssetDependencyGraph();

			var assetsToVisit = new Queue<string>();
			var visitedAssets = new HashSet<string>();

			assetsToVisit.Enqueue(AssetDatabase.GetAssetPath(activeObject));

			while (assetsToVisit.Any())
			{
				string visitingAsset = assetsToVisit.Dequeue();
				visitedAssets.Add(visitingAsset);

				var result = assetDependencyGraph.GetAllAssetsThatDependOn(visitingAsset);
				foreach (var path in result)
				{
					if (!visitingAsset.Contains(path))
					{
						assetsToVisit.Enqueue(path);
					}
				}
			}

			involvedAssets = visitedAssets.Select(p => new InvolvedAsset(p)).ToList();
			involvedAssets.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.Ordinal));
		}
	}
}
