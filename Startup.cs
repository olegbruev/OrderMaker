/*
    MTD OrderMaker - http://ordermaker.org
    Copyright (c) 2019 Oleg Bruev <job4bruev@gmail.com>. All rights reserved.

    This file is part of MTD OrderMaker.
    MTD OrderMaker is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see  https://www.gnu.org/licenses/.
*/

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mtd.OrderMaker.Web.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mtd.OrderMaker.Web.DataConfig;
using Microsoft.AspNetCore.Identity.UI.Services;
using Mtd.OrderMaker.Web.Services;
using System;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Razor;
using System.Globalization;
using Microsoft.Extensions.Hosting;

namespace Mtd.OrderMaker.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            CurrentEnvironment = env;
        }

        public IConfiguration Configuration { get; }
        public IWebHostEnvironment CurrentEnvironment { get; }


        public void ConfigureServices(IServiceCollection services)
        {

            services.Configure<CookiePolicyOptions>(options =>
            {

                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseMySql(
                    Configuration.GetConnectionString("IdentityConnection")));

            services.AddDefaultIdentity<WebAppUser>(config =>
            {
                config.SignIn.RequireConfirmedEmail = false;
                config.User.RequireUniqueEmail = true;

            }).AddRoles<WebAppRole>()
             .AddEntityFrameworkStores<IdentityDbContext>()
                .AddDefaultTokenProviders();

            services.AddDbContext<OrderMakerContext>(options => options.UseMySql(Configuration.GetConnectionString("DataConnection")));

            services.AddMvc()
                .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
                .AddDataAnnotationsLocalization()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddAuthorization(options =>
            {
                options.AddPolicy("RoleAdmin", policy => policy.RequireRole("Admin"));
                options.AddPolicy("RoleUser", policy => policy.RequireRole("User", "Admin"));
                options.AddPolicy("RoleGuest", policy => policy.RequireRole("Guest", "User", "Admin"));
            });


            services.AddLocalization(options => options.ResourcesPath = "Resources");

            services.AddMvc()
                    .AddRazorPagesOptions(options =>
                    {
                        options.Conventions.AuthorizeFolder("/");
                        options.Conventions.AuthorizeAreaFolder("Workplace", "/", "RoleUser");
                        options.Conventions.AuthorizeAreaFolder("Identity", "/Users", "RoleAdmin");
                        options.Conventions.AuthorizeAreaFolder("Config", "/", "RoleAdmin");
                    });
   
            services.AddScoped<UserHandler>();
            services.AddTransient<ConfigHandler>();
            services.AddTransient<IEmailSender, EmailSender>();
            services.AddTransient<IEmailSenderBlank, EmailSenderBlank>();
            services.Configure<EmailSettings>(Configuration.GetSection("EmailSettings"));
            services.Configure<ConfigSettings>(Configuration.GetSection("ConfigSettings"));
       
            services.AddDataProtection()
                    .SetApplicationName($"ordermaker-{CurrentEnvironment.EnvironmentName}")
                    .PersistKeysToFileSystem(new DirectoryInfo($@"{CurrentEnvironment.ContentRootPath}\keys"));

            services.AddMvc(options => options.EnableEndpointRouting = false);

#if DEBUG
            IMvcBuilder builder = services.AddRazorPages();
            if (CurrentEnvironment.IsDevelopment())
            {
                builder.AddRazorRuntimeCompilation();
            }
#endif

        }


        public void Configure(IApplicationBuilder app, IServiceProvider serviceProvider)
        {
            var config = serviceProvider.GetRequiredService<IOptions<ConfigSettings>>();

            if (config.Value.Migrate == "true")
            {
                using var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
                var context = serviceScope.ServiceProvider.GetService<OrderMakerContext>();
                context.Database.Migrate();

                var contextIdentity = serviceScope.ServiceProvider.GetService<IdentityDbContext>();
                contextIdentity.Database.Migrate();

                InitDataBase(serviceProvider);
            }

            if (CurrentEnvironment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }
           
            var cultureInfo = new CultureInfo(config.Value.CultureInfo);
            var localizationOptions = new RequestLocalizationOptions()
            {
                SupportedCultures = new List<CultureInfo> { cultureInfo },
                SupportedUICultures = new List<CultureInfo> { cultureInfo },
                DefaultRequestCulture = new RequestCulture(cultureInfo),

                FallBackToParentCultures = false,
                FallBackToParentUICultures = false,
                RequestCultureProviders = null
            };

            app.UseRequestLocalization(localizationOptions);

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseCookiePolicy();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
            });

            app.UseMvc();

        }


        private void InitDataBase(IServiceProvider serviceProvider)
        {

            var roleManager = serviceProvider.GetRequiredService<RoleManager<WebAppRole>>();
            Task<bool> hasAdminRole = roleManager.RoleExistsAsync("Admin");
            hasAdminRole.Wait();

            if (!hasAdminRole.Result)
            {
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

                roleManager.CreateAsync(roleAdmin).Wait();
                roleManager.CreateAsync(roleUser).Wait();
                roleManager.CreateAsync(roleGuest).Wait();
            }

            var userManager = serviceProvider.GetRequiredService<UserManager<WebAppUser>>();
            Task<bool> hasUser = userManager.Users.AnyAsync();
            hasUser.Wait();

            if (!hasUser.Result)
            {

                var config = serviceProvider.GetRequiredService<IOptions<ConfigSettings>>();

                WebAppUser webAppUser = new WebAppUser
                {
                    Email = config.Value.EmailSupport,
                    EmailConfirmed = true,
                    Title = "Administrator",
                    UserName = config.Value.DefaultUSR,

                };

                userManager.CreateAsync(webAppUser, config.Value.DefaultPWD).Wait();
                userManager.AddToRoleAsync(webAppUser, "Admin").Wait();

            }

            var context = serviceProvider.GetRequiredService<OrderMakerContext>();

            Task<bool> formGroup = context.MtdCategoryForm.AnyAsync();
            formGroup.Wait();

            if (!formGroup.Result)
            {
                MtdCategoryForm mtdGroupForm = new MtdCategoryForm
                {
                    Id = "17101180-9250-4498-BE4E-4A941AD6713C",
                    Name = "Default",
                    Description = "Default Group",
                    Parent = "17101180-9250-4498-BE4E-4A941AD6713C"
                };

                context.MtdCategoryForm.Add(mtdGroupForm);
                context.SaveChanges();
            }

            Task<bool> sysType = context.MtdSysType.AnyAsync();
            sysType.Wait();

            if (!sysType.Result)
            {

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

                context.MtdSysType.AddRange(mtdSysTypes);
                context.SaveChanges();
            }

            Task<bool> sysTerm = context.MtdSysTerm.AnyAsync();
            sysTerm.Wait();
            if (!sysTerm.Result)
            {
                List<MtdSysTerm> mtdSysTerms = new List<MtdSysTerm>
                {
                    new MtdSysTerm {Id=1,Name="equal", Sign="=" },
                    new MtdSysTerm {Id=2,Name="less", Sign="<" },
                    new MtdSysTerm {Id=3,Name="more", Sign=">" },
                    new MtdSysTerm {Id=4,Name="contains", Sign="~" },
                    new MtdSysTerm {Id=5,Name="no equal", Sign="<>" },
                };

                context.MtdSysTerm.AddRange(mtdSysTerms);
                context.SaveChanges();
            }

            Task<bool> sysStyle = context.MtdSysStyle.AnyAsync();
            sysStyle.Wait();
            if (!sysStyle.Result)
            {
                List<MtdSysStyle> mtdSysStyles = new List<MtdSysStyle>
                {
                    new MtdSysStyle{Id=4,Name="Line", Description="Line", Active=true},
                    new MtdSysStyle{Id=5,Name="Column", Description="Column", Active=true}
                };

                context.MtdSysStyle.AddRange(mtdSysStyles);
                context.SaveChanges();
            }

        }


    }
}
