using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;


namespace AST_Resource_Monitor.Services
{
    internal class EmailSender: IEmailSender
    {
        private readonly MailSettings _mailSettings;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IOptions<MailSettings> options, ILogger<EmailSender> logger)
        {
            _mailSettings = options.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(List<string> emails, string subject, string htmlMessage)
        {
            foreach (var email in emails)
            {
                await SendEmailAsync(email, subject, htmlMessage);
            }
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            int maxRetries = 3;
            int attempt = 0;
            Console.WriteLine("Host: "+_mailSettings.Server);
            Console.WriteLine("Username: "+_mailSettings.Username);
            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;

                    // Create the email message
                    var emailMessage = new MimeMessage();
                    var emailFrom = new MailboxAddress(_mailSettings.SenderName, _mailSettings.SenderEmail);
                    emailMessage.From.Add(emailFrom);

                    var emailTo = new MailboxAddress(email, email);
                    emailMessage.To.Add(emailTo);

                    emailMessage.Subject = subject;

                    var emailBodyBuilder = new BodyBuilder
                    {
                        HtmlBody = htmlMessage
                    };
                    emailMessage.Body = emailBodyBuilder.ToMessageBody();

                    // Use the MailKit.Net.Smtp.SmtpClient for SMTP operations
                    using (var mailClient = new SmtpClient())
                    {
                        await mailClient.ConnectAsync(_mailSettings.Server, _mailSettings.Port, MailKit.Security.SecureSocketOptions.StartTls);
                        await mailClient.AuthenticateAsync(_mailSettings.Username, _mailSettings.Password);
                        await mailClient.SendAsync(emailMessage);
                        await mailClient.DisconnectAsync(true);
                    }

                    _logger.LogInformation($"Email sent successfully to {email}.");
                    break; // Exit the retry loop on success
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send email to {email}. Attempt {attempt} of {maxRetries}.");

                    if (attempt >= maxRetries)
                    {
                        _logger.LogCritical($"Email sending failed after {maxRetries} attempts for recipient {email}. Detailed exception: {ex}");
                        //throw; // Re-throw the exception if all retries fail
                    }

                    await Task.Delay(2000); // Wait 2 seconds before retrying
                }
            }
        }
    }
    public class MailSettings
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseSSL { get; set; } = false;
    }
}
