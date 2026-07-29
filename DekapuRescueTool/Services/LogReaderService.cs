using System.Buffers.Text;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DekapuRescueTool.Models;

namespace DekapuRescueTool.Services;

public class LogReaderService : ILogReaderService
{
    private static readonly Regex UrlPattern = new(
        @"https://push\.trap\.games/api/v(\d+)/data\S*", RegexOptions.Compiled);


    public LogReadResult ReadAllLogs(string directory)
    {
        var result = new Dictionary<string, List<SaveEntry>>(StringComparer.Ordinal);
        // URLのクエリはbase64urlを含むため大文字小文字を区別して比較する
        var seenUrls = new HashSet<string>(StringComparer.Ordinal);
        var unknownVersions = 0;

        if (!Directory.Exists(directory))
            return new LogReadResult(result, unknownVersions);

        var logFiles = Directory.GetFiles(directory, "output_log_*.txt")
            .OrderBy(f => f)
            .ToArray();

        foreach (var file in logFiles)
        {
            try
            {
                ProcessFile(file, result, seenUrls, ref unknownVersions);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // 読めないファイルはスキップして残りを処理する
            }
        }

        return new LogReadResult(result, unknownVersions);
    }

    private static void ProcessFile(
        string filePath,
        Dictionary<string, List<SaveEntry>> result,
        HashSet<string> seenUrls,
        ref int unknownVersions)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);

        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            var urlMatch = UrlPattern.Match(line);
            if (!urlMatch.Success) continue;

            var rawUrl = urlMatch.Value;
            if (!seenUrls.Add(rawUrl)) continue;

            var version = int.Parse(urlMatch.Groups[1].Value);
            var parsed = TryParseUrl(rawUrl, version, out var unknownVersion);
            if (unknownVersion) unknownVersions++;
            if (parsed == null) continue;

            var (userId, entry) = parsed.Value;
            if (!result.TryGetValue(userId, out var list))
            {
                list = new List<SaveEntry>();
                result[userId] = list;
            }
            list.Add(entry);
        }
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
