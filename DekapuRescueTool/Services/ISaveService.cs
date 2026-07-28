namespace DekapuRescueTool.Services;

public enum SaveResult { Success, Duplicate, NetworkError, Error }

public interface ISaveService
{
    Task<SaveResult> SaveAsync(string url, CancellationToken ct = default);
}
