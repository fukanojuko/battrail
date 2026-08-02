# battrail 仕様

レース＋バトル系。トレイルとブースト中衝突で攻防が成立する **1対1 対戦** ゲーム。
順位（前後関係）と相手の状態を読みながら、ゲージを管理して攻撃機会を作る。

**段階前提**:
- **中間（開発中の基準形）**: 1 台の PC ＋ コントローラー 2 個 ＋ **画面分割**で 1v1 がローカル対戦できる。
- **最終**: PC 2 台でオンライン対戦（**専用サーバー無し**、ホスト＝クライアント方式）。

---

## 操作 (Controls)

- **前進**（W / ↑ / ゲームパッド左スティック↑） — 加速
- **後退**（S / ↓ / 左スティック↓） — 減速（ブレーキのみ、逆走なし）
- **左右**（A・D / ← → / 左スティック横） — コースの進行方向に対する**横オフセット**移動（コース幅内に収まる）。加速度＋慣性＋中央への弱い引力で反応する
- **ブースト**（予定: LeftShift / Space / ゲームパッド RT） — 速度限界超え＋攻撃判定発生

### 詳細（コース追従 = スプライン相対モデル）

- **ステアリング入力は無い**。曲がる操作はプレイヤーは行わない。**コース（スプライン）が前進方向を決め、プレイヤーはそれに沿ってスナップ／追従する**。
- 前進後退は指定の速度限界まで**徐々に**加減速する。前進速度はスプライン沿いの進行（弧長）に対する速度。
- 左右入力は、進行方向に対して**横方向に少し動ける**だけ。コースの幅 ±W に制限。
- **入力を離したときの挙動**: 慣性で滑らかに減速しつつ、**コース中央 (t=0) へ弱い引力で少しずつ引き戻される**。急な切り返しでは慣性によりわずかにオーバーシュートし、ブレのような挙動になる。
- 機体の見た目の向きはスプラインの接線方向に合わせる。

### 内部状態モデル

- `s` — スプライン上の進行距離（弧長）
- `t` — スプライン中心からの横オフセット（±halfWidth 内）
- 前進入力 → `s` の速度を加減速
- 左右入力 → `t` の**加速度**を操作。中央への引力＋摩擦減衰を加えた速度を積分して `t` を動かす（ばね＋減衰モデル）
- ワールド座標 = `spline.EvaluatePosition(s) + spline.GetRight(s) * t`
- ワールド回転 = `LookRotation(spline.GetTangent(s), up)`

---

## ブースト

- 速度限界を超えた速度域に入り、**攻撃判定**が発生する。
- 入力モデル: **押下中だけブースト持続**（離すと終了、ゲージが空でも終了）。
- 保持中はゲージを消費。時間で徐々に回復。**トレイル通過で大幅回復**。
- **ブースト中の衝突**:
  - 移動方向が揃っている → **前方プレイヤーが減速＋横方向にはじき出される**（後方ブースター側は速度維持）
  - 向かい合っている → **速度の値を交換**

### 当たった時の効果 (拡張可能な seam)

「被弾側に何を起こすか」「片方だけブースト中の場合」等は今は最小実装で済ませ、**後から差し替えやすい形**で切り出す:

- `HitContext`（attacker, victim, relativeSpeed, isHeadOn 等を含む値型）を作る
- `IHitReaction.OnHit(HitContext)` を定義し、Racer に注入可能な形にする
- 初期実装: 「被弾側を短時間入力無効＋横方向に大きく弾く」程度
- 演出（カメラシェイク・パーティクル・音）は `IHitReaction` の差替で順次追加

**ヒットエフェクト**: `Racer.PlayHitEffect()` が `Racer` の持つ `VisualEffect`
（`BasicImpacts`、`Player.prefab` の子）を `Play()` する。

