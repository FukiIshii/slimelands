using UnityEngine;
using UnityEngine.InputSystem;

namespace Slimelands.CameraControls
{
    /// <summary>
    /// マウスホイール(スクロール)でカメラのorthographicSizeを増減し、拡大縮小できるようにする。
    /// 村パート・ダンジョンパートどちらのシーンでも使える汎用コンポーネントとして、
    /// CardBoard等の内部実装には依存させない(対象カメラにアタッチするだけで動作する)。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraZoomController : MonoBehaviour
    {
        [Header("拡大縮小倍率")]
        [Tooltip("スクロール量に対してorthographicSizeをどれだけ変化させるか。大きいほど1スクロールあたりの拡大縮小が速くなる。")]
        [SerializeField, Range(0.1f, 10f)] private float zoomSpeed = 2f;
        [Tooltip("最も拡大したときのorthographicSize(値が小さいほど寄る)。")]
        [SerializeField, Range(0.5f, 20f)] private float minOrthographicSize = 2f;
        [Tooltip("最も縮小したときのorthographicSize(値が大きいほど引く)。")]
        [SerializeField, Range(0.5f, 20f)] private float maxOrthographicSize = 10f;

        private Camera targetCamera;
        private float defaultOrthographicSize;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            defaultOrthographicSize = targetCamera.orthographicSize;
        }

        /// <summary>
        /// このコンポーネントが無効化された瞬間(GameScreenControllerが村画面以外へ切り替える際に
        /// enabled=falseにする)、ズームを初期値へ戻す。村・ダンジョンでカメラ自体を共有しており
        /// (シーン遷移ではなくSetActive/enabled切り替え方式)、無効化してもorthographicSizeの値は
        /// 自然には戻らないため、村で拡大したまま他の画面(ダンジョン等)に遷移すると拡大されっぱなしに
        /// なってしまう不具合があった。画面遷移側(GameScreenController)にズームの内部実装を
        /// 知らせずに解決するため、無効化をきっかけに自分自身でリセットする。
        /// </summary>
        private void OnDisable()
        {
            if (targetCamera != null)
            {
                targetCamera.orthographicSize = defaultOrthographicSize;
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.value.y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize - scroll * zoomSpeed * 0.01f,
                minOrthographicSize,
                maxOrthographicSize);
        }
    }
}
