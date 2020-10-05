using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mtd.OrderMaker.Web
{
    public class MigrationHostedStartup : IHostedService
    {
        private readonly IServiceProvider serviceProvider;
        public MigrationHostedStartup(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {

            using (var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<OrderMakerContext>();
                IEnumerable<string> pm = await dbContext.Database.GetPendingMigrationsAsync();
                bool dbMigration = pm.Any();        
                IEnumerable<string> applied = await dbContext.Database.GetAppliedMigrationsAsync();
                bool dbNoInit = applied.Count() == 0;

                if (dbMigration)
                {
                    await dbContext.Database.MigrateAsync();
                    if (dbNoInit) { await InitDataBase(dbContext); }
                }

                var idnContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                IEnumerable<string> pmIdn = await idnContext.Database.GetPendingMigrationsAsync();
                bool idnMigration = pmIdn.Count() > 0;
                IEnumerable<string> amIdn = await idnContext.Database.GetAppliedMigrationsAsync();
                bool idnNoInit = amIdn.Count() == 0;

                if (idnMigration)
                {
                    await idnContext.Database.MigrateAsync();
                    if (idnNoInit) { await InitIdentityAsync(scope); }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task InitDataBase(OrderMakerContext context)
        {
            bool exists =  await context.MtdSysType.AnyAsync();
            if (exists) { return; }


            MtdCategoryForm mtdGroupForm = new MtdCategoryForm
            {
                Id = "17101180-9250-4498-BE4E-4A941AD6713C",
                Name = "Default",
                Description = "Default Group",
                Parent = "17101180-9250-4498-BE4E-4A941AD6713C"
            };

            await context.MtdCategoryForm.AddAsync(mtdGroupForm);
     
            List<MtdSysType> mtdSysTypes = new List<MtdSysType> {
                    new MtdSysType{ Id = 1, Name="Text", Description="Text", Active=true },
                    new MtdSysType{ Id = 2, Name="Integer", Description="Integer", Active=true},
                    new MtdSysType{ Id = 3, Name="Decimal",Description="Decimal", Active=true},
                    new MtdSysType{ Id = 4, Name = "Memo",Description="Memo",Active=true},
                    new MtdSysType{ Id = 5, Name="Date",Description="Date",Active=true},
                    new MtdSysType{ Id = 6, Name="DateTime",Description="DateTime",Active=true},
                    new MtdSysType{ Id = 7, Name="File",Description="File",Active=true},
                    new MtdSysType{ Id = 8, Name="Image",Description="Image",Active=true},
                    //new MtdSysType{ Id = 9, Name="Phone",Description="Phone",Active=false},
                    //new MtdSysType{ Id = 10, Name="Time",Description="Time",Active=false},
                    new MtdSysType{ Id = 11, Name="List",Description="List",Active=true},
                    new MtdSysType{ Id = 12, Name="Checkbox",Description="Checkbox",Active=true},
                };

            await context.MtdSysType.AddRangeAsync(mtdSysTypes);

            List<MtdSysTerm> mtdSysTerms = new List<MtdSysTerm>
                {
                    new MtdSysTerm {Id=1,Name="equal", Sign="=" },
                    new MtdSysTerm {Id=2,Name="less", Sign=">" },
                    new MtdSysTerm {Id=3,Name="more", Sign="<" },
                    new MtdSysTerm {Id=4,Name="contains", Sign="~" },
                    new MtdSysTerm {Id=5,Name="no equal", Sign="<>" },
                };

            await context.MtdSysTerm.AddRangeAsync(mtdSysTerms);

            List<MtdSysStyle> mtdSysStyles = new List<MtdSysStyle>
                {
                    new MtdSysStyle{Id=4,Name="Line", Description="Line", Active=true},
                    new MtdSysStyle{Id=5,Name="Column", Description="Column", Active=true}
                };

            await context.MtdSysStyle.AddRangeAsync(mtdSysStyles);


            await context.SaveChangesAsync();

        }

        private async Task InitIdentityAsync(IServiceScope scope)
        {
            RoleManager<WebAppRole> roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<WebAppRole>>();
            UserManager<WebAppUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<WebAppUser>>();

            bool exists = await roleManager.Roles.Where(x => x.NormalizedName == "ADMIN").AnyAsync();
            if (exists) return;

            var roleAdmin = new WebAppRole
            {
                Name = "Admin",
                NormalizedName = "ADMIN",
                Title = "Administrator",
                Seq = 30
            };

            var roleUser = new WebAppRole
            {
                Name = "User",
                NormalizedName = "USER",
                Title = "User",
                Seq = 20
            };

            var roleGuest = new WebAppRole
            {
                Name = "Guest",
                NormalizedName = "GUEST",
                Title = "Guest",
                Seq = 10
            };

            await roleManager.CreateAsync(roleAdmin);
            await roleManager.CreateAsync(roleUser);
            await roleManager.CreateAsync(roleGuest);

            WebAppUser webAppUser = new WebAppUser
            {
                Email = "mail@mail.any",
                EmailConfirmed = true,
                Title = "Administrator",
                UserName = "Admin",
            };

            await userManager.CreateAsync(webAppUser, "Admin&890");
            await userManager.AddToRoleAsync(webAppUser, "Admin");
        }
    }


}