- **攻撃ヒット時**は `DefaultHitReaction.OnHit()` から被弾側のみ再生（一方的にやられる側が光る）
- **非攻撃の接触**（左右分離）は `CombatManager.Resolve()` から両者を再生。
  スタンは無くても「当たった感」は出す方針（`separationSpeed` の相対速度スケールと同じ理由）
- `BasicImpacts` の **Initial Event Name は空にしてある**。既定の `OnPlay` のままだと
  生成された瞬間（レース開始時）に一度再生されてしまうため。被弾時に `Play()` で明示的に鳴らす
- エフェクトはベースの `Player.prefab` に置く。P1/P2 で共通なので Variant 側に個別追加しない
- **大きさ・寿命はコンポーネント側の上書きで調整する**（アーティストの `.vfx` は触らない）。
  初期値は Size 4.0 / LifeTime 0.1〜0.2 秒で、機体（全長 1.09）の約 4 倍の大きさが 6〜12 フレーム
  だけ出る状態だった。「大きすぎるのにほぼ見えない」ため Size 2.0（Main は 1.2）/
  LifeTime 0.28〜0.45 秒に調整した。

通常同士の衝突（非ブースト）はスタン無し。ただし「当たった感」を出すため、**相対速度が大きいほど強く弾かれる**（固定値の軽い接触ではなく、速度差に応じた跳ね返り＋わずかな減速）。

### 追い上げ（カムバック）調整

- **スタートダッシュ**: スタン解除の瞬間、`startDashDuration`秒だけ`startDashSpeed`まで強制加速（`Racer`）。攻撃判定は付与しない（被弾直後の無償の反撃機会にはしない）。被弾で離された側が差を詰め直すための救済。
- **当たり判定の範囲**: `hitRangeS = 1.4` / `hitRangeT = 0.9`（`CombatManager`）。機体の見た目の接触距離（前後 1.09 / 横 0.68）の約 1.3 倍。詳細は「当たり判定と機体サイズ」を参照。

### 攻撃側判定のバグ修正

- 旧実装は `a.IsAttacking ^ b.IsAttacking`（XOR）で attacker を決めていたため、**両者ともブースト中に衝突すると何も起きない**不具合があった。前方/後方の位置関係（s の大小）で決め、「後方にいてブースト中」の機体だけを attacker とする形に修正（前方側のブースト有無は問わない）。

---

## トレイル

- プレイヤーの**直近数秒分の走った軌跡**にトリガー当たり判定＋エフェクトを発生させる。
- **他プレイヤー**がそのトリガーを通過すると、**通過した側**のブーストゲージを回復させる（仕様: 「通過したプレイヤーのブーストゲージを大幅回復」）。
- 自分自身のトレイルでは回復しない（owner 判定）。
- 他プレイヤーの**ブースト中の体当たり**との関係: 衝突は通常のプレイヤー同士の collider 同士で判定（→「ブースト」節）。トレイルは別系統。

### レイヤ構成（差替を見越して分離）

```
Racer (ゲームロジック・(s,t) 管理)
  ├── RacerTrailVisual (見た目だけ。VFX Graph で実装、デザイナー領域)
  └── RacerTrailCollider (当たり判定だけ。コード領域)
```

- **見た目**: **VFX Graph** で組む（パッケージ `com.unity.visualeffectgraph` を導入）。デザイナーが VFX Asset を制作 → Racer の Visual に attach。Racer 側からは「位置・前進速度・機体姿勢」を提供するだけで、見た目には干渉しない。
- **当たり判定**: 軌跡をワールド座標で区切った**トリガー BoxCollider のセグメント**を一定間隔でスポーン、寿命（数秒）で Destroy。各セグメントは owner Racer を保持し、`OnTriggerEnter` で別 Racer を検出したら通過処理（ゲージ回復）を呼ぶ。
- スプラインモデルでも、判定はワールド座標で素直に成立（プレイヤーのワールド位置をスプラインから求めるだけ）。

---

## コース

