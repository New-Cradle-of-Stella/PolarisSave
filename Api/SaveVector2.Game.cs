using Polaris.API;
using UnityEngine;

namespace Polaris.Save
{
    /// <summary>游戏运行时目标专属：把 <see cref="SaveVector2"/> 接到引擎类型和 Core 的 <see cref="GameVector2"/> 上。</summary>
    public readonly partial struct SaveVector2
    {
        public static implicit operator Vector2(SaveVector2 v) => new Vector2(v.X, v.Y);

        public static implicit operator SaveVector2(Vector2 v) => new SaveVector2(v.x, v.y);

        public static implicit operator GameVector2(SaveVector2 v) => new GameVector2(v.X, v.Y);

        public static implicit operator SaveVector2(GameVector2 v) => new SaveVector2(v.X, v.Y);
    }
}
