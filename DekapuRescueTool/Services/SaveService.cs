using System.Net;
using System.Net.Http;

namespace DekapuRescueTool.Services;

public class SaveService : ISaveService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "DekapuRescueTool/1.0" } }
    };

    public async Task<SaveResult> SaveAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync(url, ct);
            return response.StatusCode switch
            {
                HttpStatusCode.OK => SaveResult.Success,
                HttpStatusCode.Conflict => SaveResult.Duplicate,
                _ => SaveResult.Error
            };
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return SaveResult.NetworkError;
        }
        catch
        {
            return SaveResult.Error;
        }
    }
}