- **形状**: スタートからゴールまでの一本道。**カーブあり、ループなし**。
- スプラインで定義（`s` がスタート=0、ゴール=spline length）。
- 壁に当たり判定が存在する。
- プレイヤーが壁に接触した場合は、**すこし跳ね返って減速する**。
- コース幅（横オフセット上限）: 仮 **±3**（後でチューニング）。
- **全長**: 約 **1634**（巡航 18u/s で約 91 秒、フルブースト 28u/s で約 58 秒）。
  導入の S 字 → 上りの高速区間 → 右回りヘアピン → 下りの復路 → 左回りヘアピン → 最終ストレート。
  高低差は y = 0〜14。
- **曲率に応じた減速**: `CourseSpline.GetCurvature(s)` が中心線の曲率 [rad/unit] を返し、
  `Racer.CornerSpeedFactor()` が最高速の倍率へ変換する（`cornerFullEffectCurvature` で
  `cornerSpeedFactorMin` に到達、間は線形）。**加速度は変えず上限だけ下げる**ので、
  直線に抜けると自然に速度が戻る。曲率は事前計算テーブル（`curvatureSampleSpacing` 間隔）の線形補間。
  - 現在値（`cornerSpeedFactorMin = 0.85` / `cornerFullEffectCurvature = 0.05`）での実測:
    直線相当（中央値）で 99%、p90 で 94%、最急カーブで 85%。巡航タイムは 91.4s → 93.7s（+2.5%）。
  - コーナリング物理そのものは無い（曲率は速度上限にしか影響せず、操作感やライン取りは変わらない）。
    さらに難度を持たせるなら「カーブ中の外側への t ドリフト」等が別途必要。
- 既定の knot 列は `CourseSpline.DefaultKnots`。`SplineContainer` が空のときだけ組まれ、
  Editor で knot を編集すればシーン側が優先される。路面メッシュの分割は固定数ではなく
  `roadSegmentLength`（1 セグメントの長さ）基準なので、コースを伸ばしても密度が保たれる。

## 試合

- **勝利条件**: **先にゴール（`s >= spline.length`）に到達した方が勝ち**。
- それ以外のスコア・ラップ・時間制限は当面なし（後付け）。

---

## プレイヤーが持つ情報

- 自機の各方向への速度
- 前後の順位のプレイヤーの速度（1対1の場合は相手の速度）
- 道に対しての移動方向（左 / 右 / 直進）
- 相手の移動方向
- ブーストゲージ残量
- 通常移動の速度限界
- ブースト時の速度
- ※必要に応じて追加

---

## ローカルマルチ（中間ステップ ＝ 開発中の基準形）

- **1 台 PC ／ コントローラー 2 個 ／ 画面分割** で 1v1 対戦。
- 入力ペアリング: playerIndex で分岐。`Gamepad.all[index]` があればそれ、無ければ Keyboard（P1=WASD / P2=矢印）。Racer.ReadMove() に集約（オンライン化時はここだけ差替）。
- カメラ: Cinemachine v3 の `OutputChannel` / `ChannelMask` で 2 個の Unity Camera にそれぞれの追従 vcam を割り振る。3人称チェイスカム（機体の斜め後方・低め、`FollowOffset`(0, 2.2, -8)）。`CinemachineFollow`（Body: `LockToTargetWithWorldUp` で機体の向きに追従）＋`CinemachineRotationComposer`（Aim: 機体を実際に見る）の組み合わせが必須。`CinemachineFollow`単体だとBindingModeを変えても実際の向き（Aim）が更新されない不具合があった。
- 画面分割の向き: **左右分割**（実装済み）。Main Camera が左半分（viewport 0,0,0.5,1）、Camera 2 が右半分（0.5,0,0.5,1）。
- HUD: 各プレイヤーのビューポート内に独立表示。
- 衝突・トレイル・ブースト等のゲームルールは **2 機体前提**で実装する。AI は当面入れない（必要になったらダミー入力で代用）。
- 既知の課題: `Gamepad.all` の順序依存（抜き差しで P1/P2 が入れ替わりうる）。本番前に明示ペアリングが要る。

