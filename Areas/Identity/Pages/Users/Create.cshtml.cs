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

using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.DataConfig;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;

namespace Mtd.OrderMaker.Web.Areas.Identity.Pages.Users
{
    public partial class CreateModel : PageModel
    {
        private readonly UserManager<WebAppUser> _userManager;
        private readonly SignInManager<WebAppUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<CreateModel> _logger;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IOptions<RequestLocalizationOptions> _options;
        private readonly IStringLocalizer<CreateModel> _localizer;


        public CreateModel(
            UserManager<WebAppUser> userManager,
            SignInManager<WebAppUser> signInManager,
            IEmailSender emailSender,
            ILogger<CreateModel> logger,
            IWebHostEnvironment hostingEnvironment,
            IOptions<RequestLocalizationOptions> options,
            IStringLocalizer<CreateModel> localizer
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _logger = logger;
            _hostingEnvironment = hostingEnvironment;
            _options = options;
            _localizer = localizer;
        }

        [BindProperty]
        public string UserName { get; set; }
        [BindProperty]
        public bool IsEmailConfirmed { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Text)]
            [Display(Name = "Full name")]
            public string Title { get; set; }

            [Required]
            [DataType(DataType.Text)]
            [Display(Name = "User name")]
            public string UserName { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Display(Name = "Send Email")]
            public bool SendEmail { get; set; }

            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            string pass = Convert.ToBase64String(salt);

            var user = new WebAppUser { Title = Input.Title, UserName = Input.UserName, Email = Input.Email, EmailConfirmed = Input.SendEmail };
            var result = await _userManager.CreateAsync(user, pass);
            await _userManager.AddToRoleAsync(user, "Guest");

            if (result.Succeeded && Input.SendEmail)
            {
                _logger.LogInformation("User created a new account with password.");
                
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { userId = user.Id, code },
                    protocol: Request.Scheme);

                string webRootPath = _hostingEnvironment.WebRootPath;
                string contentRootPath = _hostingEnvironment.ContentRootPath;

                string culture = "";
                RequestCulture requestCulture = new RequestCulture("en-US");
                if (!_options.Value.DefaultRequestCulture.Equals(requestCulture))
                {
                    culture = ".ru-RU";
                }

                var file = Path.Combine(contentRootPath, "wwwroot", "lib", "mtd-ordermaker", "emailform", $"userCreated{culture}.html" );
                var htmlArray = System.IO.File.ReadAllText(file);
                string htmlText = htmlArray.ToString();
                
                htmlText = htmlText.Replace("{link}", $"<a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>{_localizer["Create account password"]}</a>");
                htmlText = htmlText.Replace("{login}", user.UserName);

                await _emailSender.SendEmailAsync(Input.Email, _localizer["Access to OrderMaker granted"], htmlText);                

                //await _signInManager.SignInAsync(user, isPersistent: false);
                //return LocalRedirect(returnUrl);
                // return RedirectToPage("./CheckEmail");                
            }

            if (!result.Succeeded) {

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                return Page();
            }

            return RedirectToPage("./Index");

        }
    }
}