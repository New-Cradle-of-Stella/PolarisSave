# PolarisSave 实现方案

> 目标：在现有 PolarisSave 模块中提供独立的模组存档能力。
> 参考 RimWorld 的只是“同一份声明同时用于保存和读取”这个思路，API 名称和调用方式使用
> Polaris 自己的设计。

## 1. 原版保存行径

反编译目标：

- Alice In Cradle 0.29，Unity 2022.3.62f2。
- Assembly-CSharp.dll SHA-256：
  C15AE0207DE38ACC80F055C219411B855BF8AE76B395234AEA046AAADB0248D9。

### 手动保存

~~~text
UiSVD.executeSave(index)
  → COOK.createNewSave(Sf, M2D, save_cfg)
  → COOK.createBinary(null, Sf, M2D, true, save_cfg)
  → SVD.saveBinary(Sf, ByteArray)
~~~

### 自动保存

~~~text
COOK.autoSave(M2D, is_bench, force)
  → COOK.createBinary(null, CurFile, M2D)
  → SVD.saveBinary(CurFile, ByteArray)
~~~

所以手动保存和自动保存的共同序列化入口是 COOK.createBinary，共同落盘入口是
SVD.saveBinary。

SVD.saveBinary 的落盘步骤：

1. 写 temporary_savedata_XX.aicsave。
2. 删除旧 savedata_XX.aicsave。
3. 将临时文件移动为正式存档。

存档目录来自 Application.persistentDataPath，文件名为 savedata_00.aicsave 到
savedata_99.aicsave，其中 00 是自动存档。

### 读取

~~~text
COOK.initGameScene(M2D)
  → SVD.loadFileContent(CurFile)
  → COOK.readBinaryContent(ByteArray, Sf, M2D)
~~~

当前原版存档内容版本是 40。COOK.readBinaryContent 最后读取：

~~~text
version >= 37  CaneManager
version >= 38  Tips
version >= 39  UiHotel
version >= 40  last_enter_transfer
~~~

读取结束后直接返回，不检查 ByteArray 是否正好到 EOF。因此 PolarisSave 可以把自己的容器
追加在原版数据末尾，原版仍能正常读取。

SVD.changeOnlyMemo 会重写 header，但把 thumb_position 到 EOF 的内容原样复制，所以修改
存档备注不会丢掉尾部容器。

## 2. 总体方案

PolarisSave 不修改原版版本号，也不侵入原版各子系统的二进制布局，而是在存档末尾追加一个
统一容器：

~~~text
+---------------------------+
| 原版 .aicsave 数据         |
+---------------------------+
| PolarisSave 容器           |
|   模组 A 分区              |
|   模组 B 分区              |
|   未安装模组的保留分区     |
+---------------------------+
~~~

每个模组通过稳定字符串 ID 注册一个或多个分区，例如：

~~~text
com.example.alice-mod/world
com.example.alice-mod/player
~~~

分区互相隔离，并各自带 schema 版本、长度和 CRC。某个模组没有安装时，PolarisSave 不解析
它的分区，只保存原始字节，并在下一次存档时原样写回。

## 3. Polaris API

### 注册

~~~csharp
public static class PolarisSaveAPI
{
    public static SaveHandle<T> Register<T>(
        string id,
        ushort version = 1)
        where T : class, IPolarisSaveData, new();

    public static SaveHandle<T> Register<T>(
        string id,
        ushort version,
        Func<T> factory)
        where T : class, IPolarisSaveData;
}

public sealed class SaveHandle<T>
{
    public string Id { get; }
    public ushort Version { get; }
    public T Current { get; }
    public bool WasLoaded { get; }
}
~~~

- id 必须全局唯一，建议使用 BepInEx GUID/分区名。
- 重复 ID 直接拒绝，不能后注册覆盖先注册。
- 注册在第一次新游戏、读档或保存开始时冻结。
- Current 在新游戏和每次读档时都会换成新实例，避免携带上一局状态。

### 自定义数据接口

