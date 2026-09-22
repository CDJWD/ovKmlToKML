# OvkmlToKml

将[奥维互动地图](https://www.ovital.com/)导出的 **OVKML** 转为 Google Earth / GIS 可用的标准 **KML（WGS84）**。

仓库：<https://github.com/CDJWD/ovKmlToKML>

```bash
git clone https://github.com/CDJWD/ovKmlToKML.git
cd ovKmlToKML
```

奥维文件本质是 KML，但带有 `OvCoordType` 等私有标签，坐标还可能是 GCJ-02（火星）或 BD-09（百度）。本工具会：

- 去掉奥维扩展标签
- 按每个地标的 `OvCoordType` 把 GCJ-02 / BD-09 转为 WGS84
- WGS84、CGCS2000 坐标保持不变（CGCS2000 与 WGS84 差异在厘米级）

## 环境

- [.NET 8 SDK](https://dotnet.microsoft.com/download)

## 用法

```bash
# 单文件：输出与输入同目录、同名 .kml
dotnet run -- input.ovkml

# 指定输出文件
dotnet run -- input.ovkml output.kml
dotnet run -- input.ovkml -o output.kml

# 批量转换目录
dotnet run -- ./maps -o ./out

# 递归
dotnet run -- ./maps -r -o ./out

# 强制源坐标系（忽略 OvCoordType）
dotnet run -- input.ovkml --crs gcj02

# 只去奥维标签，不改坐标
dotnet run -- input.ovkml --no-coord-convert
```

发布后也可直接运行：

```bash
dotnet publish -c Release -o publish
./publish/OvkmlToKml --help
```

或安装为 .NET 全局工具：

```bash
dotnet pack -c Release
dotnet tool install --global --add-source ./nupkg OvkmlToKml
ovkml2kml input.ovkml
```

## 坐标系

| `OvCoordType` | 处理 |
| --- | --- |
| `GCJ-02` | 转为 WGS84 |
| `BD-09` | 转为 WGS84 |
| `WGS84` | 原样写出 |
| `CGCS2000` | 按 WGS84 原样写出 |

`--crs` 可选：`auto`（默认）、`gcj02`、`bd09`、`wgs84`、`cgcs2000`。

本地测试用的 ovkml 可放在 `data/`，该目录已忽略，不会提交到 GitHub。

## 示例

仓库 `samples/example.ovkml` 含一个 GCJ-02 点和一个 WGS84 点：

```bash
dotnet run -- samples/example.ovkml -o samples/example.out.kml
```

## 许可

MIT
