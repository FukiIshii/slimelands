# 進捗レポート: ガチャ(召喚)システム

作成日: 2026-07-31

## 実装した内容

### 1. 排出重み(SlimeSpeciesData)
- `SlimeSpeciesData` に `gachaWeight`(int)を追加。値が大きいほど排出されやすく、0以下は排出対象外。
- 仮の値として `Slime`(コモン)=100、`FireSlime`(レア)=20 を設定(依頼文中の例に合わせた相対値)。
- `docs/GDD.md` の「今後詰める論点」には元々ガチャの排出率バランスは含まれていないが、CLAUDE.mdルール2「バランス値はハードコードしない」に従い、コード内定数ではなくScriptableObjectのInspector値として実装した。

### 2. 排出対象一覧(GachaPoolData)
- `Assets/Scripts/Village/GachaPoolData.cs`: どのスライムカードが現在ガチャの対象かを保持するScriptableObject(`List<SlimeCardData> entries`)。
- `Assets/Data/Cards/GachaPool.asset` に既存の2種(スライム / ファイヤースライム)を登録。
- 「全てのSlimeSpeciesDataが対象になる」という要件に対し、種族を自動スキャンする方式(Resources.LoadAll等)ではなく、既存の `CardRecipeBook` と同様の「データアセットにリストとして持たせる」設計を採用した。新しいスライム種族カードを追加した際は、このリストへの追加が必要(下記「要確認」参照)。

### 3. 抽選ロジックとカード生成(GachaManager)
- `Assets/Scripts/Village/GachaManager.cs`
  - `TryDrawGacha()`: コスト消費 → 重み付き抽選 → `CardBoard.SpawnStack` でスライムカードを盤面に生成、の順で実行する公開メソッド。
  - `PickWeighted()`: 累積重み方式のシンプルな重み付き抽選(`entries`の合計重みの範囲で乱数を引き、区間判定)。
  - `OnGachaDrawn` / `OnGachaFailed` イベントでUI側に結果を通知。
- コスト消費は「盤面上に実際に置かれたカードを取り除く」方式を採用(既存のカード経済と一貫させるため、抽象的な通貨カウンターは追加していない)。そのため `CardBoard` に `TryConsumeCards(CardData, int)` を新設した。
- 消費対象として「マナ鉱石」資源カード(`Assets/Data/Cards/Resource/ManaOre.asset`)を新規作成し、仮のコストとして「マナ鉱石10枚」を設定。

### 4. UIボタン
- `Assets/Scripts/Village/GachaUI.cs`: IMGUIによる最小限のUI(ボタン1つ+直近結果のラベル)。
- `VillageManagers` GameObjectに `GachaManager` / `GachaUI` を追加し、既存の `FoodStockManager` 等と結線。

## テストした内容(いずれも公開APIをUnity RunCommand経由でPlay Mode中に実行して検証)

- **重み付き抽選の分布**: コストを一時的に無償にして200回抽選 → スライム166枚:ファイヤースライム34枚(期待比5:1に対し実測比約4.9:1、乱数のばらつきとして妥当)。
- **コスト消費**: マナ鉱石12枚を盤面に配置した状態で
  - 1回目の抽選: 成功し、マナ鉱石が10枚消費されて2枚残る(期待通り)。
  - 2回目の抽選: 残り2枚 < コスト10枚のため、`TryDrawGacha()` が `false` を返し、カードも消費されない(期待通り)。
- **バグ発見・修正**: 上記コスト消費テストの過程で、`CardBoard.TryConsumeCards` が「`Destroy()` はフレーム末まで実際の破棄が遅延する」というUnityの挙動により、同一フレーム内で複数回呼び出すと既に取り除いたカード(破棄待ちの残骸)を再度マッチしてしまう不具合を発見した。`ParentStack == null`(=既にRemove済み)のカードを候補から除外するよう修正し、再テストで正しい挙動を確認した。
- コンパイルエラー・警告なしを確認(Unity Console)。
- **未実施**: 実際のマウス操作によるボタンクリック(このツール環境ではマウス入力のシミュレーションができないため)。ロジック(`TryDrawGacha`)自体は上記の通り公開API経由で動作確認済みだが、`GachaUI` のボタンクリック→呼び出しの実地確認はユーザー側でのPlay Mode手動テストが必要。

## 後で確認が必要な点(仮の値で実装したもの)

1. **ガチャの排出重みの具体的なバランス**: コモン=100/レア=20 は依頼文中の例をそのまま採用した仮値。実際のゲームバランスとしての調整は未検討。
2. **ガチャのコスト方式そのもの**: 依頼文には「マナ鉱石を10消費」とだけ指定があり、消費方法(盤面の物理カードを取り除く方式か、独立した資源ストック値を減らす方式か)は明記されていなかった。今回は既存のカード経済(Stacklands的に資源は全て物理カードとして存在する)との一貫性を優先し、盤面上のカードを直接取り除く方式で実装した。将来的に「マナ鉱石ストック」のような抽象値を導入する場合は設計変更が必要になる。
3. **マナ鉱石の入手方法**: 今回はテスト用にRunCommandで盤面に直接配置しただけで、マナ鉱石を生成する採掘レシピ等は未実装。
4. **ガチャ対象の管理方法**: 新しいスライム種族(カード)を追加する際、`GachaPool.asset` への手動登録が必要(自動では追加されない)。将来的に種族追加を自動反映したい場合は別途エディタ拡張等の検討が必要。
5. **UIの見た目**: IMGUIによる最小限の実装。本番の見た目・配置は未着手。

## 変更したファイル

- 新規: `Assets/Scripts/Village/GachaPoolData.cs`, `GachaManager.cs`, `GachaUI.cs`
- 新規データ: `Assets/Data/Cards/GachaPool.asset`, `Assets/Data/Cards/Resource/ManaOre.asset`
- 変更: `Assets/Scripts/Slime/SlimeSpeciesData.cs`(gachaWeight追加)、`Assets/Scripts/Cards/CardBoard.cs`(TryConsumeCards追加)
- シーン: `VillageManagers` に `GachaManager` / `GachaUI` を追加・結線

## コミット

1. `feat: ガチャの抽選ロジックとスライムカード生成を実装`
2. `feat: ガチャを引くための最小限のUIボタンを追加`
