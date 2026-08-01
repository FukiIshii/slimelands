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

        public string SkillName => skillName;
        public string Description => description;
        public bool IsUltimate => isUltimate;
        public int Power => power;
        public float CooldownSeconds => cooldownSeconds;
    }
}
