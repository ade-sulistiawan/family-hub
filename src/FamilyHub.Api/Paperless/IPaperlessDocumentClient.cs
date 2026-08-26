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

    Task<PaperlessThumbnail> GetThumbnailAsync(
        PaperlessConnection connection,
        string documentExternalId,
        CancellationToken cancellationToken);

    Task<PaperlessThumbnail> GetPreviewAsync(
        PaperlessConnection connection,
        string documentExternalId,
        CancellationToken cancellationToken);
}

public record PaperlessConnection(Uri BaseUrl, string ApiToken);

public record PaperlessThumbnail(Stream Content, string ContentType);