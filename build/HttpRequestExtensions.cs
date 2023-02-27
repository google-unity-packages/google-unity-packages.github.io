using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public static class HttpRequestExtensions
{
    private static string TimeoutPropertyKey = "RequestTimeout";

    public static void SetTimeout(
        this HttpRequestMessage request,
        TimeSpan? timeout)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        request.Properties[TimeoutPropertyKey] = timeout;
    }

    public static TimeSpan? GetTimeout(this HttpRequestMessage request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.Properties.TryGetValue(
                TimeoutPropertyKey,
                out var value)
            && value is TimeSpan timeout)
            return timeout;
        return null;
    }
}

class TimeoutHandler : DelegatingHandler
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(100);

    protected async override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using (var cts = GetCancellationTokenSource(request, cancellationToken))
        {
            try
            {
                return await base.SendAsync(
                    request,
                    cts?.Token ?? cancellationToken);
            }
            catch(OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException();
            }
        }
    }
    
    private CancellationTokenSource GetCancellationTokenSource(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var timeout = request.GetTimeout() ?? DefaultTimeout;
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            // No need to create a CTS if there's no timeout
            return null;
        }
        else
        {
            var cts = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            return cts;
        }
    }
}