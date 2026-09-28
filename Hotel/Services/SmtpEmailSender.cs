using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Hotel.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;

    public SmtpEmailSender(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) ||
            string.IsNullOrWhiteSpace(_settings.FromAddress) ||
            (_settings.UseAuthentication &&
             (string.IsNullOrWhiteSpace(_settings.UserName) ||
              string.IsNullOrWhiteSpace(_settings.Password))) ||
            _settings.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "SMTP email settings are incomplete. Configure Email:Host, Email:Port, Email:FromAddress, and credentials when authentication is enabled.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _settings.Host,
            _settings.Port,
            _settings.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None);
        if (_settings.UseAuthentication)
            await client.AuthenticateAsync(_settings.UserName, _settings.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
