using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mtd.OrderMaker.Web.Data;
using Mtd.OrderMaker.Web.DataConfig;

namespace Mtd.OrderMaker.Web.Areas.Config.Pages.Email
{
    public class InputModel
    {
        public string FromName { get; set; }
        public string FromAddress { get; set; }
        public string EmailPassword { get; set; }
        public string SmtpServer { get; set; }
        public string SmtpPort { get; set; }
        public string SupportEmail { get; set; }
    }

    
    public class IndexModel : PageModel
    {
        private readonly OrderMakerContext context;

        public IndexModel(OrderMakerContext context)
        {
            this.context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }
        public async Task<IActionResult> OnGetAsync() 
        {
            Input = new InputModel();
            IList<MtdConfigParam> configParams = await context.MtdConfigParam.AsNoTracking().ToListAsync();
            Input.FromName = configParams.Where(x => x.Id == (int)ConfigParamId.EmailFromName).Select(x => x.Value).FirstOrDefault() ?? string.Empty;
            Input.FromAddress = configParams.Where(x => x.Id == (int)ConfigParamId.EmailFromAddress).Select(x => x.Value).FirstOrDefault() ?? string.Empty;
            Input.EmailPassword = configParams.Where(x => x.Id == (int)ConfigParamId.EmailPassword).Select(x => x.Value).FirstOrDefault() ?? string.Empty;
            Input.SmtpServer = configParams.Where(x => x.Id == (int)ConfigParamId.EmailSmtpServer).Select(x => x.Value).FirstOrDefault() ?? string.Empty;
            Input.SmtpPort = configParams.Where(x => x.Id == (int)ConfigParamId.EmailSmtpPort).Select(x => x.Value).FirstOrDefault() ?? string.Empty;
            Input.SupportEmail = configParams.Where(x => x.Id == (int)ConfigParamId.SupportEmail).Select(x => x.Value).FirstOrDefault() ?? string.Empty;

            return Page();
        }
   
        public async Task<IActionResult> OnPostAsync()
        {
            List<MtdConfigParam> confifParams = new List<MtdConfigParam>();

            confifParams.Add(new MtdConfigParam { Id = (int)ConfigParamId.EmailFromName, Name = ConfigParamId.EmailFromName.ToString(), Value = Input.FromName });
            confifParams.Add(new MtdConfigParam { Id = (int)ConfigParamId.EmailFromAddress, Name = ConfigParamId.EmailFromAddress.ToString(), Value = Input.FromAddress});            
            confifParams.Add(new MtdConfigParam { Id = (int)ConfigParamId.EmailPassword, Name = ConfigParamId.EmailPassword.ToString(), Value = Input.EmailPassword });
            confifParams.Add(new MtdConfigParam { Id = (int)ConfigParamId.EmailSmtpServer, Name = ConfigParamId.EmailSmtpServer.ToString(), Value = Input.SmtpServer });
            confifParams.Add(new MtdConfigParam { Id = (int)ConfigParamId.EmailSmtpPort, Name = ConfigParamId.EmailSmtpPort.ToString(), Value = Input.SmtpPort });
            confifParams.Add(new MtdConfigParam { Id = (int)ConfigParamId.SupportEmail, Name = ConfigParamId.SupportEmail.ToString(), Value = Input.SupportEmail });

            context.MtdConfigParam.RemoveRange(confifParams);
            await context.SaveChangesAsync();

            await context.MtdConfigParam.AddRangeAsync(confifParams);
            await context.SaveChangesAsync();


            return RedirectToPage("./Index");
        }
    }


}
