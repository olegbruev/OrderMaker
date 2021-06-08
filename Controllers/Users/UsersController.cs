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

using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Data;
using Mtd.OrderMaker.Web.DataConfig;

namespace Mtd.OrderMaker.Web.Controllers.Users
{
    [Route("api/users")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public partial class UsersController : ControllerBase
    {
        private readonly UserManager<WebAppUser> _userManager;
        private readonly RoleManager<WebAppRole> _roleManager;
        private readonly SignInManager<WebAppUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly OrderMakerContext _context;
        private readonly IStringLocalizer<UsersController> _localizer;
        private readonly IOptions<RequestLocalizationOptions> _options;

        private readonly List<string> claimValues = new List<string>() {
            "-create","-view","-edit", "-delete", "-view-own", "-edit-own", "-delete-own",
            "-set-own", "-reviewer", "-view-group", "-edit-group", "-delete-group"
        };

        private readonly List<string> claimParts = new List<string>() {
            "-part-create", "-part-view", "-part-edit"
        };

        public UsersController(
            UserManager<WebAppUser> userManager,
            RoleManager<WebAppRole> roleManager,
        SignInManager<WebAppUser> signInManager,
            IEmailSender emailSender,
            IWebHostEnvironment hostingEnvironment,
            OrderMakerContext context,
            IStringLocalizer<UsersController> localizer,
            IOptions<RequestLocalizationOptions> options
            )
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _hostingEnvironment = hostingEnvironment;
            _context = context;
            _localizer = localizer;
            _options = options;
        }


        [HttpPost("admin/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAdminDeleteAsync() {
            IFormCollection requestForm = await Request.ReadFormAsync();
            var userId = requestForm["user-delete-id"];
            var user = await _userManager.FindByIdAsync(userId);

            IList<MtdFilter> mtdFilters = await _context.MtdFilter.Where(x => x.IdUser == user.Id).ToListAsync();
            _context.MtdFilter.RemoveRange(mtdFilters);
            await _context.SaveChangesAsync();
            await _userManager.DeleteAsync(user);
            return Ok();
        }

        [HttpPost("admin/profile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAdminProfileAsync() {

            IFormCollection requestForm = await Request.ReadFormAsync();
            var username = requestForm["UserName"];

            if (username.Count == 0) {
                return BadRequest();
            }

            var user = await _userManager.FindByNameAsync(username);

            if (user == null)
            {
                return NotFound();
            }

            string email = requestForm["Input.Email"];
            string title = requestForm["Input.Title"];
            string phone = requestForm["Input.PhoneNumber"];
            string roleId = requestForm["Input.Role"];
            WebAppRole roleUser = await _roleManager.FindByIdAsync(roleId);

            string[] formConfirm = Request.Form["Input.IsConfirm"];
            bool isConfirm = false;
            if (formConfirm.FirstOrDefault() != null)
            {
                isConfirm = bool.Parse(formConfirm.FirstOrDefault());
            }            

            if (user.Email != email)
            {
                user.Email = email;
                user.EmailConfirmed = false;
            }

            if (user.Title != title)
            {
                user.Title = title;
            }

            if (user.PhoneNumber != phone)
            {
                user.PhoneNumber = phone;
            }

            if (isConfirm)
            {
                user.EmailConfirmed = true;
            }

            await _userManager.UpdateAsync(user);
            IList<string> roles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user,roles);

            await _userManager.AddToRoleAsync(user,roleUser.Name);

            return Ok();
        }

        [HttpPost("admin/claims")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAdminClaimsAsync() {

            IFormCollection requestForm = await Request.ReadFormAsync();
            var userId = requestForm["user-id"];

            if (userId == string.Empty) { return NotFound(); }

            var user = await _userManager.FindByIdAsync(userId);

            List<Claim> newClaims = new List<Claim>();
            IEnumerable<Claim> claims = await _userManager.GetClaimsAsync(user);
            await _userManager.RemoveClaimsAsync(user,claims);

            IList<MtdForm> forms = await _context.MtdForm.Include(x=>x.MtdFormPart).ToListAsync();
            IList<MtdGroup> groups = await _context.MtdGroup.ToListAsync();

            foreach (MtdGroup group in groups)
            {
                string value = requestForm[$"{group.Id}-group"];
                if (value == "true")
                {
                    Claim claim = new Claim(group.Id, "-group");
                    newClaims.Add(claim);
                }
            }

            foreach (MtdForm form in forms)
            {
                foreach(string claimValue in claimValues)
                {
                    string value =  requestForm[$"{form.Id}{claimValue}"];
                    if (value == "true")
                    {
                        Claim claim = new Claim(form.Id, claimValue);                        
                        newClaims.Add(claim);
                    }

                };

                foreach (MtdFormPart mtdFormPart in form.MtdFormPart)
                {
                    foreach (string claimPart in claimParts)
                    {
                        string value = requestForm[$"{mtdFormPart.Id}{claimPart}"];
                        if (value == "true")
                        {
                            Claim claim = new Claim(mtdFormPart.Id, claimPart);
                            newClaims.Add(claim);
                        }

                    };

                }
                
            }

            if (newClaims.Count > 0)
            {
                try {
                    await _userManager.AddClaimsAsync(user, newClaims);
                } catch 
                {
                    throw;
                }                
            }
            
            return Ok(); 
        }
    }
}