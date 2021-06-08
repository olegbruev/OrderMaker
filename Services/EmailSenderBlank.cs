using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Mtd.OrderMaker.Web.Data;
using Mtd.OrderMaker.Web.DataConfig;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace Mtd.OrderMaker.Web.Services
{
    public interface IEmailSenderBlank
    {
        Task SendEmailAsync(string email, string subject, string message);
        Task<bool> SendEmailBlankAsync(BlankEmail blankEmail);
    }

    public class BlankEmail
    {
        public string Email { get; set; }
        public string Subject { get; set; }
        public string Header { get; set; }
        public List<string> Content { get; set; }
    }

    public class EmailSenderBlank : IEmailSenderBlank
    {
        private EmailSettings emailSettings;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly OrderMakerContext context;


        public EmailSenderBlank(OrderMakerContext context, IWebHostEnvironment hostingEnvironment)
        {
            this.context = context;
            _hostingEnvironment = hostingEnvironment;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            await ExecuteAsync(email, subject, message);
        }


        public async Task<bool> SendEmailBlankAsync(BlankEmail blankEmail)
        {

            try
            {

                string message = string.Empty;
                foreach (string p in blankEmail.Content)
                {
                    message += $"<p>{p}</p>";
                }

                string webRootPath = _hostingEnvironment.WebRootPath;
                string contentRootPath = _hostingEnvironment.ContentRootPath;
                var file = Path.Combine(contentRootPath, "wwwroot", "lib", "mtd-ordermaker", "emailform", "blank.html");
                var htmlArray = File.ReadAllText(file);
                string htmlText = htmlArray.ToString();

                htmlText = htmlText.Replace("{title}", "OrderMaker");
                htmlText = htmlText.Replace("{header}", blankEmail.Header);
                htmlText = htmlText.Replace("{content}", message);

                await SendEmailAsync(blankEmail.Email, blankEmail.Subject, htmlText);
            }
            catch
            {
                return false;
            }


            return true;
        }

        private async Task ExecuteAsync(string email, string subject, string message)
        {

            IList<MtdConfigParam> configParams = await context.MtdConfigParam.Where(x => x.Id > 4 && x.Id < 10).ToListAsync();
            if (configParams == null) { return; }

            emailSettings = new EmailSettings
            {
                FromAddress = configParams.Where(x => x.Id == (int)ConfigParamId.EmailFromAddress).Select(x => x.Value).FirstOrDefault(),
                FromName = configParams.Where(x => x.Id == (int)ConfigParamId.EmailFromName).Select(x => x.Value).FirstOrDefault(),
                Password = configParams.Where(x => x.Id == (int)ConfigParamId.EmailPassword).Select(x => x.Value).FirstOrDefault(),
                SmtpServer = configParams.Where(x => x.Id == (int)ConfigParamId.EmailSmtpServer).Select(x => x.Value).FirstOrDefault(),
                Port = configParams.Where(x => x.Id == (int)ConfigParamId.EmailSmtpPort).Select(x => int.Parse(x.Value)).FirstOrDefault()
            };

            try
            {
                MailAddress toAddress = new MailAddress(email);
                MailAddress fromAddress = new MailAddress(emailSettings.FromAddress, emailSettings.FromName);
                // создаем письмо: message.Destination - адрес получателя
                MailMessage mail = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = message,
                    IsBodyHtml = true,
                };

                using (SmtpClient smtp = new SmtpClient(emailSettings.SmtpServer, emailSettings.Port))
                {
                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential(emailSettings.FromAddress, emailSettings.Password);
                    smtp.EnableSsl = true;
                    await smtp.SendMailAsync(mail);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error EMail sender service \n {ex.Message}");
            }
        }
    }
}
