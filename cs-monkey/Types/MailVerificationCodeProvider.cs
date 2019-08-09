using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace cs_monkey.Types
{
    /// <summary>
    /// Logs in to an e-mail server via IMAP and checks for the e-mail sent by the cryptshare system
    /// which contains the verification code
    /// </summary>
    public class MailVerificationCodeProvider : IVerificationCodeProvider
    {
        ImapConfig Config { get; }

        TimeSpan initialWaitTime = TimeSpan.FromSeconds(10);
        TimeSpan retryWaitTime = TimeSpan.FromSeconds(30);
        TimeSpan timeout = TimeSpan.FromMinutes(3);

        public MailVerificationCodeProvider(ImapConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<string> GetVerificationCode()
        {
            int started = Environment.TickCount;

            // email should not be older than 1 minute
            DateTime border = DateTime.Now.Subtract(TimeSpan.FromMinutes(1));

            await Task.Delay(initialWaitTime);

            string vcode = null;

            try
            {
                ImapClient client = new ImapClient();
                await client.ConnectAsync(Config.Host, Config.Port, MailKit.Security.SecureSocketOptions.Auto);
                await client.AuthenticateAsync(Config.Username, Config.Password);

                var inbox = await client.GetFolderAsync(Config.ImapFolderPath);

                List<MimeMessage> msgList = null;
                MimeKit.MimeMessage mail = null;                

                while (mail == null)
                {                    
                    await inbox.OpenAsync(FolderAccess.ReadWrite);
                    // we need a list to determine the message's index reliably
                    msgList = inbox.ToList();
                    mail = msgList.FirstOrDefault(x => x.From.Mailboxes.Any(f => f.Address.ToLower().Contains(Config.VerificationFromAddress.ToLower())) && x.Subject.Contains(Config.VerificationMailSubject) && x.Date >= border);
                    if (mail != null)
                        break;

                    // wait then retry
                    await inbox.CloseAsync();
                    if (Environment.TickCount - started > timeout.TotalMilliseconds)
                        throw new TimeoutException();
                    await Task.Delay(retryWaitTime);
                }

                string msg_content = mail.TextBody;
                vcode = ExtractVerificationCode(msg_content);

                // since we are going to use the VerificationCode, delete the message
                if (msgList != null)
                {
                    int midx = msgList.IndexOf(mail);
                    inbox.AddFlags(midx, MessageFlags.Deleted, true);
                    await inbox.ExpungeAsync();
                }

                await inbox.CloseAsync();

                await client.DisconnectAsync(true);
            }
            catch (Exception e)
            {
                // maybe we still got the verification code
            }

            // return what we have
            return vcode;
        }

        private string ExtractVerificationCode(string mail_content)
        {
            const string marker = "Bitte kopieren Sie den folgenden Code in die Zwischenablage und fügen Sie ihn in der Verifizierungsmaske Ihres Browsers ein, um fortzufahren:";
            int pos = mail_content.IndexOf(marker);
            if (pos == -1) return null;

            int startIdx = pos + marker.Length;
            int endIdx = mail_content.IndexOfAny(new char[] { '\r', '\n' }, pos);
            if (endIdx == -1) return null;

            string code = mail_content.Substring(startIdx, endIdx - startIdx)?.Trim();
            return code;
        } 
    }

    public class ImapConfig
    {
        public string Host { get; }
        public int Port { get; }
        public string Username { get; }
        public string Password { get; }
        public string ImapFolderPath { get; set; } = "Inbox";
        public string VerificationFromAddress { get; set; }

        public string VerificationMailSubject { get; set; } = "Cryptshare Verifizierung";

        public ImapConfig(string host, int port, string username, string password, string verificationFromAddress)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Port = port;
            Username = username ?? throw new ArgumentNullException(nameof(username));
            Password = password ?? throw new ArgumentNullException(nameof(password));
            VerificationFromAddress = verificationFromAddress ?? throw new ArgumentNullException(nameof(verificationFromAddress));
        }
    }
}
