using Slimelands.Dungeon;
using UnityEngine;

namespace Slimelands.CameraControls
{
    /// <summary>
    /// ダンジョン戦闘画面が表示されている間だけ有効化され(GameScreenController)、カメラの
    /// orthographicSizeをDungeonRunControllerの行動帯(MovementBandWidth/Height)全体がちょうど
    /// 画面に収まる大きさへ調整する。
    /// 行動帯そのものを画面のアスペクト比に合わせて縮める案も試したが、交戦距離
    /// (MeleeContactDistance/RangedRetreatDistance等)は行動帯の元のサイズを前提に調整された値のため、
    /// 帯だけを縮めると間隔調整用の値が相対的に広すぎになり、キャラクターが中途半端な位置で
    /// 縦一列に固まってしまう不具合になった。そのため帯のサイズ(=バランス値)には触れず、逆に
    /// カメラ側を行動帯に合わせて広げる(必要な場合のみズームアウトする)方式にしている。
    /// 村・ダンジョンでカメラを共有しており(CameraZoomControllerと同じ設計)、無効化された瞬間に
    /// 元のorthographicSizeへ戻す。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class DungeonCameraFitter : MonoBehaviour
    {
        [SerializeField] private DungeonRunController controller;
        [Tooltip("行動帯の外周からさらに余白としてどれだけ広く映すか(ワールド単位、片側あたり)。")]
        [SerializeField] private float margin = 0.3f;

        private Camera targetCamera;
        private float defaultOrthographicSize;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            defaultOrthographicSize = targetCamera.orthographicSize;
        }

        /// <summary>
        /// 無効化された瞬間(GameScreenControllerがダンジョン戦闘画面以外へ切り替える際に
        /// enabled=falseにする)、orthographicSizeを元の値へ戻す。CameraZoomController.OnDisableと
        /// 同じ設計。
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
            if (controller == null || targetCamera == null || !targetCamera.orthographic)
            {
                return;
            }

            float halfHeightNeeded = controller.MovementBandHeight * 0.5f + margin;
            float aspect = targetCamera.aspect;
            float halfWidthNeeded = controller.MovementBandWidth * 0.5f + margin;
            float requiredSize = aspect > 0.0001f
                ? Mathf.Max(halfHeightNeeded, halfWidthNeeded / aspect)
                : halfHeightNeeded;

            // 元のズーム(defaultOrthographicSize)より狭い範囲で足りる場合はそのまま(見た目のスケール感を
            // 変えない)。行動帯全体を映すのに元のズームでは足りない場合のみズームアウトする。
            targetCamera.orthographicSize = Mathf.Max(requiredSize, defaultOrthographicSize);
        }
    }
}
