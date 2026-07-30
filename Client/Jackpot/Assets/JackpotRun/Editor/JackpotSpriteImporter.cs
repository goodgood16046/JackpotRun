using UnityEditor;
using UnityEngine;

namespace JackpotRun.Editor
{
    /// <summary>
    /// Assets/JackpotRun/Resources/JackpotRun/Sprites/ 아래 텍스처를 카드 아이콘용
    /// Sprite 로 일괄 임포트 설정한다.
    /// </summary>
    public class JackpotSpriteImporter : AssetPostprocessor
    {
        private const string TargetPathFragment = "JackpotRun/Resources/JackpotRun/Sprites/";

        private void OnPreprocessTexture()
        {
            var normalized = assetPath.Replace('\\', '/');
            if (!normalized.Contains(TargetPathFragment)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 256;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
        }
    }
}
