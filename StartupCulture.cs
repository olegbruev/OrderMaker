using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Data;
using Mtd.OrderMaker.Web.DataConfig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mtd.OrderMaker.Web
{
    public class StartupCulture : IHostedService
    {
        private readonly IServiceProvider serviceProvider;
        public StartupCulture(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {

            using (var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<OrderMakerContext>();
                var locOptions = scope.ServiceProvider.GetRequiredService<IOptions<RequestLocalizationOptions>>();
                int cultureId = (int)ConfigParamId.DefaultCulture;
                bool canConnect = await dbContext.Database.CanConnectAsync();
                if (canConnect)
                {
                    string culture = await dbContext.MtdConfigParam.Where(x => x.Id == cultureId).Select(x => x.Value).FirstOrDefaultAsync();
                    if (culture != null)
                    {
                        locOptions.Value.DefaultRequestCulture = new RequestCulture(culture);
                    }
                }

            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }


}
