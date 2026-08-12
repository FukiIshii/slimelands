using System.Collections.Generic;
using UnityEngine;

namespace Slimelands.Slime
{
    /// <summary>
    /// スライムのスキル1種を表すマスターデータ。
    /// 必殺技(isUltimate)は、ダンジョンパートでプレイヤーが発動タイミングを操作する対象。
    /// </summary>
    [CreateAssetMenu(fileName = "NewSlimeSkill", menuName = "Slimelands/Slime Skill", order = 20)]
    public class SlimeSkillData : ScriptableObject
    {
        [SerializeField] private string skillName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private bool isUltimate;
        [SerializeField] private int power;
        [Tooltip("isUltimate=trueの場合のみ意味を持つ。発動後、次に発動可能になるまでのクールタイム(秒)。仮値、要調整。")]
        [SerializeField] private float cooldownSeconds = 10f;

        [Header("必殺技の範囲(isUltimate=trueの場合のみ意味を持つ、仮値・要調整)")]
        [SerializeField] private UltimateAreaShape areaShape = UltimateAreaShape.SingleTarget;
        [Tooltip("Circle: 対象中心の半径 / Line: 半幅 / Sweep: 帯の半幅 / Meteor: 着地地点中心の半径。")]
        [SerializeField] private float areaRadius = 2f;
        [Tooltip("Line専用: 発動者から対象方向への長さ。")]
        [SerializeField] private float areaLength = 6f;
        [Tooltip("Sweep専用: 発動してから消えるまでの合計時間(秒)。この間、帯を左右に往復し続ける。")]
        [SerializeField] private float sweepDuration = 3f;
        [Tooltip("Sweep専用: 左端から右端まで(片道)の所要時間(秒)。")]
        [SerializeField] private float sweepOneWayDuration = 1f;
        [Tooltip("Sweep専用: 接触している間、ダメージを与える間隔(秒)。")]
        [SerializeField] private float sweepTickInterval = 0.5f;

        [Header("必殺技: メテオ(Meteor専用、仮値・要調整)")]
        [Tooltip("Meteor専用: 発動後、その場で小さく縮む予備動作にかける時間(秒)。")]
        [SerializeField] private float meteorWindupDuration = 0.3f;
        [Tooltip("Meteor専用: 対象付近の上空へ舞い上がるのにかける時間(秒)。")]
        [SerializeField] private float meteorRiseDuration = 0.4f;
        [Tooltip("Meteor専用: 上空で静止する時間(秒)。")]
        [SerializeField] private float meteorHoverDuration = 0.3f;
        [Tooltip("Meteor専用: 対象付近へ向けて落下するのにかける時間(秒)。着地の瞬間にareaRadiusの" +
                 "円形範囲でダメージを与える(範囲の判定・ダメージ計算はCircleと共通のロジックを使う)。")]
        [SerializeField] private float meteorFallDuration = 0.3f;
        [Tooltip("Meteor専用(任意): 上昇〜落下中に発動者の見た目の代わりに表示する隕石本体の" +
                 "コマ送りエフェクト。未設定(0枚)の場合は従来通り発動者自身の見た目が拡縮するだけになる。")]
        [SerializeField] private List<Sprite> meteorEffectFrames = new();

        public string SkillName => skillName;
        public string Description => description;
        public bool IsUltimate => isUltimate;
        public int Power => power;
        public float CooldownSeconds => cooldownSeconds;
        public UltimateAreaShape AreaShape => areaShape;
        public float AreaRadius => areaRadius;
        public float AreaLength => areaLength;
        public float SweepDuration => sweepDuration;
        public float SweepOneWayDuration => sweepOneWayDuration;
        public float SweepTickInterval => sweepTickInterval;
        public float MeteorWindupDuration => meteorWindupDuration;
        public float MeteorRiseDuration => meteorRiseDuration;
        public float MeteorHoverDuration => meteorHoverDuration;
        public float MeteorFallDuration => meteorFallDuration;
        public IReadOnlyList<Sprite> MeteorEffectFrames => meteorEffectFrames;
    }
}
