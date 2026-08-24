# Delegate document storage to Paperless-ngx

Warranty & Receipt Archiver does not store scanned receipt/warranty files itself. It stores a Document Reference (external ID) into an existing Paperless-ngx instance and calls its token-authenticated API to fetch the file. Chosen to avoid rebuilding document storage, OCR, and search that Paperless-ngx already provides and that's already in use for other household documents.
