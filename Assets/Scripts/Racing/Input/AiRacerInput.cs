using UnityEngine;

namespace Battrail.Racing.Input
{
    /// NPC の思考。人間と同じ 3 値（前進 / 左右 / ブースト）だけを出し、Racer 側の移動処理は共通のまま使う。
    ///
    /// 戦術はこのゲームの利得構造に合わせてある:
    ///   - 相手のトレイル上ではゲージ回復 60/s がブースト消費 35/s を上回る（CombatManager）。
    ///     つまり「相手の真後ろに付いて同じ横位置を走る」間はブーストを吹かし続けられる
    ///   - 攻撃判定が出るのは「後方にいてブースト中」の機体だけ。後ろに付くことが攻撃の前提にもなる
    ///   - コーナリング物理は無く、最高速は曲率だけで決まる（横位置に依存しない）ためライン取りの利得はゼロ。
    ///     よって横方向は「トレイルに乗る / 被弾を避ける」ためだけに使う
    public sealed class AiRacerInput : IRacerInput
    {
        /// 今フレームの狙い。横位置とブーストの出し方がこれで決まる。
        enum Intent
        {
            /// 相手が居ない・遠い。最短距離（コース中央）を走る
            Cruise,
            /// 相手が前方。トレイルに乗ってゲージを回しながら詰める
            Chase,
            /// 相手が前方かつ射程内。ブーストを当てに行く
            Ram,
            /// 自分が前方で後ろから迫られている。横にずらして被弾を避ける
            Evade,
            /// 自分が前方。まだ射程外だが、トレイルを踏ませないよう相手と横位置をずらし続ける
            Block,
        }

        readonly Racer _self;
        readonly Racer _rival;
        readonly NpcTuning _tuning;

        float _nextDecisionTime;
        Vector2 _move;
        bool _boost;

        public AiRacerInput(Racer self, Racer rival, NpcTuning tuning)
        {
            _self = self;
            _rival = rival;
            _tuning = tuning;
        }

        // ReadMove / ReadBoost は同じフレームに続けて呼ばれるので、判断は時間で 1 回に絞る。
        public Vector2 ReadMove()
        {
            Think();
            return _move;
        }

        public bool ReadBoost()
        {
            Think();
            return _boost;
        }

        void Think()
        {
            if (Time.time < _nextDecisionTime)
                return;
            _nextDecisionTime = Time.time + Mathf.Max(0f, _tuning.decisionInterval);

            var intent = DecideIntent();
            _boost = DecideBoost(intent);
            _move = new Vector2(SteerTo(TargetLateral(intent)), DecideForward());
        }

        Intent DecideIntent()
        {
            if (!HasRival())
                return Intent.Cruise;

            // 正なら相手が前。攻撃判定は「後方にいてブースト中」の機体にしか出ないので、
            // 前後関係がそのまま攻めと守りの分かれ目になる。
            float ds = _rival.DistanceAlongCourse - _self.DistanceAlongCourse;

            if (ds >= 0f)
            {
                if (ds < _tuning.ramRange)
                    return Intent.Ram;
                return ds < _tuning.trailFollowRange ? Intent.Chase : Intent.Cruise;
            }

            // 自分が前方。射程に入られてから動いても間に合わない（ブーストは一瞬で射程を詰める）ので、
            // 近いうちは常に横をずらしておく。まだ遠くてもトレイル距離内なら、軌跡を踏ませないようずらす。
            float behind = -ds;
            if (behind < _tuning.evadeRange)
                return Intent.Evade;
            return behind < _tuning.trailFollowRange ? Intent.Block : Intent.Cruise;
        }

        /// 狙う横位置。追走・体当たりは相手に重ね、回避は相手から離す。
        float TargetLateral(Intent intent)
        {
            float halfWidth = _self.Course != null ? _self.Course.HalfWidth : 0f;

            float target;
            switch (intent)
            {
                case Intent.Chase:
                case Intent.Ram:
                    target = _rival.LateralOffset;
                    break;

                case Intent.Evade:
                case Intent.Block:
                    // 相手がブースト中はより大きく離す（撃たれてから動いても間に合わない）。
                    float gap = intent == Intent.Evade && _rival.IsBoosting
                        ? _tuning.evadeGapUnderThreat
                        : _tuning.evadeGap;

                    // 既に居る側へ逃げる。そちらが壁で詰まっているなら反対側へ回す。
                    float side = _self.LateralOffset >= _rival.LateralOffset ? 1f : -1f;
                    target = _rival.LateralOffset + side * gap;
                    if (Mathf.Abs(target) > halfWidth)
                        target = _rival.LateralOffset - side * gap;
                    break;

                default:
                    target = 0f;
                    break;
            }

            return Mathf.Clamp(target, -halfWidth, halfWidth);
        }

        /// 狙う横位置への PD 制御。Racer の横移動は加速度モデルなので、
        /// P 項だけだとオーバーシュートして蛇行する。横速度で制動を掛けて収める。
        float SteerTo(float target)
        {
            float error = target - _self.LateralOffset;
            float input = error * _tuning.lateralGain - _self.LateralVelocity * _tuning.lateralDamping;
            return Mathf.Clamp(input, -1f, 1f);
        }

        /// ブースト中に緩めると coastDeceleration で減速してしまうので、その間は必ず踏み続ける。
        /// カーブでの減速は Racer 側が最高速の上限を下げて処理するため、曲率は見ない。
        float DecideForward()
        {
            return _boost || _self.ForwardSpeed < _tuning.targetSpeed ? 1f : 0f;
        }

        /// ゲージのヒステリシス。追走・体当たり・回避では使い切り、それ以外は溜めてから使う。
        /// 追走中はトレイル回復が消費を上回るので出し惜しみするだけ損だが、前方に出ると
        /// トレイルを踏めず時間回復しかないので、危険が無いうち（Block）は温存する。
        bool DecideBoost(Intent intent)
        {
            bool aggressive = intent is Intent.Chase or Intent.Ram or Intent.Evade;
            float start = aggressive ? _tuning.aggressiveStartGauge : _tuning.cruiseStartGauge;
            float stop = aggressive ? _tuning.aggressiveStopGauge : _tuning.cruiseStopGauge;

            return _boost ? _self.Gauge > stop : _self.Gauge >= start;
        }

        bool HasRival()
        {
            return _rival != null && !_rival.HasFinished;
        }
    }
}
