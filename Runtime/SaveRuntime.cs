using System;
using System.Collections.Generic;
using Polaris.Save.Format;

namespace Polaris.Save.Runtime
{
    /// <summary>
    /// 注册表 + 会话状态 + 失败门。整套逻辑不碰任何游戏类型，所以能被单元测试完整驱动；
    /// Integration 层只负责把原版的字节流递进来、把结果递出去。
    /// </summary>
    internal sealed class SaveRuntime
    {
        internal static SaveRuntime Instance { get; } = new SaveRuntime();

        readonly object gate = new object();
        readonly List<ISaveHandle> ordered = new List<ISaveHandle>();
        readonly Dictionary<string, ISaveHandle> byId = new Dictionary<string, ISaveHandle>(StringComparer.Ordinal);

        /// <summary>未安装模组的分区，以及被隔离的分区：只保存原始字节，下次存档原样写回。</summary>
        readonly List<SavePartitionRecord> preserved = new List<SavePartitionRecord>();

        readonly List<SaveIssue> issues = new List<SaveIssue>();

        /// <summary>本次读档中真正读成功的分区及其存档内版本，用于 AfterLoad 迁移。</summary>
        readonly Dictionary<string, ushort> loadedVersions = new Dictionary<string, ushort>(StringComparer.Ordinal);

        bool frozen;

        internal bool IsFrozen
        {
            get { lock (gate) { return frozen; } }
        }

        internal IReadOnlyList<SaveIssue> Issues
        {
            get { lock (gate) { return issues.ToArray(); } }
        }

