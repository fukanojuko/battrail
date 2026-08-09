# 調整ポイント早見表

---

## 走り・操作感

**場所**: `Assets/Prefabs/Players/Player.prefab` の `Racer`（P1/P2 共通。ここを直せば両方に効く）
※ `playerIndex` と `startLateralOffset` だけは `Boot.unity` のインスタンス側 override

- `maxSpeed`（18）— 通常時の最高速。上げるとレース全体が短くなる
- `acceleration`（14）— 前進入力の伸び。「もっさりする」ならここ
- `brakeDeceleration`（26）— ブレーキの効き
- `coastDeceleration`（7）— 無入力時の減速。惰性の残り方
- `strafeAcceleration`（26）— 左右入力の初動の軽さ
- `lateralDamping`（10）— 横速度の収束。下げるとフワフワ、上げるとキビキビ
- `centerPullStrength`（1.2）— コース中央へ戻ろうとする強さ。上げると勝手に真ん中に寄る
- `maxLateralSpeed`（9）— 横移動の最高速
- `cornerSpeedFactorMin`（0.85）/ `cornerFullEffectCurvature`（0.05）— カーブでどれだけ最高速が落ちるか。
  0.85 = 最急カーブで 85%。1 にするとカーブ減速なし
- `wallBounce`（6）/ `wallSpeedRetain`（0.8）— 壁に当たったときの跳ね返りと減速（0.8 = 20% 減速）

## ブースト・ゲージ

**場所**: 同上（`Racer`）

- `boostSpeed`（28）— ブースト中の最高速。`maxSpeed` との差が「速くなった感」
- `boostAcceleration`（24）— ブーストの伸び
- `overspeedDecay`（12）— ブースト解除後、通常最高速まで落ちる速さ。余韻の長さ
- `maxGauge`（100）/ `gaugeDrainPerSecond`（35）/ `gaugeRegenPerSecond`（12）—
  ブーストを何秒吹けるか・何秒で戻るか。現在値で連続 約2.9秒、全回復 約8.3秒
- `boostRestartGauge`（25）— 空になった後、再びブーストできるまでに必要な回復量。
  現在値で押しっぱなしのまま約 2.1 秒待つと再点火できる。下げると息継ぎが短くなり、
  0 にすると空ゲージでブーストが永続する不具合が戻る（spec.md「空ゲージでの再点火バグ修正」）。
  **`maxGauge` より大きくしない**（ロックが解けず二度とブーストできなくなる）
- `startDashDuration`（1.5）/ `startDashSpeed`（28）/ `startDashAcceleration`（30）—
  被弾スタン明けの救済加速。追い上げのしやすさ

## スタート演出（カウントダウン）

**場所**: `Boot.unity` の `RaceManager`

- `countdownInterval`（1）— "3" "2" "1" を 1 つ出しておく時間（秒）。スタートまでの待ち時間は この 3 倍
- `goDisplaySeconds`（0.7）— "GO!" を残す時間。**この間もレースは進行している**（操作開始は GO! と同時）
- 文字の並び自体は `RaceManager.CountLabels` / `GoLabel`（コード側の定数）

**見た目**: `Assets/UI/PlayerHud.uss` の `.countdown-overlay` / `.countdown-text`

- `background-color`（rgba 0,0,0,0.3）— 黒の濃さ。result(0.45) / pause(0.6) より薄くしてある
- `font-size`（120px）/ `letter-spacing`（8px）— 数字の大きさ

## BGM

**場所**: `Boot.unity` の `RaceManager` に付いた `RaceBgm`

- `clip` — 流す曲。**未設定なら無音のまま何も起きない**。音源は `Assets/Contents/Artist/BGM/` に置いて
  Unity Editor でインポートしてから、ここに挿す
- `volume`（0.3）— 音量。ゲーム内の効果音より前に出すぎないところから始める
- `fadeOutSeconds`（1.5）— 決着後に消えるまでの時間。0 にすると即停止

`AudioSource` は実行時に `RaceBgm` が生成するので、インスペクタには出ない（loop / 2D 再生は
コード側で固定）。ポーズ中の停止は `PauseController` の `AudioListener.pause`。

## 当たり判定・攻防

