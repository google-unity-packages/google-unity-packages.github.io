using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

partial class Build
{
    static async Task<string> DownloadString(string url, CancellationToken ct)
    {
        using var http = new HttpClient();
        http.Timeout = Timeout.InfiniteTimeSpan;
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.SetTimeout(TimeSpan.FromSeconds(30));
        var response = await http.SendAsync(request, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    static async Task<Stream> DownloadFile(string url, CancellationToken ct)
    {
        using var http = new HttpClient();
        http.Timeout = Timeout.InfiniteTimeSpan;
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.SetTimeout(TimeSpan.FromMinutes(1));
        var response = await http.SendAsync(request, ct);
        return await response.Content.ReadAsStreamAsync(ct);
    }
}