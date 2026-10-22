using System.IO;
using System.Net.Sockets;
using System.Text.Encodings.Web;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BaseMVC.WebUI.Email;

public sealed class SmtpEmailSender(IOptions<EmailOptions> emailOptions) : IEmailSender
{
    public async Task SendPasswordResetAsync(
        string recipient,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        var options = emailOptions.Value;
        options.Validate();

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName ?? "BaseMVC", options.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = "Đặt lại mật khẩu BaseMVC";
        var encodedUrl = HtmlEncoder.Default.Encode(resetUrl);
        message.Body = new BodyBuilder
        {
            TextBody = $"Để đặt lại mật khẩu, mở liên kết sau: {resetUrl}\n\n"
                + "Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.",
            HtmlBody = $"<p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản BaseMVC.</p>"
                + $"<p><a href=\"{encodedUrl}\">Đặt lại mật khẩu</a></p>"
                + "<p>Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này.</p>"
        }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(
                options.SmtpHost,
                options.SmtpPort,
                options.UseSslOnConnect ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(options.SmtpUsername))
            {
                await client.AuthenticateAsync(
                    options.SmtpUsername,
                    options.SmtpPassword,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception) when (
            exception is MailKit.CommandException
                or MailKit.ProtocolException
                or MailKit.ServiceNotAuthenticatedException
                or MailKit.ServiceNotConnectedException
                or MailKit.Security.AuthenticationException
                or MailKit.Security.SaslException
                or MailKit.Security.SslHandshakeException
                or IOException
                or SocketException
                or System.Security.Authentication.AuthenticationException)
        {
            throw new EmailDeliveryException("Password-reset email could not be delivered.", exception);
        }
    }
}