**場所**: `Boot.unity` の `CombatManager`

- `hitRangeS`（1.4）— 前後方向の判定距離。機体の全長 1.09 の約 1.3 倍
- `hitRangeT`（0.9）— 横方向の判定距離。**広げすぎるとコースが狭く感じる**（コース幅 6 に対し 0.9 で 30% 占有）
- `hitCooldown`（0.4）— 連続ヒットの間隔
- `victimForwardSpeedFactor`（0.5）— 被弾側がどれだけ減速するか
- `victimLateralImpulse`（9）— 被弾側が横に弾かれる強さ
- `victimStunSeconds`（0.35）— 被弾側の操作不能時間
- `separationSpeed`（6）— 非ブースト同士の接触で左右に分離する強さ

## カメラ

**場所**: `Boot.unity` の `CM Camera`（P1）/ `CM Camera 2`（P2）。**2 つとも同じ値にすること**

- `CinemachineFollow > FollowOffset`（0, 3, -9）— 3人称視点の位置。z が引きの距離、y が高さ
- `CinemachineFollow > PositionDamping`（0.2）— 追従の遅れ。上げるとカメラがゆったり、下げると張り付く
- `CinemachineRotationComposer > Damping`（0.2）— 向きの追従の遅れ
- `CinemachineRotationComposer > DeadZone`（0.05）— 機体が少し動いてもカメラが向きを変えない範囲
- `CinemachineCamera > Lens > FieldOfView`（55）— 画角。上げると速度感が増すが端が歪む

**触らない方がいいもの**: `Main Camera` / `Camera 2` の `CinemachineBrain > UpdateMethod` は
`LateUpdate` 固定。既定の `SmartUpdate` に戻すとカメラが 50Hz 更新になり、機体（Rigidbody 補間で
毎フレーム描画）に対してカクついて見える。

## ブースト演出（カメラの引き）

**場所**: `CM Camera` / `CM Camera 2` の `BoostCameraKick`

- `kickOffset`（0, 0.5, -3）— 吹き始めにどれだけ引くか。z が引きの量、y で少し持ち上がる
- `kickInDuration`（0.3）— 引き切るまでの時間。**`PositionDamping`（0.2）より短くすると、
  速さが damping 側に支配されて指定より速く見える**
- `recoverDuration`（1.2）— 元の位置へ戻るまでの時間

## ブースト演出（集中線）

**場所A**: `Main Camera` / `Camera 2` の子 `ConcentrationLine` の `BoostConcentrationLine`

- `maxAlpha`（0.35）— ブースト中の濃さ。まず触るならここ
- `fadeInDuration`（0.12）— 出るまでの時間
- `fadeOutDuration`（0.3）— 消えるまでの時間

**場所B**: `Assets/Contents/Artist/ConcentrationLineEffect/Material/ConcentrationLine.mat`（線の絵柄そのもの）

- `_LineRate`（240）— 線の本数
- `_LineSpeed`（17.8）/ `_Speed`（0.2）— 線の流れる速さ・揺らぎ
- `_LineIntensity`（0.76）— 線の太さ／濃さのコントラスト
- `_Randomness`（49.5）— 線の不揃いさ
- `_AngleScale`（1）— 線の広がり方
- `_Color` / `_BlightColor` — 色。HDR（1 超え）で指定されていて Bloom で光る前提。
  光り方を変えるなら Volume の Bloom 側も見る

**注意**: `_Alpha` は実行時に `BoostConcentrationLine` が毎フレーム上書きするので、`.mat` 側でいじっても
Play では効かない。濃さは `maxAlpha` で調整する（マテリアルは実行時に複製されるため、Play 中に
インスペクタで `.mat` を触っても反映されない）。

## ヒットエフェクト・トレイル

**場所**: `Assets/Prefabs/Players/Player.prefab` の子（VFX）／`Player1.prefab`・`Player2.prefab` のトレイル VFX

- `BasicImpacts`（被弾エフェクト）の `Size`（2.0 / Main は 1.2）/ `LifeTime`（0.28〜0.45）—
  **`.vfx` 本体ではなくコンポーネント側の override で調整する**（アーティストのアセットは触らない）
