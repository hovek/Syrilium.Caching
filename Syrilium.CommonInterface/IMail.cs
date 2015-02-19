using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Mail;

namespace Syrilium.CommonInterface
{
    public interface IMail
    {
        string RegexCheck { get; }
        string RegexCheckAllowEmpty { get; }
        void SendMail(string from = "", string to = "", string cc = "", string bcc = "", string subject = "", string body = "",
            bool isBodyHtml = true, List<Attachment> attachments = null, string smtpHost = "", int port = 25, string userName = "",
            string password = "", bool enableSsl = true, bool useCredentials = false, SmtpClient smtpClient = null, bool disposeAfterSend = true);
        bool IsEmailValid(string email);
    }
}
