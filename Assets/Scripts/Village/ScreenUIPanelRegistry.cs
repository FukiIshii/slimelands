using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Village
{
    /// <summary>
    /// IMGUI(OnGUI)で描画される画面固定のUIパネル(ガチャボタン、カード説明UI等)が、現在
    /// マウスカーソルの直下にあるかどうかを一元管理する。CardBoardのようにワールド空間の
    /// クリック判定(Mouse.current)を直接読むシステムは、IMGUIのボタンクリックを検知できず
    /// (Event.Useで消費してもMouse.currentの状態には影響しないため)、UIパネルの手前をクリック
    /// しても同時にワールド側のクリックとして誤処理してしまう。これを避けるため、各UIパネルは
    /// 自身の描画矩形をOnGUI内で毎回登録し、ワールド側のクリック処理はその上をクリックしていないか
    /// ここで確認してからTryBeginDrag等を行う。
    /// </summary>
    public static class ScreenUIPanelRegistry
    {
        private static readonly Dictionary<object, Rect> ActivePanelRects = new();

        /// <summary>
        /// パネルの現在の矩形(GUI座標系: 左上原点、Y下向き)を登録する。非表示になったらnullを渡して解除する。
        /// </summary>
        public static void SetPanelRect(object owner, Rect? rect)
        {
            if (rect.HasValue)
            {
                ActivePanelRects[owner] = rect.Value;
            }
            else
            {
                ActivePanelRects.Remove(owner);
            }
        }

        /// <summary>
        /// 指定した画面座標(Input Systemのmouse.position形式: 左下原点、Y上向き)が、
        /// 登録されているいずれかのUIパネルの上にあるかどうか。
        /// </summary>
        public static bool IsScreenPointOverPanel(Vector2 inputSystemScreenPoint)
        {
            Vector2 guiPoint = new(inputSystemScreenPoint.x, Screen.height - inputSystemScreenPoint.y);
            foreach (Rect rect in ActivePanelRects.Values)
            {
                if (rect.Contains(guiPoint))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