        /// <summary>磁盘上还有人工可恢复的数据，本次会话拒绝普通保存。</summary>
        internal bool IsReadOnlyRecovery
        {
            get
            {
                lock (gate)
                {
                    foreach (SaveIssue issue in issues)
                    {
                        if (issue.BlocksSaving)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        internal SaveHandle<T> Register<T>(string id, ushort version, Func<T> factory)
            where T : class, IPolarisSaveData
        {
            SavePartitionId.Validate(id);

            if (version == 0)
            {
                throw new PolarisSaveException($"分区 {id} 的 schema 版本必须从 1 开始。");
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            lock (gate)
            {
                if (frozen)
                {
                    throw new PolarisSaveException(
                        $"分区 {id} 注册得太晚了。注册在第一次新游戏、读档或保存开始时就已冻结，"
                        + "请在组件的 Awake/Start 阶段（或静态字段初始化时）完成注册。");
                }

                // 先注册的赢：后注册的若能覆盖，两个模组用同一个 ID 时谁拿到数据就取决于加载顺序。
                if (byId.ContainsKey(id))
                {
                    throw new PolarisSaveException($"分区 ID \"{id}\" 已经被注册过了。");
                }

                var handle = new SaveHandle<T>(id, version, factory);
                byId.Add(id, handle);
                ordered.Add(handle);
                return handle;
            }
        }

        internal void Freeze()
        {
            lock (gate)
            {
                frozen = true;
            }
        }

        /// <summary>新游戏：全部分区换新实例，丢掉上一局的数据、保留字节和问题记录。</summary>
        internal void ResetForNewGame()
        {
            lock (gate)
            {
                frozen = true;
                ResetLocked();
            }
        }

        /// <summary>
        /// 读档：把原版存档的完整字节交进来，定位尾部容器并分发。任何情况下都不抛异常——
        /// 原版世界已经加载好了，模组数据出问题只该让本模组进恢复状态，不该掀掉整局游戏。
        /// </summary>
        /// <param name="data">存档全部字节。</param>
        /// <param name="length">有效字节数。</param>
        /// <returns>容器在字节流中的起点；没有容器或容器不可解析时为 -1。</returns>
        internal int Load(byte[] data, int length)
        {
            lock (gate)
            {
                frozen = true;
                ResetLocked();

                SaveContainerReadResult result = SaveContainerReader.Read(data, length);
                switch (result.Status)
                {
                    case SaveContainerStatus.Absent:
                        break;

                    case SaveContainerStatus.UnsupportedFormat:
                        AddIssueLocked(SaveIssueKind.UnsupportedFormat, null, result.Message, true);
                        break;

                    case SaveContainerStatus.Corrupt:
                        AddIssueLocked(SaveIssueKind.ContainerCorrupt, null, result.Message, true);
                        break;

                    default:
                        if (result.Status == SaveContainerStatus.PartiallyCorrupt)
                        {
                            AddIssueLocked(SaveIssueKind.ContainerCorrupt, null, result.Message, true);
                        }

                        foreach (SavePartitionRecord record in result.Partitions)
                        {
                            ApplyPartitionLocked(record);
                        }

                        break;
                }

                RunAfterLoadLocked(result);
                return result.ContainerStart;
            }
        }

        /// <summary>保存：生成要追加到原版数据后面的容器字节。失败一律抛异常，由上层拦下本次落盘。</summary>
        internal byte[] BuildContainer()
        {
            lock (gate)
            {
                frozen = true;

                if (IsReadOnlyRecoveryLocked())
                {
                    throw new PolarisSaveException(
                        "PolarisSave 处于只读恢复状态，拒绝保存，以免用默认值覆盖仍可人工恢复的数据。"
                        + $"问题：{string.Join("；", DescribeIssuesLocked())}");
                }

                var records = new List<SavePartitionRecord>(ordered.Count + preserved.Count);
                foreach (ISaveHandle handle in ordered)
                {
                    records.Add(new SavePartitionRecord(handle.Id, handle.Version, 0, handle.WritePayload()));
                }

                records.AddRange(preserved);

                // 按 ID 排序：同样的内存状态每次都写出同样的字节，与模组加载顺序无关。
                records.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
                return SaveContainerWriter.Write(records);
            }
        }

        /// <summary>明确丢弃一个坏分区。只能由用户显式触发，PolarisSave 绝不自动丢数据。</summary>
        internal bool DiscardPartition(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            lock (gate)
            {
                bool changed = preserved.RemoveAll(record => string.Equals(record.Id, id, StringComparison.Ordinal)) > 0;
                changed |= issues.RemoveAll(issue => string.Equals(issue.PartitionId, id, StringComparison.Ordinal)) > 0;

                if (byId.TryGetValue(id, out ISaveHandle handle))
                {
                    handle.ResetToDefault();
                    changed = true;
                }

                return changed;
            }
        }

        /// <summary>明确丢弃整个尾部容器的全部模组数据，回到"这局没有模组存档"的状态。</summary>
        internal bool DiscardAllModData()
        {
            lock (gate)
            {
                bool changed = preserved.Count != 0 || issues.Count != 0;
                ResetLocked();
                return changed;
            }
        }

        // ── 内部

        void ResetLocked()
        {
            foreach (ISaveHandle handle in ordered)
            {
                handle.ResetToDefault();
            }

            preserved.Clear();
            issues.Clear();
            loadedVersions.Clear();
        }

        void ApplyPartitionLocked(SavePartitionRecord record)
        {
            if (record.PayloadDamaged)
            {
                preserved.Add(record);
                AddIssueLocked(SaveIssueKind.PartitionCorrupt, record.Id, "分区 payload 的 CRC 校验失败。", true);
                return;
            }

            // 模组没装：原样保留字节，下次存档写回去，卸载再装回来数据还在。
            if (!byId.TryGetValue(record.Id, out ISaveHandle handle))
            {
                preserved.Add(record);
                return;
            }

            if (record.SchemaVersion > handle.Version)
            {
                preserved.Add(record);
                AddIssueLocked(
                    SaveIssueKind.PartitionTooNew,
                    record.Id,
                    $"存档里的 schema 版本 {record.SchemaVersion} 高于当前模组的 {handle.Version}。",
                    true);
                return;
            }

            try
            {
                handle.ReadPayload(record.Payload, record.SchemaVersion);
                loadedVersions[record.Id] = record.SchemaVersion;
            }
            catch (Exception ex)
            {
                handle.ResetToDefault();
                preserved.Add(record);
                AddIssueLocked(SaveIssueKind.PartitionReadFailed, record.Id, ex.Message, true);
            }
        }

        void RunAfterLoadLocked(SaveContainerReadResult result)
        {
            foreach (ISaveHandle handle in ordered)
            {
                bool hadStoredData = loadedVersions.TryGetValue(handle.Id, out ushort storedVersion);
                if (!hadStoredData)
                {
                    // 没读到分区就把 StoredVersion 报成当前版本，免得"全新存档"误触发版本迁移分支。
                    storedVersion = handle.Version;
                }

                try
                {
                    handle.RunAfterLoad(storedVersion);
                }
                catch (Exception ex)
                {
                    if (hadStoredData)
                    {
                        handle.ResetToDefault();
                        PreserveOriginalLocked(result, handle.Id);
                    }

                    // 磁盘上本来就没有这个分区时，迁移失败没有可恢复的数据要保护，不必封锁保存。
                    AddIssueLocked(SaveIssueKind.MigrationFailed, handle.Id, ex.Message, hadStoredData);
                }
            }
        }

        void PreserveOriginalLocked(SaveContainerReadResult result, string id)
        {
            foreach (SavePartitionRecord record in result.Partitions)
            {
                if (string.Equals(record.Id, id, StringComparison.Ordinal))
                {
                    preserved.Add(record);
                    return;
                }
            }
        }

        void AddIssueLocked(SaveIssueKind kind, string partitionId, string message, bool blocksSaving) =>
            issues.Add(new SaveIssue(kind, partitionId, message ?? "（无附加信息）", blocksSaving));

        bool IsReadOnlyRecoveryLocked()
        {
            foreach (SaveIssue issue in issues)
            {
                if (issue.BlocksSaving)
                {
                    return true;
                }
            }

            return false;
        }

        IEnumerable<string> DescribeIssuesLocked()
        {
            var described = new List<string>(issues.Count);
            foreach (SaveIssue issue in issues)
            {
                described.Add(issue.ToString());
            }

            return described;
        }
    }
}
