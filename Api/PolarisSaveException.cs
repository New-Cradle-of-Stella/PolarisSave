using System;

namespace Polaris.Save
{
    /// <summary>
    /// PolarisSave 抛出的全部错误：注册非法、字段类型不符、超出格式上限、容器损坏。
    /// 保存路径上抛出它会拦下本次落盘，读取路径上抛出它会隔离对应分区。
    /// </summary>
    public sealed class PolarisSaveException : Exception
    {
        public PolarisSaveException(string message)
            : base(message)
        {
        }

        public PolarisSaveException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
