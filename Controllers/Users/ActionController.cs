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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mtd.OrderMaker.Web.Data;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Mtd.OrderMaker.Web.Controllers.Users
{
    [Authorize(Roles = "Admin")]
    public partial class UsersController : ControllerBase
    {

        [HttpPost("admin/confirm/email")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAdminConfimEmailAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string userName = requestForm["UserName"];
            var user = await _userManager.FindByNameAsync(userName);

            if (user == null)
            {
                return NotFound();
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var email = await _userManager.GetEmailAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId, code },
                protocol: Request.Scheme);

            string culture = "";
            if (!_options.Value.CultureInfo.Equals("en-US"))
            {
                culture = $".{_options.Value.CultureInfo}";
            }

            string webRootPath = _hostingEnvironment.WebRootPath;
            string contentRootPath = _hostingEnvironment.ContentRootPath;
            var file = Path.Combine(contentRootPath, "wwwroot", "lib", "mtd-ordermaker", "emailform", $"userEmail{culture}.html");
            var htmlArray = System.IO.File.ReadAllText(file);
            string htmlText = htmlArray.ToString();

            htmlText = htmlText.Replace("{link}", $"<a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>{_localizer["Verify Email Address"]}</a>");
            await _emailSender.SendEmailAsync(email, _localizer["Email Verification"], htmlText);

            user.EmailConfirmed = false;
            await _userManager.UpdateAsync(user);

            return Ok();
        }

        [HttpPost("admin/confirm/password")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAdminConfirmPasswordAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string userName = requestForm["UserName"];
            var user = await _userManager.FindByNameAsync(userName);

            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                return NotFound();
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var email = await _userManager.GetEmailAsync(user);
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code },
                protocol: Request.Scheme);

            string culture = "";
            if (!_options.Value.CultureInfo.Equals("en-US"))
            {
                culture = $".{_options.Value.CultureInfo}";
            }

            string webRootPath = _hostingEnvironment.WebRootPath;
            string contentRootPath = _hostingEnvironment.ContentRootPath;
            var file = Path.Combine(contentRootPath, "wwwroot", "lib", "mtd-ordermaker", "emailform", $"userPassword{culture}.html");
            var htmlArray = System.IO.File.ReadAllText(file);
            string htmlText = htmlArray.ToString();

            htmlText = htmlText.Replace("{link}", $"<a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>{_localizer["Create account password"]}</a>");
            htmlText = htmlText.Replace("{login}", user.UserName);

            await _emailSender.SendEmailAsync(email, _localizer["Password reset"], htmlText);

            return Ok();
        }

        [HttpPost("admin/claims/all")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAdminClaimsAll()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            var userId = requestForm["user-id"];

            if (userId == string.Empty) { return NotFound(); }
            var user = await _userManager.FindByIdAsync(userId);

            IList<MtdForm> mtdForms = await _context.MtdForm.Include(x => x.MtdFormPart).ToListAsync();

            List<Claim> newClaims = new List<Claim>();
            IEnumerable<Claim> claims = await _userManager.GetClaimsAsync(user);
            await _userManager.RemoveClaimsAsync(user, claims);

            foreach (MtdForm form in mtdForms)
            {
                string[] noSelect = { "-view-own", "-view-group", "-edit-own", "-edit-group", "-delete-own", "-delete-group" };
                foreach (string claimValue in claimValues)
                {
                    Claim claim = new Claim(form.Id, claimValue);
                    if (!noSelect.Contains(claim.Value))
                    { newClaims.Add(claim); }
                };

                foreach (MtdFormPart mtdFormPart in form.MtdFormPart)
                {
                    foreach (string claimPart in claimParts)
                    {
                        Claim claim = new Claim(mtdFormPart.Id, claimPart);
                        newClaims.Add(claim);
                    };

                }
            }

            IList<MtdGroup> groups = await _context.MtdGroup.ToListAsync();

            foreach (MtdGroup group in groups)
            {
                Claim claim = new Claim(group.Id, "-group");
                newClaims.Add(claim);
            }

            try
            {
                await _userManager.AddClaimsAsync(user, newClaims);
            }
            catch
            {
                throw;
            }


            return Ok();
        }

        [HttpPost("admin/claims/clear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAdminClaimsClear()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            var userId = requestForm["user-id"];
            if (userId == string.Empty) { return NotFound(); }

            var user = await _userManager.FindByIdAsync(userId);
            IEnumerable<Claim> claims = await _userManager.GetClaimsAsync(user);
            await _userManager.RemoveClaimsAsync(user, claims);

            return Ok();
        }
    }
}
