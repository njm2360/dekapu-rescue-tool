namespace DekapuRescueTool.Services;

public enum SaveResult { Success, Duplicate, Error }

public interface ISaveService
{
    Task<SaveResult> SaveAsync(string url, CancellationToken ct = default);
}
