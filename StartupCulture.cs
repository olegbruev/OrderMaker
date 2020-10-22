using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<StartupCulture> logger;

        public StartupCulture(IServiceProvider serviceProvider, ILogger<StartupCulture> logger)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {

            using (var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<OrderMakerContext>();
                var locOptions = scope.ServiceProvider.GetRequiredService<IOptions<RequestLocalizationOptions>>();
                int cultureId = (int)ConfigParamId.DefaultCulture;

                bool canConnect = false;
                try
                {
                    canConnect = await dbContext.Database.CanConnectAsync();

                }  catch {

                    logger.LogError("Error connection to database");
                }
                
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