## ネットワーク (最終形: 1v1, 専用サーバーなし)

- **接続モデル**: ホスト＆クライアント方式。片方の PC がホスト（ゲームのシミュレーションを authoritative に走らせつつ自分もプレイ）、もう片方がクライアント接続。
- **トランスポート**: 未定。
  - **A. LAN 限定（同一ネットワーク内のみ）** — 設定最小、UDP 直結 or Unity Transport の LAN モード。
  - **B. インターネット越し（Unity Relay 等で NAT 越え）** — 別途リレーサービス利用。専用サーバーではないが外部依存は増える。
- **権威モデル**: ホスト機が**両プレイヤーの物理／衝突／ゲージ／トレイル判定**を持つ。クライアントは入力を送り、ホストから来る状態を補間して描画する。
- **同期対象**: 位置・速度（補間用）、ブースト状態、ゲージ、トレイルセグメント、コリジョン結果（はじき・速度交換）、順位。
- **ライブラリ候補**:
  - **Unity Netcode for GameObjects (NGO)** — Unity 公式、ホスト＝クライアント方式と相性◯。
  - **Mirror** — 実績豊富な OSS。
  - **生 UDP/TCP** — 軽量だが配線フル自作。1v1 だけなら成立はする。
- **未確定事項**:
  - LAN 限定 / Relay あり / 両対応 のどれにするか
  - ライブラリ選定（NGO 推奨）
  - 接続 UX（ホスト IP 入力 / Steam Lobby / シンプルなコード共有 など）

## 実装ロードマップ（進捗チェックリスト）

- [x] **基盤** — 加速度移動、Cinemachine 追従カメラ、Boot シーン実体配置
- [x] **スプライン追従** — (s, t) モデル、CourseSpline、ゴール判定、道路メッシュ可視化
- [x] **ローカル2人＋画面分割（左右）** — Player2 追加、playerIndex デバイス分岐、Cinemachine OutputChannel で 2 viewport へ振り分け
- [x] **ブースト＋ゲージ** — 押下中持続で boostSpeed まで、消費／時間回復、ブースト中=攻撃判定（Racer）
- [x] **壁** — t が ±halfWidth で跳ね返り＋減速（Racer.StepLateral）
- [x] **トレイル（判定）** — (s,t) 履歴を CombatManager が保持、他機通過でゲージ回復。見た目は仮 LineRenderer（VFX 差替待ち）
- [x] **プレイヤー衝突** — (s,t) 近接判定。片ブースト=攻撃側が相手を減速＋横はじき＋短スタン、それ以外は左右分離。HitContext/IHitReaction で差替可能
- [x] **HUD** — UI Toolkit (UXML/USS)。viewport 別（左=P1 / 右=P2）に速度・順位・ゲージバー（`Assets/UI/`, PlayerHudUI）
- [x] **順位** — s 順で順位算出＋表示（PlayerHudUI.Rank）
- [x] **勝敗UI** — RaceManager が決着で全 Racer を停止（Racer.EndRace）、HUD に "PX WIN" オーバーレイ表示
- [x] **左右の挙動差替** — 平行移動から加速度＋慣性＋中央への弱い引力（ばね＋減衰モデル）へ差替（Racer.StepLateral）。数値は暫定、実機プレイで要調整
- [x] **タイトル画面** — `Assets/Scenes/Title.unity`。ゲームタイトル＋"PRESS START"（点滅）＋操作説明。いずれかのキー/ゲームパッドボタンで Boot へ遷移（TitleScreenController）。Build Settings で Title(0) → Boot(1)
- [x] **決着後のフロー** — 決着後 1 秒待ってから、SPACE/Enter/Start ボタンで同シーンをリトライ、ESC/Select ボタンでタイトルへ戻る（PostRaceController）。HUD の勝敗オーバーレイに操作ヒントを表示
- [x] **outgame の見た目・導線改善** — タイトル画面に宇宙背景（Boot と同じ Skybox）を適用し世界観を統一。レース中は ESC/Start でポーズ（`Time.timeScale = 0`、Resume/Quitオーバーレイ）、ポーズ中に Q/Select でタイトルへ（PauseController）。HUD 上部に常時ヒント表示
- [x] **トレイル見た目（VFX Graph）** — デザイナーが各 Racer に VFX（`MasterTrail` / `MasterTrail_CaseC`）を実装（`feature/trail-system`）。`CombatManager` の仮 LineRenderer（`TrailVisual`）は撤去し、判定用の (s,t) 位置履歴のみ保持する形に整理
- [ ] **ネットワーク（最終: 1v1 P2P）** — ホスト＝クライアント接続、入力／状態同期、ロビー or 接続 UX

