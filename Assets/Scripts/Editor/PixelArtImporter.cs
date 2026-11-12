using UnityEditor;
using UnityEngine;

public class PixelArtImporter : AssetPostprocessor
{
    private const string SpritesFolder = "Assets/Art/Sprites/";
    private const float PixelsPerUnit = 32f;
    private const uint ExtrudeEdges = 1;
    private const int MaxTextureSize = 2048;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SpritesFolder))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        ApplySpriteSettings(importer);
        ApplyCompressionSettings(importer);
    }

    private void ApplySpriteSettings(TextureImporter importer)
    {
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureType = TextureImporterType.Sprite;
        if (importer.importSettingsMissing)
        {
            settings.spriteMode = (int)SpriteImportMode.Single;
        }

        settings.spritePixelsPerUnit = PixelsPerUnit;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = ExtrudeEdges;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteGenerateFallbackPhysicsShape = true;
        settings.wrapMode = TextureWrapMode.Clamp;
        settings.filterMode = FilterMode.Point;
        importer.SetTextureSettings(settings);
    }

    private void ApplyCompressionSettings(TextureImporter importer)
    {
        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.maxTextureSize = MaxTextureSize;
        platform.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
        platform.format = TextureImporterFormat.Automatic;
        platform.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(platform);
    }
}
