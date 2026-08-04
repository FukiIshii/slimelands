using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Slimelands.Slime;
using Slimelands.UI;
using Slimelands.Village;

namespace Slimelands.Cards
{
    /// <summary>
    /// 場のカードのドラッグ&ドロップ入力(新Input System)を一元管理する。
    /// クリックした位置のカードより上に乗っている全カードをまとめて持ち上げて移動でき、
    /// ドロップ先に別のスタックがあれば常にその上に重ねる。1枚だけをレシピが一致する相手に
    /// 重ねた場合はCardCraftProcessによる合成タイマーが開始し、完了すると材料2枚が消えて
    /// 結果カードが出現する。
    /// </summary>
    public class CardBoard : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CardRecipeBook recipeBook;
        [SerializeField] private CardController cardPrefab;
        [SerializeField] private Transform cardParent;
        [SerializeField] private Sprite craftProgressBackgroundSprite;
        [SerializeField] private Sprite craftProgressFillSprite;
        [Tooltip("経験値付与レシピ(ExpGrantAmount)をまとめ給餌したときに表示するポップアップの色。")]
        [SerializeField] private Color expPopupColor = new(1f, 0.85f, 0.3f);
        [Tooltip("foodStockAmountを持つレシピが完了したときに加算する食料ストック。")]
        [SerializeField] private FoodStockManager foodStockManager;
        [Tooltip("スライムカードが生成/消費されるたびに、対応する個体(SlimeIndividual)を登録/除去する村の名簿。")]
        [SerializeField] private VillageSlimeRoster villageSlimeRoster;

        [Header("配置可能範囲")]
        [Tooltip("カードを置ける範囲の左下(最小)座標。")]
        [SerializeField] private Vector2 placementAreaMin = new Vector2(-8f, -5f);
        [Tooltip("カードを置ける範囲の右上(最大)座標。")]
        [SerializeField] private Vector2 placementAreaMax = new Vector2(8f, 5f);
        [Tooltip("配置可能範囲を示す枠線の色。")]
        [SerializeField] private Color placementAreaOutlineColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        [Tooltip("配置可能範囲を示す枠線の太さ。")]
        [SerializeField] private float placementAreaOutlineWidth = 0.05f;

        [Tooltip("ドラッグ中のカードの面積のうち、この割合(0〜1)以上が対象カードと重なっていればスタックとして合体する。")]
        [SerializeField] private float stackOverlapThreshold = 0.1f;

        [Header("結果カードの配置")]
        [Tooltip("完成位置からこの範囲内に同じ種類の結果カードのスタックがあれば、そこへ合流する。")]
        [SerializeField] private float resultMergeSearchRadius = 3f;
        [Tooltip("合流先が見つからなかった場合に飛ばす距離の範囲(最小)。")]
        [SerializeField] private float resultScatterMinDistance = 1.5f;
        [Tooltip("合流先が見つからなかった場合に飛ばす距離の範囲(最大)。")]
        [SerializeField] private float resultScatterMaxDistance = 3f;
        [Tooltip("他のカードと重ならない着地位置を探す再試行回数。")]
        [SerializeField] private int resultScatterMaxAttempts = 5;
        [Tooltip("結果カードが飛んでいくアニメーションの所要時間(秒)。")]
        [SerializeField] private float resultFlyDuration = 0.35f;
        [Tooltip("結果カードが飛んでいくアニメーションの弧の高さ。")]
        [SerializeField] private float resultFlyArcHeight = 0.4f;

        [Tooltip("ドロップしたカードが他のカードと重なっている場合や配置可能範囲の外にある場合に、有効な位置まで滑る時間(秒)。")]
        [SerializeField] private float snapSlideDuration = 0.15f;

        // 重なり判定のボックスをカード実寸よりわずかに小さくし、隣接カードが辺で触れているだけの
        // 場合まで「重なっている」と誤判定しないようにする。
        private const float OverlapCheckShrink = 0.9f;

        private CardStack draggedStack;
        private int nextSortingOrder;
        private Vector2 cardFootprintSize = Vector2.one;

        /// <summary>
        /// カードをクリック(ドラッグ開始のプレス)したときに、そのカードのデータを通知する。
        /// 何もない場所をクリックした場合はnullを通知する(説明表示UI側の選択解除に使う)。
        /// </summary>
        public event System.Action<CardController> OnCardSelected;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            // シーンに最初から置かれているカードの描画順は、このカウンター(Playのたびに0から始まる)とは
            // 無関係な値を持っている可能性があるため、既存の最大値より後ろから始めて逆転を防ぐ。
            nextSortingOrder = FindHighestExistingSortingOrder() + 1;

