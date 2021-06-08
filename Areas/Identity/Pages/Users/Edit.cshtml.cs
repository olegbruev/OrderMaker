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
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Mtd.OrderMaker.Web.Areas.Identity.Data;

namespace Mtd.OrderMaker.Web.Areas.Identity.Pages.Users
{
    public partial class EditModel : PageModel
    {

        private readonly UserManager<WebAppUser> _userManager;
        private readonly RoleManager<WebAppRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _hostingEnvironment;

        public EditModel(
            UserManager<WebAppUser> userManager,
            RoleManager<WebAppRole> roleManager,
            IEmailSender emailSender,
            IWebHostEnvironment hostingEnvironment
            )
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _hostingEnvironment = hostingEnvironment;
        }
        

        public string UserName { get; set; }
        //public string Role { get; set; }

        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Text)]
            [Display(Name = "Full name")]
            public string Title { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }

            [Display(Name = "Confirm")]
            public bool IsConfirm { get; set; }
         
            public string Role { get; set; }
            
        }

        public async Task<IActionResult> OnGetAsync(string userId)
        {
            if (userId == null)
            {
                return NotFound();

            }

            var user = await _userManager.FindByIdAsync(userId);
            UserName = user.UserName;
            IList<WebAppRole> roles = await _roleManager.Roles.ToListAsync();
            var userRoles = await _userManager.GetRolesAsync(user);
            var userRoleName = userRoles.FirstOrDefault();
            var userRole = await _roleManager.FindByNameAsync(userRoleName);            

            Input = new InputModel
            {
                Role = userRole.Id,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Title = user.Title,
                IsConfirm = user.EmailConfirmed
            };

            ViewData["Roles"] = new SelectList(roles.OrderBy(x=>x.Seq), "Id", "Title", Input.Role);

            return Page();
        }

               
       
    }
}