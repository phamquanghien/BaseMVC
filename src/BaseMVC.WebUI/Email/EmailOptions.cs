using MimeKit;

namespace BaseMVC.WebUI.Email;

public sealed class EmailOptions
{
    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public string SmtpUsername { get; set; } = string.Empty;

    public string SmtpPassword { get; set; } = string.Empty;

    public bool UseSslOnConnect { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string? FromName { get; set; }

    public string PublicBaseUrl { get; set; } = string.Empty;

    public Uri Validate()
    {
        if (string.IsNullOrWhiteSpace(SmtpHost))
        {
            throw new InvalidOperationException("Email SMTP host is not configured.");
        }

        if (SmtpPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("Email SMTP port must be between 1 and 65535.");
        }

        if (!MailboxAddress.TryParse(FromAddress, out _))
        {
            throw new InvalidOperationException("A valid email sender address is required.");
        }

        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var publicBaseUri)
            || publicBaseUri.Scheme is not ("https" or "http")
            || !string.IsNullOrEmpty(publicBaseUri.UserInfo))
        {
            throw new InvalidOperationException(
                "Email public base URL must be an absolute HTTP or HTTPS URL without credentials.");
        }

        if (string.IsNullOrWhiteSpace(SmtpUsername) != string.IsNullOrWhiteSpace(SmtpPassword))
        {
            throw new InvalidOperationException(
                "SMTP username and password must either both be configured or both be empty.");
        }

        return publicBaseUri;
    }
}
