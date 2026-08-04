using UnityEngine;
using Slimelands.Cards;

namespace Slimelands.Village
{
    /// <summary>
    /// ガチャを引くための最小限のUI(IMGUIのボタン1つ)。
    /// ロジック側の動作確認を優先するための仮UIで、見た目の作り込みは行わない。
    /// </summary>
    public class GachaUI : MonoBehaviour
    {
        [SerializeField] private GachaManager gachaManager;

        private string lastResultMessage = string.Empty;

        private void OnEnable()
        {
            if (gachaManager != null)
            {
                gachaManager.OnGachaDrawn += HandleGachaDrawn;
                gachaManager.OnGachaFailed += HandleGachaFailed;
            }
        }

        private void OnDisable()
        {
            if (gachaManager != null)
            {
                gachaManager.OnGachaDrawn -= HandleGachaDrawn;
                gachaManager.OnGachaFailed -= HandleGachaFailed;
            }

            ScreenUIPanelRegistry.SetPanelRect(this, null);
        }

        private void HandleGachaDrawn(SlimeCardData drawn)
        {
            lastResultMessage = drawn != null ? $"{drawn.CardName} が出た!" : string.Empty;
        }

        private void HandleGachaFailed(string reason)
        {
            lastResultMessage = $"ガチャ失敗: {reason}";
        }

        private void OnGUI()
        {
            Rect panelRect = new(10f, 150f, 220f, 108f);
            ScreenUIPanelRegistry.SetPanelRect(this, panelRect);
            GUI.Box(panelRect, string.Empty);
            GUILayout.BeginArea(new Rect(20f, 156f, 200f, 96f));

            if (GUILayout.Button("ガチャを引く", GUILayout.Height(32f)))
            {
                gachaManager?.TryDrawGacha();
            }

            GUILayout.Label(GetCostLabel());
            GUILayout.Label(lastResultMessage);
            GUILayout.EndArea();
        }

        /// <summary>ガチャボタンの下に表示する消費コストの説明文。GachaManager側の設定を仮値含めそのまま反映する。</summary>
        private string GetCostLabel()
        {
            if (gachaManager == null || gachaManager.CostResource == null || gachaManager.CostAmount <= 0)
            {
                return "消費: なし";
            }

            return $"消費: {gachaManager.CostResource.CardName} {gachaManager.CostAmount}個";
        }
    }
}
