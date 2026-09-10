using System;
using UnityEngine;

namespace Battrail.Racing.Input
{
    /// NPC の挙動調整値。NpcSetup が Inspector に出す（Racer 側に NPC 専用の項目を増やさない）。
    [Serializable]
    public sealed class NpcTuning
    {
        [Tooltip("判断を更新する間隔（秒）。上げるほど反応が鈍く、結果として弱くなる")]
        public float decisionInterval = 0.06f;

        [Header("Forward")]
        [Tooltip("巡航で狙う速度。Racer の maxSpeed(18) と同値なら常にフルスロットル")]
        public float targetSpeed = 18f;

        [Header("Lateral")]
        [Tooltip("狙う横位置とのズレを入力に変える係数（P 項）")]
        public float lateralGain = 2f;
        [Tooltip("横速度のフィードバック（D 項）。オーバーシュートによる蛇行を止める")]
        public float lateralDamping = 0.2f;

        [Header("Tactics")]
        [Tooltip("相手の後方このΔs以内に入ったらブースト体当たりを狙う。CombatManager の hitRangeS は 1.4")]
        public float ramRange = 3f;
        [Tooltip("相手の後方このΔs以内なら追走（トレイル）が成立しているとみなす。CombatManager の trailSeconds 3 秒ぶんの距離")]
        public float trailFollowRange = 30f;
        [Tooltip("前方にいるとき、後方からこのΔs以内に迫られたら回避に入る")]
        public float evadeRange = 5f;
        [Tooltip("相手の横位置から離す距離。CombatManager の hitRangeT(0.9) より大きくする")]
        public float evadeGap = 1.3f;
        [Tooltip("相手がブースト中（＝いつ当てられてもおかしくない）ときに離す距離")]
        public float evadeGapUnderThreat = 2.2f;

        [Header("Boost")]
        [Tooltip("巡航中にブーストを始める／やめるゲージ")]
        public float cruiseStartGauge = 60f;
        public float cruiseStopGauge = 30f;
        [Tooltip("追走・体当たり・回避のときの開始／終了ゲージ。" +
                 "相手のトレイル上は回復(60/s)が消費(35/s)を上回るので、ここは使い切る側に振る")]
        public float aggressiveStartGauge = 15f;
        public float aggressiveStopGauge = 3f;
    }
}
