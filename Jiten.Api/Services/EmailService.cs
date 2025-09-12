using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;

namespace Jiten.Api.Services;

public class EmailService : IEmailSender
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var message = new MimeMessage();
        var fromName = "Jiten";
        var fromEmail = _configuration["Email:From"] ?? "noreply@example.com";
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlMessage, TextBody = StripHtml(htmlMessage) };
        message.Body = builder.ToMessageBody();


        await SendViaSmtp(message,
                          host: _configuration["Email:SmtpHost"] ?? "smtp.eu.mailgun.org",
                          port: int.TryParse(_configuration["Email:SmtpPort"], out var sp) ? sp : 587,
                          username: _configuration["Email:Username"],
                          password: _configuration["Email:Password"],
                          useStartTls: true);
    }

    private static async Task SendViaSmtp(MimeMessage message, string host, int port, string? username, string? password, bool useStartTls)
    {
        using var client = new SmtpClient();
        var secure = useStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(host, port, secure);

        if (!string.IsNullOrWhiteSpace(username))
        {
            await client.AuthenticateAsync(username, password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        var sb = new StringBuilder(html.Length);
        bool inside = false;
        foreach (var ch in html)
        {
            if (ch == '<') inside = true;
            else if (ch == '>') inside = false;
            else if (!inside) sb.Append(ch);
        }

        return sb.ToString();
    }
}