~~~csharp
public interface IPolarisSaveData
{
    void Serialize(SaveArchive archive);
}
~~~

SaveArchive 是一次保存或读取操作的显式上下文，不使用 RimWorld 那种全局 Scribe 状态：

~~~csharp
public enum SaveArchiveMode
{
    Writing,
    Reading,
    AfterLoad
}

public sealed class SaveArchive
{
    public SaveArchiveMode Mode { get; }
    public ushort StoredVersion { get; }

    public void Member<T>(string key, ref T value, T fallback = default);

    public void Child<T>(string key, ref T value)
        where T : class, IPolarisSaveData, new();

    public void ValueList<T>(string key, ref List<T> values);

    public void ChildList<T>(string key, ref List<T> values)
        where T : class, IPolarisSaveData, new();

    public void ValueMap<T>(string key, ref Dictionary<string, T> values);
}
~~~

Member 在正式实现中为受支持类型提供强类型重载或受控泛型入口。V1 支持：

- bool 和所有整数类型。
- float、double、decimal。
- string、char、byte[]、Guid、DateTime。
- enum。
- Vector2。

不支持任意对象、委托、System.Type、UnityEngine.Object 或自动 CLR 类型反序列化。

### 使用示例

~~~csharp
public sealed class PlayerExtraData : IPolarisSaveData
{
    public int Energy;

    public void Serialize(SaveArchive archive)
    {
        archive.Member("energy", ref Energy, fallback: 0);
    }
}

public sealed class MyWorldData : IPolarisSaveData
{
    public int Counter;
    public string LastMap = "";
    public List<int> Scores = new();
    public PlayerExtraData Player = new();

    public void Serialize(SaveArchive archive)
    {
        archive.Member("counter", ref Counter, fallback: 0);
        archive.Member("lastMap", ref LastMap, fallback: "");
        archive.ValueList("scores", ref Scores);
        archive.Child("player", ref Player);

        if (archive.Mode == SaveArchiveMode.AfterLoad)
        {
            Scores ??= new List<int>();
            Player ??= new PlayerExtraData();

            if (archive.StoredVersion < 2)
            {
                Counter *= 2;
            }
        }
    }
}

public static class MyModSave
{
    public static readonly SaveHandle<MyWorldData> World =
        PolarisSaveAPI.Register<MyWorldData>(
            "com.example.my-mod/world",
            version: 2);
}

MyModSave.World.Current.Counter++;
~~~

Serialize 的执行时机：

1. 保存时以 Writing 调用一次。
2. 读档时以 Reading 调用一次。
3. 全部分区读取完后，以 AfterLoad 再调用一次，用于迁移和补默认集合。

字段 key 和分区 ID 发布后不能随意改名。缺少字段时使用 fallback；字段类型变化必须显式
迁移，不能做字符串到数字之类的宽松转换。

## 4. 尾部容器格式

所有整数使用大端，字符串使用 UTF-8：

~~~text
ContainerHeader
  8 bytes  magic             = "POLARSAV"
  u16      formatVersion     = 1
  u16      headerSize        = 16
  u32      partitionCount

Partition * partitionCount
  u16      idLength
  bytes    id
  u16      schemaVersion
  u16      flags             = 0
  u32      payloadLength
  u32      payloadCrc32
  bytes    payload

ContainerFooter
  u32      containerLength
  u32      containerCrc32
  8 bytes  endMagic          = "PLSVEND!"
~~~

分区 payload 使用 UTF-8 JSON object，但 JSON 只属于内部实现，不暴露 JObject/JToken 给模组。
必须设置 TypeNameHandling.None，禁止从存档构造任意 CLR 类型。

采用 JSON 是因为字段按 key 读取，新增或删除字段不会让后续内容发生字节错位。容器外层仍用
二进制 framing 和 CRC，便于从 EOF 定位并检测截断。

限制：

| 项目 | 上限 |
| --- | ---: |
| 分区数量 | 1024 |
| 单分区 | 4 MiB |
| 整个容器 | 16 MiB |
| ID 和字段 key | 128 UTF-8 字节 |
| 对象嵌套 | 64 层 |

