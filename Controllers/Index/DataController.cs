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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mtd.OrderMaker.Web.Controllers.Index
{
    [Route("api/index")]
    [ApiController]
    [Authorize(Roles = "Admin,User")]
    public class DataController : ControllerBase
    {
        private readonly OrderMakerContext _context;
        private readonly UserManager<WebAppUser> _userManager;


        public DataController(OrderMakerContext context, UserManager<WebAppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost("search/text")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostSearchTextAsync()
        {
            IFormCollection requestForm = await  Request.ReadFormAsync();
            string form = requestForm["indexForm"];
            string value = requestForm["indexSearchText"];
            WebAppUser user = await _userManager.GetUserAsync(User);

            MtdFilter filter = await _context.MtdFilter.FirstOrDefaultAsync(x => x.IdUser == user.Id & x.MtdForm == form);
            bool old = true;
            if (filter == null)
            {
                old = false;
                filter = new MtdFilter { IdUser = user.Id, MtdForm = form };
            }

            filter.SearchNumber = "";
            filter.SearchText = value;
            filter.Page = 1;

            if (old)
            {
                _context.MtdFilter.Update(filter);
            }
            else
            {
                await _context.MtdFilter.AddAsync(filter);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { throw ex.InnerException; }


            return Ok();
        }

        [HttpPost("search/number")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostSerarchIndexAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string form = requestForm["formId"];
            string value = requestForm["searchNumber"];

            WebAppUser user = await _userManager.GetUserAsync(User);
            MtdFilter filter = await _context.MtdFilter.FirstOrDefaultAsync(x => x.IdUser == user.Id & x.MtdForm == form);
            bool old = true;
            if (filter == null)
            {
                old = false;
                filter = new MtdFilter { IdUser = user.Id, MtdForm = form };
            }

            filter.SearchNumber = value;
            filter.Page = 1;
            filter.SearchText = "";

            if (old)
            {
                _context.MtdFilter.Update(filter);
            }
            else
            {
                await _context.MtdFilter.AddAsync(filter);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { throw ex.InnerException; }
            return Ok();
        }

        [HttpPost("{idForm}/pagesize/{number}")]
        public async Task<IActionResult> PostPageSize(string idForm, int number)
        {
            int temp = number;
            if (temp > 50) temp = 50;
            WebAppUser user = await _userManager.GetUserAsync(User);
            MtdFilter filter = await _context.MtdFilter.FirstOrDefaultAsync(x => x.IdUser == user.Id && x.MtdForm == idForm);
            if (filter == null)
            {
                filter = new MtdFilter { SearchNumber = "", SearchText = "" };
                await _context.MtdFilter.AddAsync(filter);
                await _context.SaveChangesAsync();
            }

            filter.PageSize = number;
            _context.MtdFilter.Update(filter);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("pagemove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostPageMove()
        {
            /* number
             * 1 -  First Page; 2 - back; 3 - forward;
             */
            IFormCollection requestForm = await Request.ReadFormAsync();
            string idForm = requestForm["formId"];
            string formValue = requestForm["formValue"];
            int number = int.Parse(formValue);

            WebAppUser user = await _userManager.GetUserAsync(User);
            MtdFilter filter = await _context.MtdFilter.FirstOrDefaultAsync(x => x.IdUser == user.Id && x.MtdForm == idForm);
            if (filter == null)
            {
                filter = new MtdFilter { SearchNumber = "", SearchText = "" };
                await _context.MtdFilter.AddAsync(filter);
                await _context.SaveChangesAsync();
            }

            int page = filter.Page;

            switch (number)
            {
                case 2: { if (page > 1) { page--; } break; }
                case 3: { page++; break; }
                default: { page = 1; break; }
            };

            filter.Page = page < 0 ? page = 1 : page;

            _context.MtdFilter.Update(filter);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("filter/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostFilterAddAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string valueField = requestForm["indexInputField"];
            string idForm = requestForm["indexInputForm"];
            string valueFilter = requestForm["indexInputFilter"];
            string valueTerm = requestForm["indexInputTerm"];
            string valueFieldList = requestForm[$"{valueField}-inputlist"];

            string result = "";
            if (valueFilter.FirstOrDefault() != null) { result = valueFilter; }
            if (valueFieldList.FirstOrDefault() != null) { result = valueFieldList; }

            WebAppUser user = await _userManager.GetUserAsync(User);
            MtdFilter filter = await _context.MtdFilter.FirstOrDefaultAsync(x => x.IdUser == user.Id & x.MtdForm == idForm);

            if (filter == null)
            {
                filter = new MtdFilter
                {
                    IdUser = user.Id,
                    MtdForm = idForm,
                    SearchNumber = "",
                    SearchText = "",
                    Page = 1,
                    PageSize = 10
                };
                await _context.MtdFilter.AddAsync(filter);
                await _context.SaveChangesAsync();
            }

            if (valueField != "period")
            {
                await SaveFilterField(valueField, valueTerm, result, filter);

            } else
            {
                string dateStart = requestForm["periodStart"];
                string dateFinish = requestForm["periodFinish"];

                bool isOkDateStart = DateTime.TryParse(dateStart, out DateTime dateTimeStart);
                bool isOkDateFinish = DateTime.TryParse(dateFinish, out DateTime dateTimeFinish);

                if (isOkDateStart && isOkDateFinish)
                {
                    MtdFilterDate mtdFilterDate = new MtdFilterDate
                    {
                        Id = filter.Id,
                        DateStart = dateTimeStart,
                        DateEnd = dateTimeFinish
                    };

                    bool isExists = await _context.MtdFilterDate.Where(x=>x.Id == filter.Id).AnyAsync();
                 
                    if (isExists)
                    {
                        _context.MtdFilterDate.Update(mtdFilterDate);                        
                    } else
                    {
                        await _context.MtdFilterDate.AddAsync(mtdFilterDate);
                    }

                    await _context.SaveChangesAsync();

                }

            }
            

            return Ok();

        }

        private async Task SaveFilterField(StringValues valueField, StringValues valueTerm, string result, MtdFilter filter)
        {
            int term = int.Parse(valueTerm);
            MtdFilterField field = new MtdFilterField { MtdFilter = filter.Id, MtdFormPartField = valueField, MtdTerm = term, Value = result };

            try
            {
                await _context.MtdFilterField.AddAsync(field);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { throw ex.InnerException; }
        }

        [HttpPost("filter/remove")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostFilterRemoveAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string strID = requestForm["idField"];
            if (strID.Contains("-field"))
            {
                strID = strID.Replace("-field", "");
                bool ok = int.TryParse(strID,out int idField);
                if (!ok) return Ok();
                MtdFilterField mtdFilterField = new MtdFilterField { Id = idField };
                try
                {
                    _context.MtdFilterField.Remove(mtdFilterField);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex) { throw ex.InnerException; }
            }

            if (strID.Contains("-date"))
            {
                strID = strID.Replace("-date", "");
                bool ok = int.TryParse(strID, out int idFilter);
                if (!ok) return Ok();
                MtdFilterDate filterDate = new MtdFilterDate { Id = idFilter };
                try
                {
                    _context.MtdFilterDate.Remove(filterDate);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex) { throw ex.InnerException; }
            }

            if (strID.Contains("-script")) {
                strID = strID.Replace("-script", "");
                bool ok = int.TryParse(strID, out int idFilter);
                if (!ok) return Ok();
                MtdFilterScript mtdFilterScript = await _context.MtdFilterScript.FindAsync(idFilter);
                mtdFilterScript.Apply = 0;
                _context.MtdFilterScript.Update(mtdFilterScript);
                await _context.SaveChangesAsync();
            }

            return Ok();

        }

        [HttpPost("filter/removeAll")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostFilterRemoveAllAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string strID = requestForm["idFilter"];
            bool isOk = int.TryParse(strID,out int idFilter);
            if (!isOk) { return NotFound(); }

            IList<MtdFilterField> mtdFilterFields = await _context.MtdFilterField.Where(x => x.MtdFilter == idFilter).ToListAsync();
            IList<MtdFilterScript> mtdFilterScripts = await _context.MtdFilterScript
                .Where(x => x.MtdFilter == idFilter)
                .Select(x => new MtdFilterScript { Id = x.Id, MtdFilter = x.MtdFilter, Name = x.Name, Description = x.Description, Script = x.Script, Apply = 0 })
                .ToListAsync();           
            MtdFilterDate mtdFilterDate = await _context.MtdFilterDate.Where(x => x.Id == idFilter).FirstOrDefaultAsync();
            
            try
            {
                _context.MtdFilterField.RemoveRange(mtdFilterFields);
                _context.MtdFilterScript.UpdateRange(mtdFilterScripts);
                if (mtdFilterDate != null)
                {
                    _context.MtdFilterDate.Remove(mtdFilterDate);
                }                
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { throw ex.InnerException; }


            return Ok();

        }

        [HttpPost("filter/columns/add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostFilterColumnsAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string idForm = requestForm["indexDataColumnIdForm"];
            string data = requestForm["indexDataColumnList"];
            string showNumber = requestForm["indexDataColumnNumber"];
            string showDate = requestForm["indexDataColumnDate"];

            List<string> fieldIds = new List<string>();
            if (data.FirstOrDefault() != null && data.Length > 0) fieldIds = data.Split(",").ToList();
            WebAppUser user = await _userManager.GetUserAsync(User);
            MtdFilter filter = await _context.MtdFilter.Include(m => m.MtdFilterColumn).FirstOrDefaultAsync(x => x.IdUser == user.Id & x.MtdForm == idForm);

            if (filter == null)
            {
                filter = new MtdFilter
                {
                    IdUser = user.Id,
                    MtdForm = idForm,
                    SearchNumber = "",
                    SearchText = "",
                    Page = 1,
                    PageSize = 10
                };
                await _context.MtdFilter.AddAsync(filter);
                await _context.SaveChangesAsync();
            }

            List<MtdFilterColumn> columns = new List<MtdFilterColumn>();
            int seq = 0;
            foreach (string field in fieldIds.Where(x => x != ""))
            {
                seq++;
                columns.Add(new MtdFilterColumn
                {
                    MtdFilter = filter.Id,
                    MtdFormPartField = field,
                    Sequence = seq
                });
            }


            try
            {
                filter.ShowNumber = showNumber == "true" ? (sbyte)1 : (sbyte)0;
                filter.ShowDate = showDate == "true" ? (sbyte)1 : (sbyte)0;

                _context.MtdFilter.Update(filter);

                if (filter.MtdFilterColumn != null)
                {
                    _context.MtdFilterColumn.RemoveRange(filter.MtdFilterColumn);
                    await _context.SaveChangesAsync();
                }

                await _context.MtdFilterColumn.AddRangeAsync(columns);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { throw ex.InnerException; }


            return Ok();

        }


        [HttpPost("waitlist/set")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostWaitListSetAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string idForm = requestForm["id-form-waitlist"];
            WebAppUser user = await _userManager.GetUserAsync(HttpContext.User);
            MtdFilter mtdFilter = await _context.MtdFilter.Where(x => x.IdUser == user.Id && x.MtdForm == idForm).FirstOrDefaultAsync();
            if (mtdFilter == null)
            {
                mtdFilter = new MtdFilter
                {
                    IdUser = user.Id,
                    MtdForm = idForm,
                    PageSize = 10,
                    SearchText = "",
                    SearchNumber = "",
                    Page = 1,
                    WaitList = 1,
                };
                await _context.MtdFilter.AddAsync(mtdFilter);
                await _context.SaveChangesAsync();
                return Ok();
            }

            mtdFilter.WaitList = mtdFilter.WaitList == 0 ? 1 : 0;
            _context.MtdFilter.Update(mtdFilter);
            await _context.SaveChangesAsync();

            return Ok();

        }

        [HttpPost("filter/script")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostFilterScriptAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string valueField = requestForm["script-selector"];
            bool isOk = int.TryParse(valueField, out int id);
            if (!isOk) { return NotFound(); }
            MtdFilterScript mtdFilterScript = await _context.MtdFilterScript.FindAsync(id);
            mtdFilterScript.Apply = 1;
            _context.MtdFilterScript.Update(mtdFilterScript);
            await _context.SaveChangesAsync();

            return Ok();

        }
    }
}
