#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace Neuston.WhatUsesThis
{
	class AssetDependencyGraph
	{
		readonly Dictionary<string,List<string>> targetToSourceDictionary;

		public AssetDependencyGraph()
		{
			targetToSourceDictionary = CreateTargetToSourceDictionary();
		}

		static Dictionary<string, List<string>> CreateTargetToSourceDictionary()
		{
			var allAssetPaths = AssetDatabase.GetAllAssetPaths().ToList();

			var targetToSourceDictionary = allAssetPaths.ToDictionary(assetPath => assetPath, assetPath => new List<string>());

			for (var i = 0; i < allAssetPaths.Count; i++)
			{
				EditorUtility.DisplayProgressBar("Asset Dependency Graph", "Building graph...", (float) i / allAssetPaths.Count);

				var assetDependencies = AssetDatabase.GetDependencies(allAssetPaths[i], false);

				foreach (var assetDependency in assetDependencies)
				{
					if (targetToSourceDictionary.ContainsKey(assetDependency) && assetDependency != allAssetPaths[i])
					{
						targetToSourceDictionary[assetDependency].Add(allAssetPaths[i]);
					}
				}
			}

			EditorUtility.ClearProgressBar();

			return targetToSourceDictionary;
		}

		public Dictionary<string, List<string>> GetDependants(IEnumerable<string> selectedObjects)
		{
			var results = new Dictionary<string, List<string>>();

			foreach (var selectedObject in selectedObjects)
			{
				if (targetToSourceDictionary.TryGetValue(selectedObject, out var value))
				{
					results.Add(selectedObject, value);
				}
				else
				{
					Debug.LogWarning($"Not aware of asset at path {selectedObject}");
					results.Add(selectedObject, new List<string>());
				}
			}

			return results;
		}

		/// <summary>
		/// Get all assets that depend on the given asset.
		/// </summary>
		public List<string> GetAllAssetsThatDependOn(string assetPath)
		{
			if (targetToSourceDictionary.ContainsKey(assetPath))
			{
				return targetToSourceDictionary[assetPath];
			}

			throw new ArgumentException($"Asset path {assetPath} not found in dictionary.");
		}

		public void OnDeletedAsset(string deletedAssetPath)
		{
			targetToSourceDictionary.Remove(deletedAssetPath);

			foreach (var value in targetToSourceDictionary.Values)
			{
				value.RemoveAll(p => p == deletedAssetPath);
			}
		}
	}
}