---

## プロジェクト運用 / チーム

- **作業ブランチ**: `develop`（main は安定版。機能は `feature/*` → `develop`、区切りで `develop` → `main`）。
- **プレイヤーのプレハブ構成**: `Assets/Prefabs/Players/Player.prefab` がロジック・共通構造を持つ base。`Player1.prefab` / `Player2.prefab` はその **Prefab Variant** で、見た目（マテリアル）だけを差し替えている。モデル・共通ロジックの変更は base の `Player.prefab` を編集すれば両方に伝播する。`playerIndex` / `startLateralOffset` はシーン（Boot.unity）側のインスタンス override（ゲームプレイ上のデータなので見た目の差分＝Variantには含めない）。
- **デザイン作業領域**: `Assets/Contents/Artist/`（例: `TestMech/`＝ロボ機体 FBX＋マテリアル）。デザイナーがここで素材を管理。
- 機体モデルの差し替え（cube → `TestMech` の FBX）は base `Player.prefab` の MeshFilter/Renderer を差し替えれば両プレイヤーに反映される。プレイヤーごとの色だけ変えたい場合は各 Variant（`Player1.prefab` / `Player2.prefab`）側で差し替える。

---

## 仕様判断ログ

セッション中に確定した解釈・選択を記録（後でひっくり返すかも前提）。

- **後退入力 = ブレーキのみ**。仕様「後退(減速)」の `(減速)` を素直に解釈、`[0, MaxSpeed]` クランプ、逆走なし。レース系のお手本（F-Zero / Wipeout）にも合う。壁スタック等で逆走が必要になれば再検討。
- **車らしいステアリング回転は不採用（仕様優先）**。車キャラ付けの観点で A/D が yaw 回転になる案もあるが、仕様の `平行移動` を優先。
- **左右挙動 = 加速度＋慣性＋中央への弱い引力（複合案）を採用**。「コース引っ張り＋ブレ」の解釈として、中央への緩やかな引き戻しのみ／慣性オーバーシュートのみ／両方の複合、の3案から複合案を選択。文字通り「引っ張り」と「ブレ」の両方を再現できるため。

---

## 現状のキーマッピング

| 操作 | キーボード | ゲームパッド |
|------|------------|--------------|
| 前進 | W, ↑ | 左スティック↑ |
| 後退（ブレーキ） | S, ↓ | 左スティック↓ |
| 左 | A, ← | 左スティック← |
| 右 | D, → | 左スティック→ |
| ブースト | （未実装） | （未実装） |

すべて `Assets/InputSystem_Actions.inputactions` の `Player.Move` アクションを経由。

---

## 関連ファイル

- `Assets/Scripts/Racer.cs` — 移動ロジック本体（前進・後退・左右）
- `Assets/Scripts/RacerInput.cs` — デバイス入力読み取り（Racer から分離。オンライン対応時はここだけ差替）
- `Assets/Scripts/TrailVisual.cs` — トレイルの見た目（CombatManager から分離。VFX Graph 差替時はここを差し替える）
- `Assets/InputSystem_Actions.inputactions` + 自動生成 `.cs` — 入力アセット
- `Assets/Scenes/Boot.unity` — メインシーン
- `Assets/Materials/{Ground,Player}.mat` — URP/Lit マテリアル

