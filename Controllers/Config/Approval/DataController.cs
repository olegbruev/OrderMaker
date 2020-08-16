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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mtd.OrderMaker.Web.Data;

namespace Mtd.OrderMaker.Web.Controllers.Config.Approval
{
    [Route("api/config/approval")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class DataController : ControllerBase
    {

        private readonly OrderMakerContext _context;


        public DataController(OrderMakerContext context)
        {
            _context = context;
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string approvalId = requestForm["IdApproval"];
            if (approvalId == null)
            {
                return NotFound();
            }

            MtdApproval mtdApproval = new MtdApproval { Id = approvalId };
            _context.MtdApproval.Remove(mtdApproval);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("stage/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostStageCreateAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string approvalId = requestForm["IdApproval"];
            string fieldName = requestForm["fieldName"];
            string fieldNote = requestForm["fieldNote"];
            string fieldUser = requestForm["fieldUser"];
            string fieldStageStr = requestForm["fieldStage"];
            int.TryParse(fieldStageStr, out int fieldStage);
            string blockParts = string.Empty;

            if (approvalId == null)
            {
                return NotFound();
            }

            MtdApproval mtdApproval = await _context.MtdApproval.FindAsync(approvalId);
            IList<string> partIds = await _context.MtdFormPart.Where(x => x.MtdForm == mtdApproval.MtdForm).Select(x => x.Id).ToListAsync();

            foreach (string id in partIds)
            {
                bool result = bool.TryParse(Request.Form[id], out bool check);
                if (result && check)
                {
                    blockParts += $"{id}&";
                }
            }

            MtdApprovalStage stage = new MtdApprovalStage
            {
                MtdApproval = approvalId,
                BlockParts = blockParts,
                Description = fieldNote,
                Name = fieldName,
                UserId = fieldUser,
                Stage = fieldStage,

            };

            await _context.MtdApprovalStage.AddAsync(stage);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("stage/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostStageEditAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string stageId = requestForm["IdStage"];
            string fieldName = requestForm["fieldName"];
            string fieldNote = requestForm["fieldNote"];
            string fieldUser = requestForm["fieldUser"];
            string fieldStageStr = requestForm["fieldStage"];

            int.TryParse(fieldStageStr, out int fieldStage);
            string blockParts = string.Empty;

            if (stageId == null || !int.TryParse(stageId, out int id))
            {
                return NotFound();
            }

            
            MtdApprovalStage mtdApprovalStage = await _context.MtdApprovalStage.Include(x=>x.MtdApprovalNavigation).Where(x=>x.Id==id).FirstOrDefaultAsync();

            if (mtdApprovalStage == null) { return NotFound(); }
            IList<string> partIds = await _context.MtdFormPart.Where(x => x.MtdForm == mtdApprovalStage.MtdApprovalNavigation.MtdForm).Select(x => x.Id).ToListAsync();

            foreach (string idPart in partIds)
            {
                bool result = bool.TryParse(Request.Form[idPart], out bool check);
                if (result && check)
                {
                    blockParts += $"{idPart}&";
                }
            }

            mtdApprovalStage.BlockParts = blockParts;
            mtdApprovalStage.Description = fieldNote;
            mtdApprovalStage.Name = fieldName;
            mtdApprovalStage.UserId = fieldUser;
            mtdApprovalStage.Stage = fieldStage;

            _context.MtdApprovalStage.Update(mtdApprovalStage);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("stage/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostStageDeleteAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string stageId = requestForm["id-stage-delete"];
            bool ok = int.TryParse(stageId, out int id);
            if (stageId == null && !ok)
            {
                return NotFound();
            }

            MtdApprovalStage stage = new MtdApprovalStage { Id = id };
            _context.MtdApprovalStage.Remove(stage);
            await _context.SaveChangesAsync();
            return Ok();
        }


        [HttpPost("stage/sequence")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostStageSequenceAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string strData = requestForm["drgData"];
            string approvalId = requestForm["IdApproval"];
            string formId = requestForm["fieldForm"];

            string[] data = strData.Split("&");

            IList<MtdApprovalStage> stages = await _context.MtdApprovalStage.Where(x => x.MtdApproval == approvalId).ToListAsync();

            int counter = 1;
            foreach (string idStr in data)
            {
                bool isOk = int.TryParse(idStr, out int id);
                if (isOk)
                {
                    var field = stages.Where(x => x.Id == id).FirstOrDefault();
                    if (field != null)
                    {
                        field.Stage = counter;
                        counter++;
                    }
                }

            }

            _context.MtdApprovalStage.UpdateRange(stages);
            await _context.SaveChangesAsync();

            return Ok();
        }

    }
}