- `MasterTrail_CaseC` の `TriggerTrailRate`（60）/ `TrailLifeTime`（2.8）— トレイルの密度と残る時間。
  見た目だけ。当たり判定は下の `CombatManager` 側

## コース

**場所**: `Boot.unity` の `Course`（`CourseSpline` + `SplineContainer`）

- `halfWidth`（3）— コース幅の半分。走れる横幅。当たり判定の体感にも直結
- `roadSegmentLength`（1.5）— 路面メッシュの分割の細かさ（見た目のみ）
- `curvatureSampleSpacing`（4）— カーブ減速の計算に使う曲率テーブルの間隔
- コース形状そのものは `SplineContainer` の knot を Editor で編集（触ると `CourseSpline.DefaultKnots`
  より優先される）

## 画面全体の絵作り

**場所**: `Assets/Settings/SampleSceneProfile.asset`（`Boot.unity` の `Global Volume` が参照）

- `Bloom > Intensity`（1）/ `Threshold`（0）/ `Scatter`（0.25）— 光の滲み。集中線・トレイルの光り方が変わる
- `Tonemapping`（Neutral）— 全体の色の出方
- `Vignette > Intensity`（0.2）— 画面四隅の暗さ
- `MotionBlur > Intensity`（0.6）— 速度感。上げるとブースト中の流れが強くなる（Mode は override オフ）

## HUD

**場所**: `Assets/UI/PlayerHud.uxml` / `PlayerHud.uss`（レイアウト・見た目）、
`Assets/Scripts/PlayerHudUI.cs`（表示内容とゲージの色）

- ゲージの色は `PlayerHudUI` の `BoostColor` / `NormalColor` / `StunColor`（コード側の定数）
- 配置・サイズ・フォントは `.uss`

## NPC の強さ

**場所**: `Boot.unity` の `RaceManager` > `NpcSetup` > `Tuning`

**弱くしたいとき効くのは上の 2 つ**（`decisionInterval` を上げる、`targetSpeed` を下げる）。

- `decisionInterval`（0.06）— 判断を更新する間隔。**強さの主軸**。上げるほど反応が鈍くなる。
  0.2 くらいまで上げると目に見えて隙ができる
- `targetSpeed`（18）— 巡航で狙う速度。`Racer.maxSpeed`（18）と同値なら常にフルスロットル。
  下げると人間より遅くなる（コーナーでの減速は `Racer` 側の上限で自動的に掛かるので、ここでは考えなくて良い）
- `lateralGain`（2.0）/ `lateralDamping`（0.2）— 狙う横位置への PD 制御。
  **`lateralDamping` を 0 にすると蛇行する**（加速度モデルに対して P 項だけだと振動するため）。
  `lateralGain` を上げるなら damping も上げる（臨界制動はおよそ `26*damping + 10 = 2*sqrt(26*gain)`）
- `ramRange`（3.0）— 相手の後方この距離まで詰めたら体当たりを狙う。`CombatManager.hitRangeS` は 1.4
- `trailFollowRange`（30）— この距離以内なら相手を意識する（後方なら追走、前方ならトレイルを踏ませない
  ように横をずらす）。これを超えて離れている間だけコース中央を直進する。**下げると直線的な走りが増える**
- `evadeRange`（5.0）— 前方にいるとき、後ろからこの距離まで迫られたら回避に入る。
  ブースト有無は問わない（撃たれてから動いても間に合わないため）
- `evadeGap`（1.3）/ `evadeGapUnderThreat`（2.2）— 相手の横位置から離す量と、相手がブースト中のときの量。
  **`CombatManager.hitRangeT`（0.9）より大きく保つ**（下回ると避けきれない）。
  上げるほど横に大きく振れるが、コース幅は ±3 なので 2.5 を超えると壁に張り付きやすくなる
- `cruiseStartGauge`（60）/ `cruiseStopGauge`（30）— 相手が遠いときのブースト開始・終了ゲージ
- `aggressiveStartGauge`（15）/ `aggressiveStopGauge`（3）— 追走・体当たり・回避中の開始・終了ゲージ。
  相手のトレイル上は回復（60/s）が消費（35/s）を上回るので、ここは使い切る側に振ってある。
  上げると出し惜しみして弱くなる