## アーキテクチャ方針

- **Update() ポーリングより event / Awaitable**: 「状態変化を検知して一度だけ何かする」類の処理（決着検知、決着後の入力待ち等）は Update() で毎フレーム条件を見るのではなく、C# event（`Racer.Finished` 等）や Unity 6 の `Awaitable`（`PostRaceController` 等）で書く。ただし物理シミュレーション（`Racer.FixedUpdate`, `CombatManager.FixedUpdate`）や継続的な値の毎フレーム反映（`PlayerHudUI.Update` の速度/ゲージ表示、`TitleScreenController.Update` の点滅演出）は本来的に毎フレーム処理が必要なため対象外
- **新規の外部依存は入れない**: 上記は R3 等の Reactive ライブラリを追加せず、C# 標準の `event`/`Awaitable` のみで実現する方針（「依存は負債」）
- **単一責任でクラスを分ける**: `Racer`＝移動物理、`RacerInput`＝デバイス入力、`CombatManager`＝衝突・トレイル判定ロジック、`TrailVisual`＝トレイルの見た目、という形で責務を分離

---

## 値のチューニング基準（暫定）

- `maxSpeed = 18`
- `acceleration = 14`
- `brakeDeceleration = 26`
- `coastDeceleration = 7`（無入力時の摩擦）
- `strafeAcceleration = 26`（左右入力の加速度）
- `lateralDamping = 10`（左右速度の摩擦減衰）
- `centerPullStrength = 1.2`（中央 t=0 への引力係数）
- `maxLateralSpeed = 9`

`Racer` の `[SerializeField]` 経由で Inspector からも調整可。

### 値を変えるときは、コードではなくシーン／プレハブ側を変える

**`[SerializeField]` の初期化子（コード側の既定値）を書き換えても、既にシーンやプレハブに
シリアライズ済みのコンポーネントには反映されない。** 初期化子が使われるのはコンポーネントを
新規追加したときだけで、以後はシリアライズされた値が常に勝つ。

実際に `Combat.cs` の `hitRangeS` を 1.2 → 2.0 に、`separationSpeed` を 4 → 6 に書き換えた
変更が `Boot.unity` に反映されず、長期間「広げたはずの当たり判定が効いていない」状態になっていた。

- 調整は Inspector（＝シーン／プレハブ）側で行い、コードの初期化子も同じ値に揃えておく
- 乖離が疑わしいときは `SerializedObject` でシーン値とコード既定値を突き合わせて確認する

### 当たり判定と機体サイズ

プレイヤー同士の判定は `CombatManager` が (s, t) 空間で行う（`hitRangeS` / `hitRangeT`）。
**`Player.prefab` の BoxCollider は現状どこからも参照されていない**（`Assets/Scripts/` に
`OnCollision` / `OnTrigger` / `Physics.` の使用は無く、`Racer.Awake()` が `isKinematic = true` を
設定し、位置は毎 FixedUpdate に `MovePosition` で確定する。kinematic 同士は衝突を生成しない）。
判定を広げたいときにコライダーを触っても効果は無い。

- 機体の見た目のサイズ: 幅 **0.68** × 高さ 0.95 × 全長 **1.09**
- 2 機が視覚的に接触する距離: 横 **0.68** / 前後 **1.09**
- 現在値 `hitRangeS = 1.4`（接触距離の 1.3 倍）/ `hitRangeT = 0.9`（同 1.3 倍）
- 1 物理ステップの最大 Δs は 0.56 なので、判定幅に対してすり抜けは起きない
- **横幅を広げすぎるとコースが狭く感じる**。`hitRangeT` は相手の周囲 ±`hitRangeT` を占有ゾーンに
  するので、コース幅 6 に対して 1.2 だと 40%、0.9 だと 30% が塞がる。1.2 で試して「コースが狭い」
  という判断になったため 0.9 に下げた。
