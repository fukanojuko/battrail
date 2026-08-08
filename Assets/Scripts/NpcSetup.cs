using UnityEngine;

namespace Battrail
{
    /// 1 人プレイ（GameMode.VsNpc）のとき、対象 Racer の入力源を AI に差し替える。
    /// Racer が Start で既定の RacerInput を作る前に注入したいので、処理は Awake で行う。
    public class NpcSetup : MonoBehaviour
    {
        [Tooltip("タイトルを経由せず Boot を直接 Play したときも NPC にする（Editor 専用のデバッグ用）")]
        [SerializeField] bool forceNpcInEditor = false;

        [SerializeField] NpcTuning tuning = new();

        private void Awake()
        {
            if (forceNpcInEditor && Application.isEditor)
                GameMode.VsNpc = true;

            if (!GameMode.VsNpc)
                return;

            var racer = Racer.Find(GameMode.NpcPlayerIndex);
            if (racer == null)
            {
                Debug.LogWarning($"[NpcSetup] playerIndex {GameMode.NpcPlayerIndex} の Racer が見つからない");
                return;
            }

            // 相手（人間側）は駆け引きの前提なので一緒に渡す。見つからなければ単独走行に落ちる。
            var rival = Racer.Find(1 - GameMode.NpcPlayerIndex);
            racer.SetInput(new AiRacerInput(racer, rival, tuning));
        }
    }
}
