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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Data;
using Mtd.OrderMaker.Web.Services;

namespace Mtd.OrderMaker.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly OrderMakerContext _context;
        private readonly UserHandler _userHandler;

        public IndexModel(OrderMakerContext context, UserHandler userHandler)
        {
            _context = context;
            _userHandler = userHandler;
        }

        public IList<MtdForm> Forms { get; set; }
        public string SearchText { get; set; }

        public async Task<IActionResult> OnGetAsync(string searchText)
        {
            
            WebAppUser user = await _userHandler.GetUserAsync(HttpContext.User);

            List<string> formIds = await _userHandler.GetFormIdsAsync(user, RightsType.View, RightsType.ViewGroup, RightsType.ViewOwn);
            List<MtdForm> forms = await _context.MtdForm.Where(x => formIds.Contains(x.Id)).ToListAsync();

            foreach (var form in forms)
            {
                bool isExists = await _context.MtdFilter.Where(x => x.MtdForm == form.Id && x.IdUser==user.Id).AnyAsync();
                if (!isExists)
                {
                    MtdFilter mtdFilter = new MtdFilter
                    {
                        MtdForm = form.Id,
                        IdUser = user.Id,
                        Page = 1,
                        PageSize = 10,
                        SearchText = "",
                        ShowDate = 1,
                        WaitList = 0,
                        SearchNumber = "",
                        ShowNumber = 1
                    };

                    await _context.MtdFilter.AddAsync(mtdFilter);
                    await _context.SaveChangesAsync();
                }
            }

            IQueryable<MtdForm> query = _context.MtdForm
                    .Include(x => x.MtdCategoryNavigation)
                    .Include(x => x.MtdFormHeader)
                    .Include(x => x.MtdFormDesk)
                    .Where(x => formIds.Contains(x.Id));

            if (searchText != null)
            {
                query = query.Where(x => x.Name.Contains(searchText));
                SearchText = searchText;
            }


            Forms = await query.ToListAsync();
            return Page();
        }


        /*Sample code that makes a Culture change*/
        //[HttpPost]
        //public IActionResult OnPost(string culture)
        //{
        //    Response.Cookies.Append(
        //        CookieRequestCultureProvider.DefaultCookieName,
        //        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        //        new CookieOptions { Path = "/", HttpOnly = false, IsEssential = true, Expires = DateTimeOffset.UtcNow.AddMonths(1) }
        //    );

        //    return RedirectToPage("./Index");
        //}

    }
}
