using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using Syrilium.CommonInterface;
using System.Text.RegularExpressions;

namespace Syrilium.Common
{
    public class Mail : IMail
    {
        public string RegexCheck { get { return @"^([\w\!\#$\%\&\'\*\+\-\/\=\?\^\`{\|\}\~]+\.)*[\w\!\#$\%\&\'\*\+\-\/\=\?\^\`{\|\}\~]+@((((([a-zA-Z0-9]{1}[a-zA-Z0-9\-]{0,62}[a-zA-Z0-9]{1})|[a-zA-Z])\.)+[a-zA-Z]{2,6})|(\d{1,3}\.){3}\d{1,3}(\:\d{1,5})?)$"; } }
        public string RegexCheckAllowEmpty { get { return string.Concat(@"^$|", RegexCheck); } }

        public void SendMail(string from = "", string to = "", string cc = "", string bcc = "", string subject = "", string body = "",
            bool isBodyHtml = true, List<Attachment> attachments = null, string smtpHost = "", int port = 25, string userName = "",
            string password = "", bool enableSsl = true, bool useCredentials = false, SmtpClient smtpClient = null, bool disposeAfterSend = true)
        {
            MailMessage mm = new MailMessage();
            mm.IsBodyHtml = isBodyHtml;
            mm.From = new MailAddress(from);
            foreach (string address in to.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                mm.To.Add(address);
            }
            foreach (string address in cc.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                mm.CC.Add(address);
            }
            foreach (string address in bcc.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                mm.Bcc.Add(address);
            }
            mm.Subject = subject;
            mm.Body = body;

            if (attachments != null)
            {
                foreach (Attachment att in attachments)
                {
                    mm.Attachments.Add(att);
                }
            }

            if (smtpClient == null)
            {
                smtpClient = new SmtpClient();
            }
            smtpClient.Host = smtpHost;
            smtpClient.Port = port;
            smtpClient.EnableSsl = enableSsl;
            if (useCredentials)
            {
                NetworkCredential nc = new NetworkCredential(userName, password);
                smtpClient.Credentials = nc;
            }
            smtpClient.Send(mm);
            if (disposeAfterSend)
            {
                smtpClient.Dispose();
            }
        }

        public bool IsEmailValid(string email)
        {
            // Return true if strIn is in valid e-mail format.
            if (email != null)
            {
                return Regex.IsMatch(email,
                       @"^(?("")(""[^""]+?""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                       @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9]{2,17}))$",
                       RegexOptions.IgnoreCase);
            }
            else
            {
                return false;
            }
        }
    }
}
