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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Services;

namespace Mtd.OrderMaker.Web.Areas.Identity.Pages.Users
{
    public class IndexModel : PageModel
    {
        private readonly UserHandler _userManager;
        private readonly RoleManager<WebAppRole> _roleManager;

        public IndexModel(UserHandler userManager, RoleManager<WebAppRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public List<WebAppPerson> Persons { get; set; }

        public string SearchText { get; set; }

        public async Task<IActionResult> OnGetAsync(string searchText)
        {

            IQueryable<WebAppUser> query = _userManager.Users;
            if (searchText != null)
            {
                string normText = searchText.ToUpper();
                query = query.Where(x => x.NormalizedUserName.Contains(normText) ||
                     x.Title.ToUpper().Contains(normText) || x.NormalizedEmail.Contains(normText)
                );
                SearchText = searchText;
            }

            Persons = new List<WebAppPerson>();
            IList<WebAppUser> users = await query.ToListAsync();

            foreach (var user in users) {
               var roles = await _userManager.GetRolesAsync(user);
                Persons.Add(new WebAppPerson {
                     User = user,
                     Role = await _roleManager.FindByNameAsync(roles.FirstOrDefault())
                });
            }
            

            return Page();
        }

        public class WebAppPerson
        {
            public WebAppUser User { get; set; }
            public WebAppRole Role { get; set; }
        }

    }
}