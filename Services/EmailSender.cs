/*
    OrderMaker - http://ordermaker.org
    Copyright(c) 2019 Oleg Bruev. All rights reserved.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.If not, see https://www.gnu.org/licenses/.
*/

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using Mtd.OrderMaker.Web.DataConfig;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Localization;
using Mtd.OrderMaker.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Mtd.OrderMaker.Web.Services
{

    public class EmailSender : IEmailSender
    {
        private EmailSettings emailSettings;
        private readonly IHostingEnvironment _hostingEnvironment;
        private readonly OrderMakerContext context;



        public EmailSender(OrderMakerContext context, IHostingEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
            this.context = context;
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

                await ExecuteAsync(blankEmail.Email, blankEmail.Subject, htmlText);
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

