using DekapuRescueTool.Models;

namespace DekapuRescueTool.Services;

public interface ILogReaderService
{
    IReadOnlyDictionary<string, List<SaveEntry>> ReadAllLogs(string directory);
}
