using UnityEditor;

namespace FishBreeder.EditorTools
{
    /// <summary>
    /// Import settings for fish models and textures under Assets/Resources/Fish,
    /// so a new Meshy fish only needs to be dropped into a folder there.
    /// </summary>
    public class FishAssetPostprocessor : AssetPostprocessor
    {
        const string FishFolder = "Assets/Resources/Fish/";

        public override uint GetVersion() => 1;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(FishFolder)) return;
            var importer = (ModelImporter)assetImporter;
            // Readable so FishVisual can work out which end is the head.
            importer.isReadable = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FishFolder)) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("_Normal.png")) importer.textureType = TextureImporterType.NormalMap;
            importer.maxTextureSize = 1024;
        }
    }
}
