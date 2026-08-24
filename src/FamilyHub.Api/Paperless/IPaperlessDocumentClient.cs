namespace FamilyHub.Api.Paperless;

public interface IPaperlessDocumentClient
{
    Task<string> UploadAsync(
        PaperlessConnection connection,
        Stream content,
        string fileName,
        string contentType,
        string title,
        CancellationToken cancellationToken);
}

public record PaperlessConnection(Uri BaseUrl, string ApiToken);