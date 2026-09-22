using System.Reflection;

namespace OvkmlToKml;

internal static class Program
{
    private const int ExitOk = 0;
    private const int ExitError = 1;
    private const int ExitUsage = 2;

    private static int Main(string[] args)
    {
        try
        {
            var options = CliOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintHelp();
                return ExitOk;
            }

            if (options.ShowVersion)
            {
                Console.WriteLine(GetVersion());
                return ExitOk;
            }

            return Run(options);
        }
        catch (CliUsageException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("使用 --help 查看用法。");
            return ExitUsage;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"错误: {ex.Message}");
            return ExitError;
        }
    }

    private static int Run(CliOptions options)
    {
        var inputs = CollectInputs(options);
        if (inputs.Count == 0)
        {
            Console.Error.WriteLine("未找到 .ovkml 文件。");
            return ExitError;
        }

        int failed = 0;
        int succeeded = 0;

        foreach (var input in inputs)
        {
            var output = ResolveOutputPath(input, options);
            try
            {
                var result = OvkmlConverter.Convert(input, output, options.ToConvertOptions());
                succeeded++;
                if (!options.Quiet)
                {
                    Console.WriteLine($"{input}");
                    Console.WriteLine($"  -> {output}");
                    Console.WriteLine($"     坐标点 {result.PointCount}，已转换 {result.ConvertedCount}，坐标系 {result.CoordSystems}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Console.Error.WriteLine($"失败: {input}");
                Console.Error.WriteLine($"  {ex.Message}");
            }
        }

        if (!options.Quiet)
        {
            Console.WriteLine($"完成：成功 {succeeded}，失败 {failed}。");
        }

        return failed == 0 ? ExitOk : ExitError;
    }

    private static List<string> CollectInputs(CliOptions options)
    {
        var files = new List<string>();
        foreach (var path in options.Inputs)
        {
            if (File.Exists(path))
            {
                if (!path.EndsWith(".ovkml", StringComparison.OrdinalIgnoreCase))
                {
                    throw new CliUsageException($"不是 .ovkml 文件: {path}");
                }

                files.Add(Path.GetFullPath(path));
                continue;
            }

            if (Directory.Exists(path))
            {
                var search = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                files.AddRange(Directory.GetFiles(Path.GetFullPath(path), "*.ovkml", search));
                continue;
            }

            throw new CliUsageException($"找不到文件或目录: {path}");
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    private static string ResolveOutputPath(string inputFile, CliOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Output))
        {
            var output = Path.GetFullPath(options.Output);
            if (Directory.Exists(output) || options.Inputs.Count > 1 || Directory.Exists(options.Inputs[0]))
            {
                var name = Path.ChangeExtension(Path.GetFileName(inputFile), ".kml");
                if (options.Recursive && Directory.Exists(options.Inputs[0]))
                {
                    var root = Path.GetFullPath(options.Inputs[0]);
                    var relative = Path.GetRelativePath(root, inputFile);
                    return Path.Combine(output, Path.ChangeExtension(relative, ".kml"));
                }

                return Path.Combine(output, name);
            }

            return output;
        }

        return Path.ChangeExtension(inputFile, ".kml");
    }

    private static string GetVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private static void PrintHelp()
    {
        var name = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "OvkmlToKml";
        Console.WriteLine("OvkmlToKml — 将奥维地图 OVKML 转为标准 KML（WGS84）");
        Console.WriteLine();
        Console.WriteLine("用法:");
        Console.WriteLine($"  {name} <input.ovkml> [output.kml]");
        Console.WriteLine($"  {name} <目录> [-o 输出目录] [-r]");
        Console.WriteLine($"  {name} [选项] <文件或目录>...");
        Console.WriteLine();
        Console.WriteLine("选项:");
        Console.WriteLine("  -o, --output <路径>   输出文件或目录（默认与输入同名、扩展名为 .kml）");
        Console.WriteLine("  -r, --recursive       递归处理目录中的 .ovkml");
        Console.WriteLine("      --crs <值>        强制源坐标系: auto, gcj02, bd09, wgs84, cgcs2000");
        Console.WriteLine("                        默认 auto，按每个地标的 OvCoordType 识别");
        Console.WriteLine("      --no-coord-convert  不转换坐标，仅去掉奥维扩展标签");
        Console.WriteLine("  -q, --quiet           减少输出");
        Console.WriteLine("  -h, --help            显示帮助");
        Console.WriteLine("      --version         显示版本");
        Console.WriteLine();
        Console.WriteLine("说明:");
        Console.WriteLine("  GCJ-02 / BD-09 会转为 WGS84；WGS84 与 CGCS2000 保持不变。");
    }
}

internal sealed class CliUsageException : Exception
{
    public CliUsageException(string message) : base(message) { }
}

internal sealed class CliOptions
{
    public List<string> Inputs { get; } = new();
    public string? Output { get; private set; }
    public bool Recursive { get; private set; }
    public bool Quiet { get; private set; }
    public bool ConvertCoordinates { get; private set; } = true;
    public CoordSystem? ForcedSource { get; private set; }
    public bool ShowHelp { get; private set; }
    public bool ShowVersion { get; private set; }

    public ConvertOptions ToConvertOptions() => new()
    {
        ConvertCoordinates = ConvertCoordinates,
        ForcedSource = ForcedSource
    };

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        if (args.Length == 0)
        {
            options.ShowHelp = true;
            return options;
        }

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h":
                case "--help":
                case "/?":
                    options.ShowHelp = true;
                    return options;
                case "--version":
                    options.ShowVersion = true;
                    return options;
                case "-q":
                case "--quiet":
                    options.Quiet = true;
                    break;
                case "-r":
                case "--recursive":
                    options.Recursive = true;
                    break;
                case "--no-coord-convert":
                    options.ConvertCoordinates = false;
                    break;
                case "-o":
                case "--output":
                    options.Output = RequireValue(args, ref i, arg);
                    break;
                case "--crs":
                    options.ForcedSource = ParseCrs(RequireValue(args, ref i, arg));
                    break;
                default:
                    if (arg.StartsWith('-'))
                    {
                        throw new CliUsageException($"未知选项: {arg}");
                    }

                    options.Inputs.Add(arg);
                    break;
            }
        }

        if (options.Inputs.Count == 0)
        {
            throw new CliUsageException("请指定输入文件或目录。");
        }

        // 允许：ovkml2kml in.ovkml out.kml
        if (options.Output is null && options.Inputs.Count == 2
            && options.Inputs[1].EndsWith(".kml", StringComparison.OrdinalIgnoreCase)
            && !Directory.Exists(options.Inputs[1]))
        {
            options.Output = options.Inputs[1];
            options.Inputs.RemoveAt(1);
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int i, string option)
    {
        if (i + 1 >= args.Length)
        {
            throw new CliUsageException($"选项 {option} 需要参数。");
        }

        return args[++i];
    }

    private static CoordSystem ParseCrs(string value)
    {
        if (value.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return CoordSystem.Auto;
        }

        return CoordSystemParser.Parse(value)
            ?? throw new CliUsageException($"无法识别的坐标系: {value}");
    }
}

