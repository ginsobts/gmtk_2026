using UnityEditor;
using UnityEngine;

/// <summary>
/// 自动给美术资源配置导入设置，省去美术手动调 Importer，也避免忘设置导致的坑。
/// 作用范围：Resources/Art/Characters、Resources/Art/Imposters、Resources/Art/Camera。
/// - 统一：Sprite 类型、底部中心轴、100 PPU、无 mipmap、Clamp、alphaIsTransparency、不压缩（2D 立绘更清晰）。
/// - 相机外壳等需要在运行时读像素（自动量取取景开口），额外开启 isReadable。
/// 其它图集/地面贴图不在范围内，保持原样，避免回归。
/// </summary>
public class ArtImportPostprocessor : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/Resources/Art/")) return;

        bool isCharacterLike =
            p.Contains("/Resources/Art/Characters/") ||
            p.Contains("/Resources/Art/Imposters/") ||
            p.Contains("/Resources/Art/Camera/");
        if (!isCharacterLike) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter; // 立绘脚底为轴
        ti.SetTextureSettings(settings);

        // 相机相关贴图需要 CPU 读像素（取景开口自动量取）。
        if (p.Contains("/Resources/Art/Camera/"))
            ti.isReadable = true;
    }
}
