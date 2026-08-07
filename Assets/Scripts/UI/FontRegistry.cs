using TMPro;
using UnityEngine;

namespace Slimelands.UI
{
    /// <summary>
    /// コード生成のワールド空間テキスト(FloatingNumberPopup、CardControllerのラベル等)が
    /// 共通で使うTMPフォントアセットの参照をキャッシュする。プレハブ経由ではなく
    /// AddComponentで動的に生成するテキストが多いため、Inspectorでの配線ができない
    /// 箇所向けにResources経由でロードする。
    /// </summary>
    public static class FontRegistry
    {
        private static TMP_FontAsset misakiGothic;

        public static TMP_FontAsset MisakiGothic =>
            misakiGothic ??= Resources.Load<TMP_FontAsset>("Fonts/MisakiGothic2nd SDF");
    }
}
