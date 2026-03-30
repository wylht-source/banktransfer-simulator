namespace BankingApi.Domain.Entities;

public class LoanDocument
{
    public Guid     Id                { get; private set; }
    public Guid     LoanId            { get; private set; }
    public string   UploadedByUserId  { get; private set; } = null!;
    public string   BlobPath          { get; private set; } = null!;
    public string   OriginalFileName  { get; private set; } = null!;
    public string   ContentType       { get; private set; } = null!;
    public long     SizeBytes         { get; private set; }
    public DateTime UploadedAt        { get; private set; }
    public string?  DocumentType      { get; private set; }   // nullable — Client informs or AI detects later
    public string   StorageProvider   { get; private set; } = null!;
    public string?  Sha256            { get; private set; }   // nullable — computed on upload

    // EF Core constructor
    private LoanDocument() { }

    public LoanDocument(
        Guid loanId,
        string uploadedByUserId,
        string blobPath,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string? documentType,
        string? sha256,
        string storageProvider = "AzureBlob")
    {
        Id               = Guid.NewGuid();
        LoanId           = loanId;
        UploadedByUserId = uploadedByUserId;
        BlobPath         = blobPath;
        OriginalFileName = originalFileName;
        ContentType      = contentType;
        SizeBytes        = sizeBytes;
        UploadedAt       = DateTime.UtcNow;
        DocumentType     = documentType;
        StorageProvider  = storageProvider;
        Sha256           = sha256;
    }
}
