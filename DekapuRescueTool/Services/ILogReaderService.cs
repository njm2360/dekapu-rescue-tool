using DekapuRescueTool.Models;

namespace DekapuRescueTool.Services;

public record LogReadResult(
    IReadOnlyDictionary<string, List<SaveEntry>> Entries,
    int UnknownVersionCount);

public interface ILogReaderService
{
    LogReadResult ReadAllLogs(string directory);
}
