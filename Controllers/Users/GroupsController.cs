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
using Mtd.OrderMaker.Web.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mtd.OrderMaker.Web.Controllers.Users
{

    [Route("api/groups")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class GroupsController : ControllerBase
    {
        private readonly OrderMakerContext _context;
        public GroupsController(OrderMakerContext context)
        {
            _context = context;
        }


        [HttpPost("add")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAddAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string name = requestForm["group-name"];
            string note = requestForm["group-note"];

            MtdGroup mtdGroup = new MtdGroup { Name = name, Description = note };

            await _context.MtdGroup.AddAsync(mtdGroup);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostEditAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string id = requestForm["group-id"];
            string name = requestForm["group-name"];
            string note = requestForm["group-note"];

            MtdGroup mtdGroup = await _context.MtdGroup.FindAsync(id);
            if (mtdGroup == null) { return NotFound(); }

            mtdGroup.Name = name;
            mtdGroup.Description = note;

            _context.MtdGroup.Update(mtdGroup);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string id = requestForm["group-id"];

            MtdGroup mtdGroup = new MtdGroup { Id = id  };

            _context.MtdGroup.Remove(mtdGroup);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
