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
            Assert.DoesNotContain(request.Headers.Accept, value =>
                value.Parameters.Any(parameter => parameter.Name == "version"));

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

    [Fact]
    public async Task Upload_supports_png_with_legacy_paperless_task_responses()
    {
        var requests = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
        requests.Enqueue(request =>
        {
            Assert.DoesNotContain(request.Headers.Accept, value =>
                value.Parameters.Any(parameter => parameter.Name == "version"));
            var form = Assert.IsType<MultipartFormDataContent>(request.Content);
            var document = Assert.Single(form, part => part.Headers.ContentDisposition?.Name == "document");
            Assert.Equal("receipt.png", document.Headers.ContentDisposition?.FileName);
            Assert.Equal("image/png", document.Headers.ContentType?.MediaType);
            return JsonResponse("\"task-png\"");
        });
        requests.Enqueue(_ => JsonResponse("""
            [{"status":"SUCCESS","related_document":"4821"}]
            """));
        using var httpClient = new HttpClient(new QueueMessageHandler(requests));
        var client = new PaperlessDocumentClient(httpClient);
        await using var content = new MemoryStream([137, 80, 78, 71]);

        var documentId = await client.UploadAsync(
            new PaperlessConnection(new Uri("https://paperless.example.test/"), "secret-token"),
            content,
            "receipt.png",
            "image/png",
            "Washing machine",
            CancellationToken.None);

        Assert.Equal("4821", documentId);
        Assert.Empty(requests);
    }

    [Fact]
    public async Task GetThumbnail_requests_the_documents_thumbnail_and_returns_its_bytes_and_content_type()
    {
        var requests = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
        requests.Enqueue(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://paperless.example.test/api/documents/4821/thumb/", request.RequestUri?.AbsoluteUri);
            Assert.Equal(new AuthenticationHeaderValue("Token", "secret-token"), request.Headers.Authorization);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3]),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return response;
        });
        using var httpClient = new HttpClient(new QueueMessageHandler(requests));
        var client = new PaperlessDocumentClient(httpClient);

        var thumbnail = await client.GetThumbnailAsync(
            new PaperlessConnection(new Uri("https://paperless.example.test/"), "secret-token"),
            "4821",
            CancellationToken.None);

        Assert.Equal("image/png", thumbnail.ContentType);
        using var buffer = new MemoryStream();
        await thumbnail.Content.CopyToAsync(buffer);
        Assert.Equal(new byte[] { 1, 2, 3 }, buffer.ToArray());
    }

    [Fact]
    public async Task GetThumbnail_reports_a_failed_response_as_a_paperless_upload_failure()
    {
        var requests = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
        requests.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(new QueueMessageHandler(requests));
        var client = new PaperlessDocumentClient(httpClient);

        var exception = await Assert.ThrowsAsync<PaperlessUploadException>(() => client.GetThumbnailAsync(
            new PaperlessConnection(new Uri("https://paperless.example.test/"), "secret-token"),
            "4821",
            CancellationToken.None));

        Assert.Equal("Paperless-ngx could not return the document thumbnail.", exception.Message);
    }

    [Fact]
    public async Task GetPreview_requests_the_documents_original_file_and_returns_its_bytes_and_content_type()
    {
        var requests = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
        requests.Enqueue(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://paperless.example.test/api/documents/4821/download/?original=true", request.RequestUri?.AbsoluteUri);
            Assert.Equal(new AuthenticationHeaderValue("Token", "secret-token"), request.Headers.Authorization);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4]),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return response;
        });
        using var httpClient = new HttpClient(new QueueMessageHandler(requests));
        var client = new PaperlessDocumentClient(httpClient);

        var preview = await client.GetPreviewAsync(
            new PaperlessConnection(new Uri("https://paperless.example.test/"), "secret-token"),
            "4821",
            CancellationToken.None);

        Assert.Equal("image/png", preview.ContentType);
        using var buffer = new MemoryStream();
        await preview.Content.CopyToAsync(buffer);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer.ToArray());
    }

    [Fact]
    public async Task GetPreview_reports_a_failed_response_as_a_paperless_upload_failure()
    {
        var requests = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>();
        requests.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var httpClient = new HttpClient(new QueueMessageHandler(requests));
        var client = new PaperlessDocumentClient(httpClient);

        var exception = await Assert.ThrowsAsync<PaperlessUploadException>(() => client.GetPreviewAsync(
            new PaperlessConnection(new Uri("https://paperless.example.test/"), "secret-token"),
            "4821",
            CancellationToken.None));

        Assert.Equal("Paperless-ngx could not return the document preview.", exception.Message);
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