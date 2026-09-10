using UnityEngine;

namespace Battrail.Racing.Input
{
    /// Racer が毎 FixedUpdate に読む入力源。人間のデバイス入力（RacerInput）と
    /// NPC の思考（AiRacerInput）を同じ口で扱うための抽象。オンライン化時のリモート入力もここに来る。
    public interface IRacerInput
    {
        Vector2 ReadMove();
        bool ReadBoost();
    }
}
