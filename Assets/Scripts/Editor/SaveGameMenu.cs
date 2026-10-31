using FishBreeder.Core;
using UnityEditor;
using UnityEngine;

namespace FishBreeder.EditorTools
{
    /// <summary>Developer menu for the editor's saves (builds keep theirs in another folder).</summary>
    public static class SaveGameMenu
    {
        [MenuItem("Fish Breeder/Delete Editor Saves")]
        static void DeleteSaves()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FishBreeder] Stop playing first: quitting saves the game again.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Delete editor saves", $"Delete every save in\n{SaveGame.Folder}?\nBuilt games keep their own saves.", "Delete", "Cancel")) return;
            SaveGame.DeleteAll();
            Debug.Log($"[FishBreeder] Editor saves deleted ({SaveGame.Folder}).");
        }

        [MenuItem("Fish Breeder/Show Save Folder")]
        static void ShowSaves()
        {
            System.IO.Directory.CreateDirectory(SaveGame.Folder);
            EditorUtility.RevealInFinder(SaveGame.Folder + System.IO.Path.DirectorySeparatorChar);
        }
    }
}
