using System.Net.Http.Headers;
using System.Text.Json;

namespace FamilyHub.Api.Paperless;

public class PaperlessDocumentClient(HttpClient httpClient) : IPaperlessDocumentClient
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromSeconds(60);

    public async Task<string> UploadAsync(
        PaperlessConnection connection,
        Stream content,
        string fileName,
        string contentType,
        string title,
        CancellationToken cancellationToken)
    {
        try
        {
            var taskId = await StartUploadAsync(
                connection,
                content,
                fileName,
                contentType,
                title,
                cancellationToken);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(ProcessingTimeout);

            while (true)
            {
                var result = await GetTaskResultAsync(connection, taskId, timeoutSource.Token);
                if (result.DocumentId is not null)
                {
                    return result.DocumentId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                if (result.IsFailed)
                {
                    throw new PaperlessUploadException("Paperless-ngx could not process the uploaded document.");
                }

                await Task.Delay(PollInterval, timeoutSource.Token);
            }
        }
        catch (PaperlessUploadException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new PaperlessUploadException("Paperless-ngx could not be reached.", exception);
        }
        catch (JsonException exception)
        {
            throw new PaperlessUploadException("Paperless-ngx returned an invalid response.", exception);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PaperlessUploadException("Paperless-ngx did not finish processing the document in time.");
        }
    }

    private async Task<string> StartUploadAsync(
        PaperlessConnection connection,
        Stream content,
        string fileName,
        string contentType,
        string title,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(connection, HttpMethod.Post, "api/documents/post_document/");
        using var form = new MultipartFormDataContent();
        var documentContent = new StreamContent(content);
        documentContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(documentContent, "document", fileName);
        form.Add(new StringContent(title), "title");
        request.Content = form;

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaperlessUploadException("Paperless-ngx rejected the document upload.");
        }

        var taskId = await response.Content.ReadFromJsonAsync<string>(cancellationToken);
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new PaperlessUploadException("Paperless-ngx did not return an upload task identifier.");
        }

        return taskId;
    }

    private async Task<PaperlessTaskResult> GetTaskResultAsync(
        PaperlessConnection connection,
        string taskId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            connection,
            HttpMethod.Get,
            $"api/tasks/?task_id={Uri.EscapeDataString(taskId)}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new PaperlessUploadException("Paperless-ngx upload status could not be read.");
        }

        using var payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var tasks = payload.RootElement.ValueKind == JsonValueKind.Array
            ? payload.RootElement
            : payload.RootElement.TryGetProperty("results", out var results)
                ? results
                : default;
        if (tasks.ValueKind != JsonValueKind.Array || tasks.GetArrayLength() == 0)
        {
            return new PaperlessTaskResult(null, false);
        }

        var task = tasks[0];
        var status = task.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()
            : null;
        if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            if (task.TryGetProperty("result_data", out var resultData)
                && resultData.ValueKind == JsonValueKind.Object)
            {
                if (TryGetDocumentId(resultData, "document_id", out var documentId))
                {
                    return new PaperlessTaskResult(documentId, false);
                }

                if (TryGetDocumentId(resultData, "duplicate_of", out var duplicateId))
                {
                    return new PaperlessTaskResult(duplicateId, false);
                }
            }

            if (TryGetDocumentId(task, "related_document", out var relatedDocumentId))
            {
                return new PaperlessTaskResult(relatedDocumentId, false);
            }
        }

        var isFailed = status is not null
            && (status.Equals("failure", StringComparison.OrdinalIgnoreCase)
                || status.Equals("revoked", StringComparison.OrdinalIgnoreCase));
        return new PaperlessTaskResult(null, isFailed);
    }

    private static bool TryGetDocumentId(JsonElement container, string propertyName, out int documentId)
    {
        documentId = default;
        if (!container.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out documentId),
            JsonValueKind.String => int.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out documentId),
            _ => false,
        };
    }

    private static HttpRequestMessage CreateRequest(
        PaperlessConnection connection,
        HttpMethod method,
        string relativeUrl)
    {
        var request = new HttpRequestMessage(method, new Uri(connection.BaseUrl, relativeUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", connection.ApiToken);
        return request;
    }

    private sealed record PaperlessTaskResult(int? DocumentId, bool IsFailed);
}

public class PaperlessUploadException : Exception
{
    public PaperlessUploadException(string message)
        : base(message)
    {
    }

    public PaperlessUploadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}