所有长度必须在分配内存前校验。

## 5. Harmony 接入点

| 目标 | 补丁 | 用途 |
| --- | --- | --- |
| COOK.createBinary(ByteArray, SVD.sFile, NelM2DBase, bool, bool) | Prefix + Postfix | 开始保存状态；在原版数据后追加容器 |
| SVD.saveBinary(SVD.sFile, ByteArray) | Prefix | Polaris 序列化失败时阻止覆盖原存档 |
| COOK.readBinaryContent(ByteArray, SVD.sFile, NelM2DBase) | Postfix | 原版读取成功后加载尾部容器 |
| COOK.newGame(NelM2DBase, bool) | Postfix | 建立全新分区实例并清除上一局数据 |

COOK.readBinaryContent 是 private，需要 PolarisSave 像 PolarisLang 一样导入运行时 props，
使用项目现有 Publicizer。

Core 已经在相同方法上发布 SaveSerialized、SaveLoaded 和 NewGameStarted 回调。执行顺序必须
保证：

1. PolarisSave 追加完成后再发布 SaveSerialized。
2. PolarisSave 加载完成后再发布 SaveLoaded。
3. PolarisSave 重置完成后再发布 NewGameStarted。

使用显式 Harmony priority 并写集成测试，不依赖程序集扫描顺序。

## 6. 兼容与失败策略

| 情况 | 处理 |
| --- | --- |
| 老存档没有 Polaris 容器 | 所有分区使用新实例默认值 |
| 某个内容模组未安装 | 其分区作为 opaque 字节原样保留 |
| 分区缺少新字段 | 使用 fallback |
| 分区 schema 较旧 | Reading 后在 AfterLoad 中迁移 |
| 分区 schema 比当前模组新 | 不解析，不允许保存覆盖 |
| 单分区损坏 | 原版世界继续加载；隔离该分区；进入只读恢复状态 |
| 整个容器损坏 | 原版世界继续加载；不发布模组数据；进入只读恢复状态 |
| 模组保存时抛异常 | 本次 SVD.saveBinary 被拦截，原存档不覆盖 |
| 原版读档失败 | 不加载尾部数据，随 COOK.newGame 重置 |

只读恢复状态下不能普通保存，以免用默认值覆盖仍可人工恢复的数据。后续可以提供明确的诊断
操作，让用户选择丢弃某个坏分区或整个容器；不能自动丢弃。

需要明确告知玩家：如果完全卸载 PolarisSave 后用原版重新保存，原版会重建文件并永久丢失
尾部模组数据。只读档、不再次保存则不影响原版兼容性。

## 7. 内部结构

~~~text
PolarisSave/
  Api/             注册、SaveHandle、SaveArchive、IPolarisSaveData
  Serialization/   基本值、深对象、集合和 JSON 文档
  Format/          容器 reader/writer、CRC32、长度校验
  Runtime/         注册表、会话状态、opaque 分区、失败门
  Integration/     四个原版 Harmony 补丁
  Tests/           codec、序列化和程序集签名测试
~~~

公开 API 不暴露 nel、PixelLiner、Harmony 或 JSON.NET 类型。只有 Integration 层可以直接引用
COOK、SVD 和 ByteArray。

## 8. 最短实施顺序

1. **容器与序列化：**实现 SaveArchive、JSON 文档、二进制容器、CRC 和单元测试。
2. **生命周期：**实现分区注册、Current 更换、opaque 保留和 schema 迁移。
3. **原版接入：**实现四个补丁以及保存失败门。
4. **游戏验收：**测试手动保存、自动保存、备注修改、模组卸载后再保存、损坏分区和无
   Polaris 原版读取。

实现前只需额外用一个真实 v40 存档确认三点：

- readBinaryContent 返回时的 position 与原版内容末尾一致。
- PolarisSave 与 Core 回调补丁的实际执行顺序。
- changeOnlyMemo 在真实文件上完整保留 EOF。
