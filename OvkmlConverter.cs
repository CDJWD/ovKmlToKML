using System.Globalization;
using System.Text;
using System.Xml;

namespace OvkmlToKml;

public static class OvkmlConverter
{
    internal static ConvertResult Convert(string inputPath, string outputPath, ConvertOptions options)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("找不到输入文件。", inputPath);
        }

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var doc = new XmlDocument { PreserveWhitespace = false };
        doc.Load(inputPath);

        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("kml", "http://www.opengis.net/kml/2.2");
        nsmgr.AddNamespace("gx", "http://www.google.com/kml/ext/2.2");

        var result = new ConvertResult();
        var usedSystems = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        if (options.ConvertCoordinates)
        {
            ConvertCoordinateNodes(doc, nsmgr, options, result, usedSystems);
            ConvertGxCoordNodes(doc, nsmgr, options, result, usedSystems);
            ConvertLookAtNodes(doc, nsmgr, options, usedSystems);
        }

        RemoveAoweiExtensionNodes(doc.DocumentElement);
        result.CoordSystems = usedSystems.Count == 0 ? "未标注" : string.Join(", ", usedSystems);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            NewLineChars = "\n",
            OmitXmlDeclaration = false
        };

        using var writer = XmlWriter.Create(outputPath, settings);
        doc.Save(writer);

        return result;
    }

    /// <summary>
    /// 将 ovkml 转为 kml。源坐标系默认按 OvCoordType 自动识别。
    /// </summary>
    public static void Convert(string inputPath, string outputPath)
    {
        Convert(inputPath, outputPath, new ConvertOptions());
    }

    private static void ConvertCoordinateNodes(
        XmlDocument doc,
        XmlNamespaceManager nsmgr,
        ConvertOptions options,
        ConvertResult result,
        SortedSet<string> usedSystems)
    {
        var nodes = SelectNodes(doc, nsmgr, "//kml:coordinates", "//coordinates");
        foreach (XmlNode node in nodes)
        {
            var source = ResolveSource(node, options, usedSystems);
            var text = node.InnerText?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var pairs = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var rebuilt = new StringBuilder();
            foreach (var pair in pairs)
            {
                rebuilt.Append(ConvertLonLatAltToken(pair, ',', source, result));
                rebuilt.Append(' ');
            }

            node.InnerText = rebuilt.ToString().Trim();
        }
    }

    private static void ConvertGxCoordNodes(
        XmlDocument doc,
        XmlNamespaceManager nsmgr,
        ConvertOptions options,
        ConvertResult result,
        SortedSet<string> usedSystems)
    {
        var nodes = SelectNodes(doc, nsmgr, "//gx:coord", "//coord");
        foreach (XmlNode node in nodes)
        {
            if (!string.Equals(node.LocalName, "coord", StringComparison.Ordinal))
            {
                continue;
            }

            var source = ResolveSource(node, options, usedSystems);
            var text = node.InnerText?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            node.InnerText = ConvertLonLatAltToken(text, ' ', source, result).Trim();
        }
    }

    private static void ConvertLookAtNodes(
        XmlDocument doc,
        XmlNamespaceManager nsmgr,
        ConvertOptions options,
        SortedSet<string> usedSystems)
    {
        var views = SelectNodes(doc, nsmgr, "//kml:LookAt", "//LookAt", "//kml:Camera", "//Camera");
        foreach (XmlNode view in views)
        {
            XmlNode? lonNode = null;
            XmlNode? latNode = null;
            foreach (XmlNode child in view.ChildNodes)
            {
                if (child.LocalName == "longitude") lonNode = child;
                if (child.LocalName == "latitude") latNode = child;
            }

            if (lonNode is null || latNode is null)
            {
                continue;
            }

            if (!TryParseDouble(lonNode.InnerText, out var lon) ||
                !TryParseDouble(latNode.InnerText, out var lat))
            {
                continue;
            }

            var source = ResolveSource(view, options, usedSystems);
            var converted = CoordinateConverter.ToWgs84(new GeoPoint(lon, lat), source);
            lonNode.InnerText = FormatNumber(converted.Lon);
            latNode.InnerText = FormatNumber(converted.Lat);
        }
    }

    private static string ConvertLonLatAltToken(string token, char separator, CoordSystem source, ConvertResult result)
    {
        var parts = token.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 ||
            !TryParseDouble(parts[0], out var lon) ||
            !TryParseDouble(parts[1], out var lat))
        {
            return token;
        }

        result.PointCount++;
        var converted = CoordinateConverter.ToWgs84(new GeoPoint(lon, lat), source);
        if (converted.Lon != lon || converted.Lat != lat)
        {
            result.ConvertedCount++;
        }

        var alt = parts.Length >= 3 ? parts[2] : "0";
        return separator == ','
            ? $"{FormatNumber(converted.Lon)},{FormatNumber(converted.Lat)},{alt}"
            : $"{FormatNumber(converted.Lon)} {FormatNumber(converted.Lat)} {alt}";
    }

    private static CoordSystem ResolveSource(XmlNode node, ConvertOptions options, SortedSet<string> usedSystems)
    {
        CoordSystem source;
        if (options.ForcedSource is { } forced && forced != CoordSystem.Auto)
        {
            source = forced;
        }
        else
        {
            source = FindOvCoordType(node) ?? CoordSystem.Wgs84;
        }

        usedSystems.Add(CoordSystemParser.ToDisplay(source));
        return source;
    }

    private static CoordSystem? FindOvCoordType(XmlNode node)
    {
        for (XmlNode? current = node; current is not null; current = current.ParentNode)
        {
            foreach (XmlNode child in current.ChildNodes)
            {
                if (child.LocalName == "OvCoordType")
                {
                    return CoordSystemParser.Parse(child.InnerText) ?? CoordSystem.Wgs84;
                }
            }
        }

        return null;
    }

    private static void RemoveAoweiExtensionNodes(XmlNode? node)
    {
        if (node is null)
        {
            return;
        }

        var toRemove = new List<XmlNode>();
        foreach (XmlNode child in node.ChildNodes)
        {
            if (child.NodeType == XmlNodeType.Element && child.LocalName.StartsWith("Ov", StringComparison.Ordinal))
            {
                toRemove.Add(child);
            }
            else
            {
                RemoveAoweiExtensionNodes(child);
            }
        }

        foreach (var child in toRemove)
        {
            node.RemoveChild(child);
        }
    }

    private static XmlNodeList SelectNodes(XmlDocument doc, XmlNamespaceManager nsmgr, params string[] xpaths)
    {
        foreach (var xpath in xpaths)
        {
            var nodes = doc.SelectNodes(xpath, nsmgr);
            if (nodes is { Count: > 0 })
            {
                return nodes;
            }
        }

        return doc.SelectNodes("/nonexistent")!;
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string FormatNumber(double value)
    {
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }
}
