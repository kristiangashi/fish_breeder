using UnityEditor;

namespace FishBreeder.EditorTools
{
    /// <summary>
    /// Import settings for Meshy models and textures under Assets/Resources/Fish and
    /// Assets/Resources/Decor, so a new model only needs to be dropped into a folder there.
    /// </summary>
    public class FishAssetPostprocessor : AssetPostprocessor
    {
        const string FishFolder = "Assets/Resources/Fish/";
        const string DecorFolder = "Assets/Resources/Decor/";

        public override uint GetVersion() => 3;

        static bool IsManaged(string path) => path.StartsWith(FishFolder) || path.StartsWith(DecorFolder);

        void OnPreprocessModel()
        {
            if (!IsManaged(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            // Readable so FishVisual can work out which end is the head, and decor can build mesh colliders.
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
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
