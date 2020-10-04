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
using System.Linq;

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

            services.AddHostedService<MigrationHostedStartup>();

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







    }
}
