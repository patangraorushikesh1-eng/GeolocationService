using System.Net;

namespace GeolocationService.Tests.Fakes;

public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _factory;
    private int _callCount;

    public StubHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> factory)
    {
        _factory = factory;
    }

    public StubHttpMessageHandler(params HttpResponseMessage[] responses)
        : this((_, i) => responses[Math.Min(i, responses.Length - 1)]) { }

    public int CallCount => _callCount;
    public List<HttpRequestMessage> Requests { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        var idx = Interlocked.Increment(ref _callCount) - 1;
        return Task.FromResult(_factory(request, idx));
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
}
