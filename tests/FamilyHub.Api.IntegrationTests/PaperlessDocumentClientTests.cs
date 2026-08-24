using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FamilyHub.Api.Paperless;

namespace FamilyHub.Api.IntegrationTests;

public class PaperlessDocumentClientTests
{
    [Fact]
    public async Task Upload_sends_the_document_and_returns_the_processed_document_id()
    {
        var requests = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
        requests.Enqueue(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://paperless.example.test/api/documents/post_document/", request.RequestUri?.AbsoluteUri);
            Assert.Equal(new AuthenticationHeaderValue("Token", "secret-token"), request.Headers.Authorization);
            Assert.Contains(request.Headers.Accept, value =>
                value.MediaType == "application/json"
                && value.Parameters.Any(parameter => parameter.Name == "version" && parameter.Value == "10"));

            var form = Assert.IsType<MultipartFormDataContent>(request.Content);
            var title = Assert.Single(form, part => part.Headers.ContentDisposition?.Name == "title");
            Assert.Equal("Washing machine", title.ReadAsStringAsync().GetAwaiter().GetResult());
            var document = Assert.Single(form, part => part.Headers.ContentDisposition?.Name == "document");
            Assert.Equal("receipt.jpg", document.Headers.ContentDisposition?.FileName);
            Assert.Equal("image/jpeg", document.Headers.ContentType?.MediaType);
            Assert.Equal("receipt-image", document.ReadAsStringAsync().GetAwaiter().GetResult());

            return JsonResponse("\"task-123\"");
        });
        requests.Enqueue(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://paperless.example.test/api/tasks/?task_id=task-123", request.RequestUri?.AbsoluteUri);
            return JsonResponse("""
                {"results":[{"status":"success","result_data":{"document_id":4821}}]}
                """);
        });
        using var httpClient = new HttpClient(new QueueMessageHandler(requests));
        var client = new PaperlessDocumentClient(httpClient);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("receipt-image"));

        var documentId = await client.UploadAsync(
            new PaperlessConnection(new Uri("https://paperless.example.test/"), "secret-token"),
            content,
            "receipt.jpg",
            "image/jpeg",
            "Washing machine",
            CancellationToken.None);

        Assert.Equal("4821", documentId);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task Upload_reports_a_transport_failure_as_a_paperless_upload_failure()
    {
        using var httpClient = new HttpClient(new ThrowingMessageHandler());
        var client = new PaperlessDocumentClient(httpClient);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("receipt-image"));

        var exception = await Assert.ThrowsAsync<PaperlessUploadException>(() => client.UploadAsync(
            new PaperlessConnection(new Uri("https://paperless.example.test/"), "secret-token"),
            content,
            "receipt.jpg",
            "image/jpeg",
            "Washing machine",
            CancellationToken.None));

        Assert.Equal("Paperless-ngx could not be reached.", exception.Message);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class QueueMessageHandler(Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.NotEmpty(responses);
            return Task.FromResult(responses.Dequeue()(request));
        }
    }

    private sealed class ThrowingMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused.");
    }
}