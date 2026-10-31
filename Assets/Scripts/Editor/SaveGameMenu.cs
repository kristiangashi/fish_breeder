using FishBreeder.Core;
using UnityEditor;
using UnityEngine;

namespace FishBreeder.EditorTools
{
    /// <summary>Developer menu for the game's one save file.</summary>
    public static class SaveGameMenu
    {
        [MenuItem("Fish Breeder/Delete Save (start a new game)")]
        static void DeleteSave()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[FishBreeder] Stop playing first: quitting saves the game again.");
                return;
            }
            SaveGame.Delete();
            Debug.Log($"[FishBreeder] Save deleted ({SaveGame.FilePath}). The next play starts a new game.");
        }

        [MenuItem("Fish Breeder/Show Save File")]
        static void ShowSave()
        {
            if (SaveGame.Exists) EditorUtility.RevealInFinder(SaveGame.FilePath);
            else Debug.Log($"[FishBreeder] No save yet. It will be written to {SaveGame.FilePath}.");
        }
    }
}
