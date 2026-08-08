using UnityEngine;

namespace Battrail
{
    /// タイトルで選んだ対戦形式を Boot シーンへ持ち越すための入れ物。
    /// PostRaceController のリトライは同じシーンの再ロードなので、static に置けばリトライ後も保たれる。
    public static class GameMode
    {
        /// true なら P2 を NPC が操作する 1 人プレイ。false は従来のローカル 2 人対戦。
        public static bool VsNpc { get; set; }

        /// NPC が操作する側の playerIndex。1v1 前提なので P1 が人間・P2 が NPC で固定。
        public const int NpcPlayerIndex = 1;

        // ドメインリロードを無効にしていると static が Play をまたいで残るため、明示的に初期化する。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            VsNpc = false;
        }
    }
}