            if (cardPrefab != null && cardPrefab.TryGetComponent(out BoxCollider2D prefabCollider))
            {
                cardFootprintSize = prefabCollider.size;
            }

            CreatePlacementAreaOutline();
        }

        // 「必要人数0で常時稼働」の生産(例: マナだまり)はプレイヤーの操作を必要としないため、
        // シーン開始時点で既にレシピと一致しているスタックがあれば、ここで自動的に合成を開始する
        // (ドラッグ&ドロップで一度触るまで動き出さない、という不自然な挙動を避ける)。
        private void Start()
        {
            BindIndividualsToPreplacedSlimeCards();

            foreach (CardStack stack in FindObjectsByType<CardStack>(FindObjectsInactive.Exclude))
            {
                TryStartCraftIfMatches(stack);
            }
        }

        /// <summary>
        /// エディタ上でシーンに直接配置されたスライムカードはCreateLooseCardを経由しないため、
        /// 起動時点ではBoundIndividualを持たない(レベル・ステータス・必殺技などの個体データが
        /// 存在しない)。実行時に生成されたスライムと同様に扱えるよう、起動時に個体を生成して
        /// 割り当てる。
        /// </summary>
        private void BindIndividualsToPreplacedSlimeCards()
        {
            foreach (CardController card in FindObjectsByType<CardController>(FindObjectsInactive.Exclude))
            {
                if (card.Data is SlimeCardData slimeCardData && slimeCardData.Species != null && card.BoundIndividual == null)
                {
                    var individual = new SlimeIndividual(slimeCardData.Species);
                    card.BindSlimeIndividual(individual);
                    villageSlimeRoster?.AddSlime(individual);
                }
            }
        }

        /// <summary>配置可能範囲(placementAreaMin〜placementAreaMax)を示す枠線をLineRendererで生成する。ゲーム内(Game view)でも常に見える。</summary>
        private void CreatePlacementAreaOutline()
        {
            var outlineObject = new GameObject("PlacementAreaOutline");
            outlineObject.transform.SetParent(transform, false);

            var lineRenderer = outlineObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
            lineRenderer.positionCount = 4;
            lineRenderer.SetPosition(0, new Vector3(placementAreaMin.x, placementAreaMin.y, 0f));
            lineRenderer.SetPosition(1, new Vector3(placementAreaMax.x, placementAreaMin.y, 0f));
            lineRenderer.SetPosition(2, new Vector3(placementAreaMax.x, placementAreaMax.y, 0f));
            lineRenderer.SetPosition(3, new Vector3(placementAreaMin.x, placementAreaMax.y, 0f));
            lineRenderer.startWidth = placementAreaOutlineWidth;
            lineRenderer.endWidth = placementAreaOutlineWidth;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = placementAreaOutlineColor;
            lineRenderer.endColor = placementAreaOutlineColor;
            lineRenderer.sortingOrder = -1000;
            lineRenderer.textureMode = LineTextureMode.Tile;
        }

        /// <summary>Playしていない間もScene view上で配置可能範囲を確認できるようにする(Editor専用)。</summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = placementAreaOutlineColor;
            Vector3 center = new Vector3((placementAreaMin.x + placementAreaMax.x) * 0.5f, (placementAreaMin.y + placementAreaMax.y) * 0.5f, 0f);
            Vector3 size = new Vector3(placementAreaMax.x - placementAreaMin.x, placementAreaMax.y - placementAreaMin.y, 0f);
            Gizmos.DrawWireCube(center, size);
        }

        private static int FindHighestExistingSortingOrder()
        {
            int highest = 0;
            foreach (CardController card in FindObjectsByType<CardController>(FindObjectsInactive.Exclude))
            {
                if (card.SpriteRenderer.sortingOrder > highest)
                {
                    highest = card.SpriteRenderer.sortingOrder;
                }
            }

            return highest;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // IMGUIの画面固定パネル(ガチャボタン、カード説明UI等)の上をクリックした場合は、
                // ワールド側のカード選択・ドラッグ開始として処理しない(ボタン操作がそのまま
                // 盤面のクリックとして誤爆し、選択中のカードが解除されてしまうのを防ぐ)。
                if (!ScreenUIPanelRegistry.IsScreenPointOverPanel(mouse.position.ReadValue()))
                {
                    TryBeginDrag(GetPointerWorldPosition(mouse));
                }
            }
            else if (draggedStack != null && mouse.leftButton.wasReleasedThisFrame)
            {
                EndDrag(GetPointerWorldPosition(mouse));
            }
            else if (draggedStack != null && mouse.leftButton.isPressed)
            {
                draggedStack.UpdateDrag(GetPointerWorldPosition(mouse));
            }
        }

        private Vector3 GetPointerWorldPosition(Mouse mouse)
        {
            Vector3 screenPosition = mouse.position.ReadValue();
            screenPosition.z = Mathf.Abs(targetCamera.transform.position.z);
            Vector3 worldPosition = targetCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = 0f;
            return worldPosition;
        }

        private void TryBeginDrag(Vector3 pointerWorldPosition)
        {
            CardController clickedCard = FindTopCardAt(pointerWorldPosition, null);
            OnCardSelected?.Invoke(clickedCard);

            if (clickedCard == null)
            {
                return;
            }

            CardStack originalStack = clickedCard.ParentStack;
            if (originalStack == null)
            {
                // 結果カードが着地演出中など、まだどのスタックにも属していない場合は無視する。
                return;
            }

            int index = originalStack.IndexOf(clickedCard);
            draggedStack = originalStack.RemoveFrom(index);
            AssignAscendingSortingOrders(draggedStack.Cards);
            draggedStack.BeginDrag(pointerWorldPosition);
        }

        private void EndDrag(Vector3 pointerWorldPosition)
        {
            CardStack dropped = draggedStack;
            dropped.UpdateDrag(pointerWorldPosition);
            draggedStack = null;

            // 合成中のスタックはドロップ先にできない(何も乗せられない)。
            // ただし合成中のスタック自身を持ち上げて他所へ動かすことは引き続き可能。
            CardController target = FindStackDropTarget(dropped, skipCraftingStacks: true);
            if (target == null)
            {
                // 十分に重なる相手がいない場合: 持ち上げていたグループはそのまま新しいスタックとして残るが、
                // 配置可能範囲の外にある、または他のカードと見た目上重なっているなら
                // (ドロップした瞬間だけ)有効な位置へ滑らせて補正する。ドラッグ中は範囲外へ自由に動かせる。
                SnapToValidPosition(dropped);

                // 分離してできたグループ自体の中身が、単独でいずれかのレシピと一致するなら
                // 合成タイマーを開始する(例: 何かの上に乗っていた施設+スライムだけを
                // 分離して独立させた場合、そのまま生産が始まる)。1枚だけの合体時とは異なり、
                // 何枚のグループでも判定する。
                TryStartCraftIfMatches(dropped);
                return;
            }

            CardStack targetStack = target.ParentStack;
            CardController droppedTopCard = dropped.Count == 1 ? dropped.PeekTop() : null;

            // 同じ種類のカードがN枚重なったスタックをまとめてドロップした場合、条件が合えば
            // 1枚ずつ順番に合成するバッチ給餌を行う(例: マナクリスタルを重ねてスライムに与える)。
            // 対象外の組み合わせならfalseを返すので、その場合は下の通常の重ね合わせ処理に進む。
            if (droppedTopCard == null && TryBeginBatchFeed(dropped, targetStack))
            {
                return;
            }

            // カードは常にまず物理的に重ねる。
            List<CardController> cardsToMove = dropped.TakeAll();
            AssignAscendingSortingOrders(cardsToMove);
            targetStack.AddRangeToTop(cardsToMove);

            // 重ねた後のスタック全体の内容が、過不足なくいずれかのレシピと一致する場合のみ合成タイマーを開始する。
            // (例: 木材2枚の山にさらに木材を重ねて合計3枚になった場合、素材2枚のレシピとは一致しないため始まらない)
            // 複数枚をまとめて乗せた場合(droppedTopCard==null)は対象外とする。
            if (droppedTopCard != null)
            {
                TryStartCraftIfMatches(targetStack);
            }
        }

        /// <summary>
        /// 同じ種類のカードがN枚重なったスタックを、別のスタックへまとめてドロップした場合に、
        /// 「1枚だけドロップしたときと同じレシピ」がN回連続で成立する状況であれば、1枚ずつ順番に
        /// 合成を実行するバッチ給餌を開始する(例: マナクリスタルを重ねてスライムに与える)。
        /// TryMatch/TryAssign自体には一切手を加えず、既存の判定・CardCraftProcessをそのまま
        /// 繰り返し呼び出すだけで実現する(レシピ側にN枚固定の枠を用意する必要をなくすため)。
        /// 対応しない組み合わせの場合は何もせずfalseを返す(呼び出し側が従来通りの単純な重ね合わせを行う)。
        /// </summary>
        private bool TryBeginBatchFeed(CardStack dropped, CardStack targetStack)
        {
            if (recipeBook == null || dropped == null || dropped.Count <= 1 || targetStack == null)
            {
                return false;
            }

            CardData repeatedData = dropped.Cards[0].Data;
            foreach (CardController card in dropped.Cards)
            {
                if (card.Data != repeatedData)
                {
                    // 重なっているカードの種類が揃っていない場合はバッチ給餌の対象外。
                    return false;
                }
            }

            // 対象スタックの現在の中身+このカード1枚で、いずれかのレシピと一致するか確認する
            // (実際の判定はrecipeBook.TryFindRecipe/recipe.TryMatchをそのまま使う)。
            var probeContents = new List<CardData>(targetStack.Count + 1);
            foreach (CardController card in targetStack.Cards)
            {
                probeContents.Add(card.Data);
            }
            probeContents.Add(repeatedData);

            if (!recipeBook.TryFindRecipe(probeContents, out CardRecipeData recipe) ||
                !recipe.TryMatch(probeContents, out int[] mapping))
            {
                return false;
            }

            // 対象スタック側のカードが1枚でも消費される(isConsumed=true)枠に割り当てられる場合、
            // 1回目の合成で対象自体が失われてしまい2回目以降を繰り返せないため対象外とする
            // (対象側が常に残る=isConsumed=falseの場合のみ、安全に繰り返せる)。
            for (int i = 0; i < targetStack.Count; i++)
            {
                if (recipe.Ingredients[mapping[i]].IsConsumed)
                {
                    return false;
                }
            }

            List<CardController> queue = dropped.TakeAll();
            StartCoroutine(FeedQueueSequentially(targetStack, queue, recipe));
            return true;
        }

        /// <summary>
        /// キューにあるカードを対象スタックへ1枚ずつ乗せてはCardCraftProcessの完了を待つ、を繰り返す。
        /// CardCraftProcessのDestroy()はフレーム末まで反映が遅延されるため、次のカードを乗せる前に
        /// 必ず1フレーム待ってから進める(同一フレーム内で複数のCardCraftProcessが同居し、
        /// GetComponentの参照が曖昧になるのを避けるため)。全て給餌し終えたら、経験値付与レシピの
        /// 場合のみ合計値をまとめてポップアップ表示する。
        /// </summary>
        private IEnumerator FeedQueueSequentially(CardStack targetStack, List<CardController> queue, CardRecipeData recipe)
        {
            int fedCount = 0;

            foreach (CardController card in queue)
            {
                if (targetStack == null)
                {
                    yield break;
                }

                yield return null;
                if (targetStack == null)
                {
                    yield break;
                }

                AssignAscendingSortingOrders(new List<CardController> { card });
                targetStack.AddToTop(card);
                TryStartCraftIfMatches(targetStack);

                CardCraftProcess process = targetStack.GetComponent<CardCraftProcess>();
                if (process == null)
                {
                    // 想定外(TryBeginBatchFeedの事前確認と食い違った)。これ以上は続けない。
                    yield break;
                }

                while (!process.IsComplete)
                {
                    yield return null;
                    if (targetStack == null)
                    {
                        yield break;
                    }
                }

                fedCount++;
            }

            if (fedCount > 0 && recipe.ExpGrantAmount > 0 && targetStack != null)
            {
                int totalExp = recipe.ExpGrantAmount * fedCount;
                FloatingNumberPopup.Spawn(targetStack.transform.position, totalExp, expPopupColor);
            }
        }

        // 「位置が実質的に変わっていない」とみなす許容誤差(距離の2乗)。
        private const float PositionEpsilonSqr = 0.0001f;

        /// <summary>
        /// スタックとして合体しなかったカード群が、配置可能範囲の外にある場合や
        /// 他のカードと見た目上重なっている場合に、有効な位置まで滑らせて補正する
        /// (FindNearestFreePositionが範囲内クランプと重なり回避の両方を担う)。
        /// </summary>
        private void SnapToValidPosition(CardStack stack)
        {
            if (stack == null || stack.Count == 0)
            {
                return;
            }

            Vector3 currentPosition = stack.transform.position;
            Vector3 targetPosition = FindNearestFreePosition(currentPosition, stack);

            if ((targetPosition - currentPosition).sqrMagnitude <= PositionEpsilonSqr)
            {
                return;
            }

            stack.transform.position = targetPosition;
            StartCoroutine(SlideStackTo(stack, currentPosition, targetPosition, snapSlideDuration));
        }

        private IEnumerator SlideStackTo(CardStack stack, Vector3 start, Vector3 end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (stack == null)
                {
                    // ドロップ直後にスタックが再度掴まれる等で消滅していたら、それ以上は何もしない。
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - ((1f - t) * (1f - t));
                stack.transform.position = Vector3.Lerp(start, end, eased);
                yield return null;
            }

            if (stack != null)
            {
                stack.transform.position = end;
            }
        }

        /// <summary>
        /// スタックの中身が、いずれかのレシピと過不足なく一致する場合に合成タイマーを開始する。
        /// 既に合成中のスタックには何もしない。
        /// </summary>
        private void TryStartCraftIfMatches(CardStack candidateStack)
        {
            if (recipeBook == null || candidateStack == null || candidateStack.Count == 0)
            {
                return;
            }

            // 完了直後のCardCraftProcessはDestroy()の反映が次フレーム末まで遅延されるため、
            // IsCompleteなインスタンスは「合成中」とみなさず自動再開を許可する。
            CardCraftProcess existingCraft = candidateStack.GetComponent<CardCraftProcess>();
            if (existingCraft != null && !existingCraft.IsComplete)
            {
                return;
            }

            var stackContents = new List<CardData>(candidateStack.Count);
            foreach (CardController card in candidateStack.Cards)
            {
                stackContents.Add(card.Data);
            }

            if (recipeBook.TryFindRecipe(stackContents, out CardRecipeData recipe))
            {
                CardCraftProcess craft = candidateStack.gameObject.AddComponent<CardCraftProcess>();
                craft.Begin(recipe, new List<CardController>(candidateStack.Cards),
                    craftProgressBackgroundSprite, craftProgressFillSprite, OnCraftCompleted);
            }
        }

        private void OnCraftCompleted(CardCraftProcess craft)
        {
            CardStack stack = craft.Stack;
            CardRecipeData recipe = craft.Recipe;
            IReadOnlyList<CardController> ingredients = craft.Ingredients;

            // 各カードがどの素材枠に割り当てられたかを再計算し、その枠のIsConsumedで消費するかを判定する
            // (isConsumed=falseの枠に割り当てられたカード=施設やスライムはその場に残る)。
            var dataList = new List<CardData>(ingredients.Count);
            foreach (CardController ingredient in ingredients)
            {
                dataList.Add(ingredient.Data);
            }

            recipe.TryMatch(dataList, out int[] cardToIngredientIndex);

            var consumed = new List<CardController>(ingredients.Count);
            for (int i = 0; i < ingredients.Count; i++)
            {
                bool isConsumed = cardToIngredientIndex == null
                    || recipe.Ingredients[cardToIngredientIndex[i]].IsConsumed;
                if (isConsumed)
                {
                    consumed.Add(ingredients[i]);
                }
            }

            Vector3 anchorPosition = stack.transform.position;

            foreach (CardController ingredient in consumed)
            {
                // 配合・進化などでスライムカードごと消費される場合、対応する個体も
                // 村の名簿(VillageSlimeRoster)から取り除く(新しい上位種の個体として
                // 結果カード側で改めて登録されるため、消費元は残さない)。
                if (ingredient.BoundIndividual != null)
                {
                    villageSlimeRoster?.RemoveSlime(ingredient.BoundIndividual);
                }

                stack.Remove(ingredient);
            }

            foreach (CardController ingredient in consumed)
            {
                Destroy(ingredient.gameObject);
            }

            if (recipe.FoodStockAmount > 0)
            {
                // 結果カードを生成する代わりに食料ストックへ加算するレシピ(例: アイテム+納屋)。
                if (foodStockManager != null)
                {
                    foodStockManager.AddFood(recipe.FoodStockAmount);
                }
            }
            else if (recipe.ExpGrantAmount > 0)
            {
                // 結果カードを生成する代わりに、消費されず残ったスライムの個体へ経験値を加算する
                // レシピ(例: マナクリスタル+スライム→経験値付与)。isConsumed=falseの素材枠に
                // 割り当てられたスライムカードが対象(=盤面に残っている、上のconsumed処理で
                // 破棄されていないカード)。
                foreach (CardController ingredient in ingredients)
                {
                    if (consumed.Contains(ingredient) || ingredient.BoundIndividual == null)
                    {
                        continue;
                    }

                    ingredient.BoundIndividual.AddExp(recipe.ExpGrantAmount);
                }

                // まとめ給餌(FeedQueueSequentially)経由と表示を揃えるため、1枚ずつの給餌でも
                // 経験値量をポップアップ表示する。
                FloatingNumberPopup.Spawn(anchorPosition, recipe.ExpGrantAmount, expPopupColor);
            }
            else
            {
                for (int i = 0; i < recipe.ResultCount; i++)
                {
                    CardController resultCard = CreateLooseCard(recipe.Result);
                    resultCard.transform.position = anchorPosition;
                    resultCard.SetSortingOrder(NextSortingOrder());
                    PlaceResultCard(resultCard, anchorPosition, recipe.Result);
                }
            }

            // 素材が消費されず(または一部が残り)スタックがまだ存在する場合、その中身が引き続き
            // 何らかのレシピと一致するなら、進行バーがリセットされた新しい合成を自動的に開始する
            // (施設+スライムのような「消費されない生産レシピ」が繰り返しループする)。
            if (stack != null && stack.Count > 0)
            {
                TryStartCraftIfMatches(stack);
            }
        }

        /// <summary>
        /// 完成した結果カード1枚を配置する。
        /// 完成位置の近くに同じ種類の結果カードのスタックがあればそこへ飛んで合流し、
        /// なければ近くのランダムな位置へ飛ばして新しいスタックとして着地させる。
        /// </summary>
        private void PlaceResultCard(CardController resultCard, Vector3 originPosition, CardData resultData)
        {
            // 着地までの間、プレイヤーが飛行中のカードを掴めないようコライダーを一時的に無効化する。
            Collider2D collider = resultCard.GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            CardStack mergeTarget = FindNearbyStackOfType(originPosition, resultData);
            if (mergeTarget != null)
            {
                StartCoroutine(FlyThenMergeIntoStack(resultCard, originPosition, mergeTarget.transform.position, mergeTarget));
            }
            else
            {
                Vector3 landingPosition = FindScatterLandingPosition(originPosition);
                StartCoroutine(FlyThenCreateStack(resultCard, originPosition, landingPosition));
            }
        }

        /// <summary>完成位置から一定範囲内にある、全カードがresultDataと同じ種類のスタックを探す(合成中のものは除外)。</summary>
        private CardStack FindNearbyStackOfType(Vector3 originPosition, CardData resultData)
        {
            CardStack best = null;
            float bestDistanceSqr = resultMergeSearchRadius * resultMergeSearchRadius;

            foreach (CardStack candidate in FindObjectsByType<CardStack>(FindObjectsInactive.Exclude))
            {
                if (candidate.Count == 0 || candidate.GetComponent<CardCraftProcess>() != null)
                {
                    continue;
                }

                if (!IsStackEntirelyOfType(candidate, resultData))
                {
                    continue;
                }

                float distanceSqr = (candidate.transform.position - originPosition).sqrMagnitude;
                if (distanceSqr <= bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    best = candidate;
                }
            }

            return best;
        }

        private static bool IsStackEntirelyOfType(CardStack stack, CardData data)
        {
            foreach (CardController card in stack.Cards)
            {
                if (card.Data != data)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>元の位置からランダムな方向・距離の着地点を探す。他のカードと重なる場合は再試行し、それでも駄目なら最も近い空き位置にフォールバックする。</summary>
        private Vector3 FindScatterLandingPosition(Vector3 originPosition)
        {
            for (int attempt = 0; attempt < resultScatterMaxAttempts; attempt++)
            {
                Vector3 candidate = RandomOffsetPosition(originPosition);
                if (!IsPositionOccupied(candidate, null))
                {
                    return candidate;
                }
            }

            return FindNearestFreePosition(originPosition, null);
        }

        private Vector3 RandomOffsetPosition(Vector3 originPosition)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(resultScatterMinDistance, resultScatterMaxDistance);
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;
            return ClampToBoard(originPosition + offset);
        }

        /// <summary>
        /// カード全体(実寸)が配置可能範囲に収まるよう、中心座標を範囲の内側にクランプする。
        /// 範囲がカード1枚分より狭い場合は範囲の中心に寄せる。
        /// </summary>
        private Vector3 ClampToBoard(Vector3 position)
        {
            float halfWidth = cardFootprintSize.x * 0.5f;
            float halfHeight = cardFootprintSize.y * 0.5f;

            float minX = placementAreaMin.x + halfWidth;
            float maxX = placementAreaMax.x - halfWidth;
            float minY = placementAreaMin.y + halfHeight;
            float maxY = placementAreaMax.y - halfHeight;

            float x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : (placementAreaMin.x + placementAreaMax.x) * 0.5f;
            float y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : (placementAreaMin.y + placementAreaMax.y) * 0.5f;
            return new Vector3(x, y, position.z);
        }

        /// <summary>
        /// カード1枚分の実寸ボックスで判定するため、中心点が偶然空いているだけで
        /// 実際には縁が重なっている、というケースも「重なっている」として検出できる。
        /// excludeStackを指定すると、そのスタック自身のカードとの重なりは無視する。
        /// </summary>
        private bool IsPositionOccupied(Vector3 position, CardStack excludeStack)
        {
            Collider2D[] hits = Physics2D.OverlapBoxAll(position, cardFootprintSize * OverlapCheckShrink, 0f);
            foreach (Collider2D hit in hits)
            {
                CardController card = hit.GetComponentInParent<CardController>();
                if (card == null)
                {
                    continue;
                }

                if (excludeStack != null && card.ParentStack == excludeStack)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>元の位置を中心に同心円状に探索し、最も近い空き位置を返す(見つからなければ元の位置)。候補は常に配置可能範囲内にクランプする。</summary>
        private Vector3 FindNearestFreePosition(Vector3 originPosition, CardStack excludeStack)
        {
            Vector3 clampedOrigin = ClampToBoard(originPosition);
            if (!IsPositionOccupied(clampedOrigin, excludeStack))
            {
                return clampedOrigin;
            }

            const float ringStep = 0.3f;
            const int maxRings = 20;
            const int samplesPerRing = 8;

            for (int ring = 1; ring <= maxRings; ring++)
            {
                float radius = ringStep * ring;
                for (int s = 0; s < samplesPerRing; s++)
                {
                    float angle = (Mathf.PI * 2f / samplesPerRing) * s;
                    Vector3 candidate = ClampToBoard(originPosition + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
                    if (!IsPositionOccupied(candidate, excludeStack))
                    {
                        return candidate;
                    }
                }
            }

            return clampedOrigin;
        }

        private IEnumerator FlyThenMergeIntoStack(CardController card, Vector3 start, Vector3 end, CardStack targetStack)
        {
            yield return AnimateFlyPosition(card.transform, start, end);
            SetColliderEnabled(card, true);

            if (targetStack == null)
            {
                // 飛行中に合流先のスタックが消滅していた場合(合成消費・取得など)は、
                // 代わりにその場で新しいスタックとして着地させる。
                CardStack fallbackStack = CardStack.Create(cardParent, end);
                fallbackStack.AddToTop(card);
                yield break;
            }

            targetStack.AddToTop(card);
        }

        private IEnumerator FlyThenCreateStack(CardController card, Vector3 start, Vector3 end)
        {
            yield return AnimateFlyPosition(card.transform, start, end);
            SetColliderEnabled(card, true);
            CardStack newStack = CardStack.Create(cardParent, end);
            newStack.AddToTop(card);
        }

        private static void SetColliderEnabled(CardController card, bool enabled)
        {
            Collider2D collider = card.GetComponent<Collider2D>();
            if (collider != null)
            {
                collider.enabled = enabled;
            }
        }

        /// <summary>簡易な放物線(ポップ)移動: 直線移動に正弦カーブで上下の弧を加える。</summary>
        private IEnumerator AnimateFlyPosition(Transform cardTransform, Vector3 start, Vector3 end)
        {
            float elapsed = 0f;
            while (elapsed < resultFlyDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / resultFlyDuration);
                float eased = 1f - ((1f - t) * (1f - t));
                Vector3 flatPosition = Vector3.Lerp(start, end, eased);
                float arc = Mathf.Sin(t * Mathf.PI) * resultFlyArcHeight;
                cardTransform.position = flatPosition + new Vector3(0f, arc, 0f);
                yield return null;
            }

            cardTransform.position = end;
        }

        private void AssignAscendingSortingOrders(IReadOnlyList<CardController> cardsBottomToTop)
        {
            foreach (CardController card in cardsBottomToTop)
            {
                card.SetSortingOrder(NextSortingOrder());
            }
        }

        /// <summary>
        /// ドロップされたスタック(の一番下のカード=着地面)が、既存スタックの一番上のカードと
        /// stackOverlapThreshold以上の面積で重なっているかを基準に、スタック先を探す。
        /// 単なるカーソル位置(点)ではなく実際の見た目の重なりで判定するため、
        /// わずかに触れただけでは合体しない。
        /// </summary>
        private CardController FindStackDropTarget(CardStack dropped, bool skipCraftingStacks)
        {
            if (dropped.Count == 0)
            {
                return null;
            }

            Collider2D droppedCollider = dropped.Cards[0].GetComponent<Collider2D>();
            if (droppedCollider == null)
            {
                return null;
            }

            CardController best = null;
            float bestOverlap = stackOverlapThreshold;

            foreach (CardStack candidate in FindObjectsByType<CardStack>(FindObjectsInactive.Exclude))
            {
                if (candidate == dropped || candidate.Count == 0)
                {
                    continue;
                }

                if (skipCraftingStacks && candidate.GetComponent<CardCraftProcess>() != null)
                {
                    continue;
                }

                CardController top = candidate.PeekTop();
                Collider2D targetCollider = top != null ? top.GetComponent<Collider2D>() : null;
                if (targetCollider == null)
                {
                    continue;
                }

                float overlap = ComputeOverlapRatio(droppedCollider, targetCollider);
                if (overlap >= bestOverlap)
                {
                    bestOverlap = overlap;
                    best = top;
                }
            }

            return best;
        }

        /// <summary>droppedの面積のうち、targetと重なっている割合(0〜1)を返す。</summary>
        private static float ComputeOverlapRatio(Collider2D dropped, Collider2D target)
        {
            Bounds a = dropped.bounds;
            Bounds b = target.bounds;

            float overlapWidth = Mathf.Max(0f, Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x));
            float overlapHeight = Mathf.Max(0f, Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y));
            float overlapArea = overlapWidth * overlapHeight;

            float droppedArea = a.size.x * a.size.y;
            return droppedArea > 0f ? overlapArea / droppedArea : 0f;
        }

        private CardController FindTopCardAt(Vector3 worldPosition, Transform excludeRoot)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
            CardController top = null;
            int topOrder = int.MinValue;

            foreach (Collider2D hit in hits)
            {
                CardController card = hit.GetComponentInParent<CardController>();
                if (card == null)
                {
                    continue;
                }

                if (excludeRoot != null && card.transform.IsChildOf(excludeRoot))
                {
                    continue;
                }

                int order = card.SpriteRenderer.sortingOrder;
                if (order > topOrder)
                {
                    topOrder = order;
                    top = card;
                }
            }

            return top;
        }

        public CardStack SpawnStack(CardData data, Vector3 worldPosition)
        {
            CardController card = CreateLooseCard(data);
            card.SetSortingOrder(NextSortingOrder());
            CardStack stack = CardStack.Create(cardParent, worldPosition);
            stack.AddToTop(card);
            return stack;
        }

        /// <summary>
        /// 盤面上から指定カードをamount枚探して取り除く(ガチャのコスト消費などに使う)。
        /// amount枚に満たない場合は何も変更せずfalseを返す(全額支払えない限り消費しない)。
        /// </summary>
        public bool TryConsumeCards(CardData data, int amount)
        {
            if (data == null || amount <= 0)
            {
                return true;
            }

            var matches = new List<CardController>();
            foreach (CardController card in FindObjectsByType<CardController>(FindObjectsInactive.Exclude))
            {
                // ParentStack == nullは合成完了直後などでRemove済み(Destroy()はフレーム末まで遅延する)の
                // カードを指すため、二重に消費対象としてマッチしないよう除外する。
                if (card.Data == data && card.ParentStack != null)
                {
                    matches.Add(card);
                    if (matches.Count >= amount)
                    {
                        break;
                    }
                }
            }

            if (matches.Count < amount)
            {
                return false;
            }

            foreach (CardController card in matches)
            {
                card.ParentStack?.Remove(card);
                Destroy(card.gameObject);
            }

            return true;
        }

        private CardController CreateLooseCard(CardData data)
        {
            CardController card = Instantiate(cardPrefab, Vector3.zero, Quaternion.identity, cardParent);
            card.Initialize(data);

            if (data is SlimeCardData slimeCardData && slimeCardData.Species != null)
            {
                // SlimeCardDataは種族を指すだけの共有アセットなので、カードが生成されるたびに
                // 個体差(SlimeIndividual)を新規に作り、村の名簿(VillageSlimeRoster)へ登録する。
                // ガチャ・合成・配合・進化、いずれの経路でもこのメソッドを通るため一箇所で完結する。
                // 必殺技は種族のUltimateSkillCandidatesからのランダム抽選・固定含め、
                // SlimeIndividualのコンストラクタ内で完結する(GDD確定仕様)。
                var individual = new SlimeIndividual(slimeCardData.Species);

                card.BindSlimeIndividual(individual);
                villageSlimeRoster?.AddSlime(individual);
            }

            return card;
        }

        private int NextSortingOrder()
        {
            return nextSortingOrder++;
        }
    }
}