internal static class CoordSystemParser
{
    public static CoordSystem? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToUpperInvariant()
            .Replace('_', '-')
            .Replace(" ", string.Empty);

        return normalized switch
        {
            "AUTO" => CoordSystem.Auto,
            "GCJ-02" or "GCJ02" or "GCJ" or "MARS" => CoordSystem.Gcj02,
            "BD-09" or "BD09" or "BAIDU" or "BD" => CoordSystem.Bd09,
            "WGS84" or "WGS-84" or "WGS1984" or "GPS" => CoordSystem.Wgs84,
            "CGCS2000" or "CGCS-2000" or "2000" => CoordSystem.Cgcs2000,
            _ => null
        };
    }

    public static string ToDisplay(CoordSystem system) => system switch
    {
        CoordSystem.Gcj02 => "GCJ-02",
        CoordSystem.Bd09 => "BD-09",
        CoordSystem.Wgs84 => "WGS84",
        CoordSystem.Cgcs2000 => "CGCS2000",
        _ => "auto"
    };
}

internal enum CoordSystem
{
    Auto,
    Gcj02,
    Bd09,
    Wgs84,
    Cgcs2000
}

internal sealed class ConvertOptions
{
    public bool ConvertCoordinates { get; init; } = true;
    public CoordSystem? ForcedSource { get; init; }
}

internal sealed class ConvertResult
{
    public int PointCount { get; set; }
    public int ConvertedCount { get; set; }
    public string CoordSystems { get; set; } = "未知";
}
