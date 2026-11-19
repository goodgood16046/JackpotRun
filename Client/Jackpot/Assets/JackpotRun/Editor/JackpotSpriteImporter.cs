using UnityEditor;
using UnityEngine;

namespace JackpotRun.Editor
{
    /// <summary>
    /// Assets/JackpotRun/Resources/JackpotRun/Sprites/ 아래 텍스처를 카드 아이콘용
    /// Sprite 로 일괄 임포트 설정한다.
    /// FUN_OVERHAUL_DESIGN §3 — 기존 8개 카테고리 폴더와 신규 폴더(Symbols · SymAugments · SymRelics ·
    /// Bosses)가 모두 이 경로 조각 아래라 같은 설정을 받는다. 심볼 계열은 투명 배경 PNG 이므로 알파를
    /// 입력 그대로 쓰고(alphaSource=FromInput) alphaIsTransparency 로 투명 가장자리 후광을 막는다.
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
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
        }
    }
}
