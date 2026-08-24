using FamilyHub.Api.Paperless;

namespace FamilyHub.Api.IntegrationTests;

public class FakePaperlessDocumentClient : IPaperlessDocumentClient
{
    public async Task<string> UploadAsync(
        PaperlessConnection connection,
        Stream content,
        string fileName,
        string contentType,
        string title,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (connection.BaseUrl != new Uri("https://paperless.example.test/")
            || connection.ApiToken != "secret-paperless-token"
            || fileName != "receipt.png"
            || contentType != "image/png"
            || title != "Washing machine"
            || body != "receipt-image")
        {
            throw new PaperlessUploadException("Unexpected Paperless upload request.");
        }

        return "4821";
    }
}