using System.Buffers.Text;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DekapuRescueTool.Models;

namespace DekapuRescueTool.Services;

public class LogReaderService : ILogReaderService
{
    private const string UrlMarker = "push.trap.games";

    private static readonly Regex UrlPattern = new(
        @"https://push\.trap\.games/api/v(\d+)/data\S*", RegexOptions.Compiled);

    private readonly record struct FoundUrl(string Url, int Version);

    public LogReadResult ReadAllLogs(string directory)
    {
        var result = new Dictionary<string, List<SaveEntry>>(StringComparer.Ordinal);
        var seenUrls = new HashSet<string>(StringComparer.Ordinal);
        var unknownVersions = 0;

        if (!Directory.Exists(directory))
            return new LogReadResult(result, unknownVersions);

        var logFiles = Directory.GetFiles(directory, "output_log_*.txt")
            .OrderBy(f => f)
            .ToArray();

        var perFile = new List<FoundUrl>[logFiles.Length];
        Parallel.For(0, logFiles.Length, i =>
        {
            perFile[i] = CollectUrls(logFiles[i]);
        });

        var uniqueUrls = new List<FoundUrl>();
        foreach (var found in perFile)
            foreach (var item in found)
                if (seenUrls.Add(item.Url))
                    uniqueUrls.Add(item);

        var parsed = new ((string UserId, SaveEntry Entry)? Hit, bool UnknownVersion)[uniqueUrls.Count];
        Parallel.For(0, uniqueUrls.Count, i =>
        {
            var hit = TryParseUrl(uniqueUrls[i].Url, uniqueUrls[i].Version, out var unknownVersion);
            parsed[i] = (hit, unknownVersion);
        });

        foreach (var (hit, unknownVersion) in parsed)
        {
            if (unknownVersion)
            {
                unknownVersions++;
                continue;
            }
            if (hit == null) continue;

            var (userId, entry) = hit.Value;
            if (!result.TryGetValue(userId, out var list))
            {
                list = new List<SaveEntry>();
                result[userId] = list;
            }
            list.Add(entry);
        }

        return new LogReadResult(result, unknownVersions);
    }

    private static List<FoundUrl> CollectUrls(string filePath)
    {
        var found = new List<FoundUrl>();
        var seenUrls = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                1 << 20, FileOptions.SequentialScan);
            using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1 << 16);

            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                if (!line.Contains(UrlMarker, StringComparison.Ordinal)) continue;

                var urlMatch = UrlPattern.Match(line);
                if (!urlMatch.Success) continue;

                var rawUrl = urlMatch.Value;
                if (!seenUrls.Add(rawUrl)) continue;

                found.Add(new FoundUrl(rawUrl, int.Parse(urlMatch.Groups[1].Value)));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { }

        return found;
    }

    private static (string UserId, SaveEntry Entry)? TryParseUrl(string rawUrl, int version, out bool unknownVersion)
    {
        unknownVersion = false;
        try
        {
            var uri = new Uri(rawUrl);
            var query = ParseQuery(uri.Query);

            if (!query.TryGetValue("user_id", out var rawUserId) ||
                !query.TryGetValue("data", out var dataStr))
                return null;

            var userId = DecodeUserId(rawUserId);

            string? json = version switch
            {
                3 => dataStr,
                4 => DecodeBase64Url(dataStr),
                _ => null
            };
            if (json == null)
            {
                unknownVersion = true;
                return null;
            }

            var saveData = JsonSerializer.Deserialize<SaveDataJson>(json);
            if (saveData == null) return null;

            if (!long.TryParse(saveData.CreditAll, out var creditAll)) return null;
            if (!long.TryParse(saveData.LastSave, out var lastSaveUnix)) return null;

            var entryTime = DateTimeOffset.FromUnixTimeSeconds(lastSaveUnix).LocalDateTime;

            return (userId, new SaveEntry
            {
                Url = rawUrl,
                CreditAll = creditAll,
                SaveTime = entryTime
            });
        }
        catch
        {
            return null;
        }
    }

    private static string DecodeUserId(string encoded)
    {
        try
        {
            return DecodeBase64Url(encoded);
        }
        catch (FormatException)
        {
            return encoded;
        }
    }

    private static string DecodeBase64Url(string encoded)
        => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded));

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.TrimStart('?').Split('&'))
        {
            var idx = part.IndexOf('=');
            if (idx < 0) continue;
            result[Uri.UnescapeDataString(part[..idx])] = Uri.UnescapeDataString(part[(idx + 1)..]);
        }
        return result;
    }
}
