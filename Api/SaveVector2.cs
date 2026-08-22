namespace Polaris.Save
{
    /// <summary>
    /// 存档里的二维坐标。公开 API 不引用 <c>UnityEngine.Vector2</c>，因为存档内核要能脱离 Unity
    /// 独立编译；游戏运行时目标里补了与 <c>Vector2</c>、<c>GameVector2</c> 的隐式互转，模组按平时
    /// 的写法传值即可。
    /// </summary>
    public readonly partial struct SaveVector2
    {
        public SaveVector2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }

        public float Y { get; }

        public static SaveVector2 Zero => new SaveVector2(0f, 0f);

        public override string ToString() => $"({X:0.###}, {Y:0.###})";

        public override bool Equals(object obj) =>
            obj is SaveVector2 other && other.X.Equals(X) && other.Y.Equals(Y);

        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();

        public static bool operator ==(SaveVector2 left, SaveVector2 right) => left.Equals(right);

        public static bool operator !=(SaveVector2 left, SaveVector2 right) => !left.Equals(right);
    }
}
