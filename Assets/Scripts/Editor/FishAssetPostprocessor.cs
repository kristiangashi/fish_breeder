using UnityEditor;

namespace FishBreeder.EditorTools
{
    /// <summary>
    /// Import settings for Meshy models and textures under Assets/Resources/Fish,
    /// Assets/Resources/Decor, Assets/Resources/Equipment and Assets/Resources/Crew, so a new model only needs to be dropped
    /// into a folder there. Crew models keep their skeleton (moved by code, see CrewRig).
    /// </summary>
    public class FishAssetPostprocessor : AssetPostprocessor
    {
        const string FishFolder = "Assets/Resources/Fish/";
        const string DecorFolder = "Assets/Resources/Decor/";
        const string CrewFolder = "Assets/Resources/Crew/";
        const string EquipmentFolder = "Assets/Resources/Equipment/";

        public override uint GetVersion() => 5;

        static bool IsManaged(string path) => path.StartsWith(FishFolder) || path.StartsWith(DecorFolder) || path.StartsWith(CrewFolder) || path.StartsWith(EquipmentFolder);

        void OnPreprocessModel()
        {
            if (!IsManaged(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            // Readable so FishVisual can work out which end is the head, and decor and equipment can build mesh colliders.
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            if (assetPath.StartsWith(CrewFolder))
            {
                importer.isReadable = false;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.optimizeGameObjects = false;
            }
        }

        void OnPreprocessTexture()
        {
            if (!IsManaged(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("_Normal.png")) importer.textureType = TextureImporterType.NormalMap;
            // Metallic in R, smoothness in A: linear data, not colour.
            if (assetPath.EndsWith("_MetallicSmoothness.png")) importer.sRGBTexture = false;
            // Smoothness maps are data, not colour.
            if (assetPath.EndsWith("_Smoothness.png")) importer.sRGBTexture = false;
            // Keep Meshy's full 2048 textures: several fish or decorations share each atlas.
            importer.maxTextureSize = 2048;
        }
    }
}
