using System;
using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Cards
{
    /// <summary>
    /// カードの合成待ち時間の進行を管理し、カードの上にバー型のプログレスバーを表示する。
    /// CardStackと同じGameObjectに付与される。
    /// 完了した瞬間にonCompletedを呼んでからバー・自分自身を破棄する
    /// (実際のカード消去・結果カード生成はCardBoard側の責務)。
    /// </summary>
    [RequireComponent(typeof(CardStack))]
    public class CardCraftProcess : MonoBehaviour
    {
        private const float BarWidth = 1f;
        private static readonly Vector3 BarLocalPosition = new Vector3(0f, 0.75f, 0f);
        private const int BarSortingOrder = 5000;

        private float elapsed;
        private GameObject barRoot;
        private Transform fillTransform;
        private Action<CardCraftProcess> onCompleted;

        public CardStack Stack { get; private set; }
        public CardRecipeData Recipe { get; private set; }
        public IReadOnlyList<CardController> Ingredients { get; private set; }
        public bool IsComplete { get; private set; }

        /// <summary>
        /// trueの間、進行中の合成タイマーは一切進まない(食料不足で月齢の進行がブロックされている間、
        /// 村の生産活動も連動して止めるための全体スイッチ。MoonCycleManagerが切り替える)。
        /// ただし一部のレシピ(IsPauseExemptRecipe参照)は例外的に進行を続ける。
        /// </summary>
        public static bool ProductionPaused { get; set; }

        public void Begin(CardRecipeData recipeData, IReadOnlyList<CardController> ingredients,
            Sprite backgroundSprite, Sprite fillSprite, Action<CardCraftProcess> onCompletedCallback)
        {
            Stack = GetComponent<CardStack>();
            Recipe = recipeData;
            Ingredients = ingredients;
            onCompleted = onCompletedCallback;
            elapsed = 0f;
            IsComplete = false;

            barRoot = new GameObject("CraftProgressBar");
            barRoot.transform.SetParent(transform, false);
            barRoot.transform.localPosition = BarLocalPosition;

            var background = new GameObject("Background");
            background.transform.SetParent(barRoot.transform, false);
            SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = backgroundSprite;
            backgroundRenderer.sortingOrder = BarSortingOrder;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(barRoot.transform, false);
            fill.transform.localPosition = new Vector3(-BarWidth * 0.5f, 0f, 0f);
            fill.transform.localScale = new Vector3(0f, 1f, 1f);
            SpriteRenderer fillRenderer = fill.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = fillSprite;
            fillRenderer.sortingOrder = BarSortingOrder + 1;

            fillTransform = fill.transform;
        }

        private void Update()
        {
            if (IsComplete)
            {
                return;
            }

            if (ProductionPaused && !IsPauseExemptRecipe(Recipe))
            {
                return;
            }

            elapsed += Time.deltaTime;
            float duration = Mathf.Max(Recipe.CraftDuration, 0.0001f);
            float progress = elapsed / duration;

            if (progress >= 1f)
            {
                IsComplete = true;
                SetProgress(1f);
                onCompleted?.Invoke(this);
                RemoveVisualsAndSelf();
                return;
            }

            SetProgress(progress);
        }

        /// <summary>
        /// 生産停止中でも例外的に進行を続けてよいレシピかどうか。
        /// 食料ストックを増やすレシピ(FoodStockAmount/UseIngredientFoodSupplyValue)は、食料不足を
        /// 自力で解消する手段を残すために必須の例外。経験値付与レシピ(ExpGrantAmount、マナクリスタルの
        /// スライムへの給餌)は「施設の生産」ではなくスライムの世話に近い行為であり、まとめ給餌
        /// (CardBoard.FeedQueueSequentially)が停止中に1枚だけ合体した状態で止まってしまうと
        /// 見た目が不自然になるため、同様に例外とする。
        /// </summary>
        private static bool IsPauseExemptRecipe(CardRecipeData recipe)
        {
            return recipe != null &&
                (recipe.FoodStockAmount > 0 || recipe.UseIngredientFoodSupplyValue || recipe.ExpGrantAmount > 0);
        }

        private void SetProgress(float progress01)
        {
            if (fillTransform == null)
            {
                return;
            }

            Vector3 scale = fillTransform.localScale;
            scale.x = Mathf.Clamp01(progress01);
            fillTransform.localScale = scale;
        }

        /// <summary>進行中の合成を中断し、進行度を破棄する。</summary>
        public void Cancel()
        {
            RemoveVisualsAndSelf();
        }

        /// <summary>
        /// スタックを丸ごと持ち上げて移動した場合に、進行中の状態(経過時間・素材・レシピ・
        /// プログレスバー)をそのまま新しいCardStackへ引き継ぐ。自分自身は破棄される。
        /// </summary>
        public void TransferTo(CardStack newStack)
        {
            CardCraftProcess newProcess = newStack.gameObject.AddComponent<CardCraftProcess>();
            newProcess.Stack = newStack;
            newProcess.Recipe = Recipe;
            newProcess.Ingredients = Ingredients;
            newProcess.onCompleted = onCompleted;
            newProcess.elapsed = elapsed;
            newProcess.IsComplete = IsComplete;

            barRoot.transform.SetParent(newStack.transform, false);
            newProcess.barRoot = barRoot;
            newProcess.fillTransform = fillTransform;

            barRoot = null;
            Destroy(this);
        }

        private void RemoveVisualsAndSelf()
        {
            if (barRoot != null)
            {
                Destroy(barRoot);
                barRoot = null;
            }

            Destroy(this);
        }
    }
}
