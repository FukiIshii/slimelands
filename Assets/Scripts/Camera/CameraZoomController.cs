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
        [Tooltip("スクロール量に対してorthographicSizeをどれだけ変化させるか。")]
        [SerializeField] private float zoomSpeed = 2f;
        [Tooltip("最も拡大したときのorthographicSize(値が小さいほど寄る)。")]
        [SerializeField] private float minOrthographicSize = 2f;
        [Tooltip("最も縮小したときのorthographicSize(値が大きいほど引く)。")]
        [SerializeField] private float maxOrthographicSize = 10f;

        private Camera targetCamera;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
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
