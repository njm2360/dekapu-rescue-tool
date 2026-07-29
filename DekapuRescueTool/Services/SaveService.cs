using System.Net;
using System.Net.Http;

namespace DekapuRescueTool.Services;

public class SaveService : ISaveService
{
    // ソケット枯渇を避けるためインスタンスを使い回す
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", $"{AppInfo.Name}/{AppInfo.Version}" } }
    };

    public async Task<SaveResult> SaveAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync(url, ct);
            // サーバーは登録済みのURLに409を返す
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
