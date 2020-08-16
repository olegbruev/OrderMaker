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
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Data;
using Mtd.OrderMaker.Web.DataHandler.Approval;
using Mtd.OrderMaker.Web.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Mtd.OrderMaker.Web;
using Microsoft.AspNetCore.Http;

namespace Mtd.OrderMaker.Web.Controllers.Store
{

    [Route("api/store/approval")]
    [ApiController]
    [Authorize(Roles = "Admin,User")]
    public class ApprovalController : ControllerBase
    {
        private readonly OrderMakerContext _context;
        private readonly UserHandler _userHandler;
        private readonly IEmailSenderBlank _emailSender;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public ApprovalController(OrderMakerContext context, UserHandler userHandler, IEmailSenderBlank emailSender, IStringLocalizer<SharedResource> sharedLocalizer)
        {
            _context = context;
            _userHandler = userHandler;
            _emailSender = emailSender;
            _localizer = sharedLocalizer;
        }

        [HttpPost("approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostApproveAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string storeId = requestForm["id-store"];
            WebAppUser webAppUser = await _userHandler.GetUserAsync(HttpContext.User);
            ApprovalHandler approvalHandler = new ApprovalHandler(_context, storeId);
            bool isApprover = await approvalHandler.IsApproverAsync(webAppUser);
            if (!isApprover) { return NotFound(); }

            bool isOk = await approvalHandler.ActionApprove(webAppUser);
            if (isOk)
            {
                await SendEmailApprove(approvalHandler);
            }
            return Ok();
        }

        [HttpPost("reject")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostRejectAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string storeId = requestForm["id-store"];
            bool completeOk = bool.TryParse(requestForm["checkbox-complete"], out bool completeCheck);
            bool stageOk = int.TryParse(requestForm["next-stage"], out int stageId);
            if (!stageOk || !completeOk) { return NotFound(); }

            WebAppUser webAppUser = await _userHandler.GetUserAsync(HttpContext.User);
            ApprovalHandler approvalHandler = new ApprovalHandler(_context, storeId);

            bool isFirstStage = await approvalHandler.IsFirstStageAsync();
            bool isApprover = await approvalHandler.IsApproverAsync(webAppUser);
            if (!isApprover || isFirstStage) { return NotFound(); }

            bool isOk = await approvalHandler.ActionReject(completeCheck, stageId, webAppUser);
            if (isOk)
            {
                await SendEmailReject(approvalHandler);
            }
            return Ok();
        }


        private async Task<bool> SendEmailApprove(ApprovalHandler approvalHandler)
        {
            
            string ownerId = await approvalHandler.GetOwnerID();
            WebAppUser userCurrent = await _userHandler.GetUserAsync(HttpContext.User);
            WebAppUser userOwner = _userHandler.Users.Where(x => x.Id == ownerId).FirstOrDefault();            
            string storeId = await approvalHandler.GetStoreID();
            MtdForm mtdForm = await approvalHandler.GetFormAsync();

            MtdApprovalStage stageNext = await approvalHandler.GetNextStageAsync();            

            if (stageNext != null)
            {
                WebAppUser userNext = _userHandler.Users.Where(x => x.Id == stageNext.UserId).FirstOrDefault();
                BlankEmail blankEmail = new BlankEmail
                {
                    Subject = _localizer["Approval event"],
                    Email = userNext.Email,
                    Header = _localizer["Approval required"],
                    Content = new List<string> {
                        $"<strong>{_localizer["Document"]} - {mtdForm.Name}</strong>",
                        $"{_localizer["User"]} {userCurrent.Title} {_localizer["approved the document at"]} {DateTime.Now}",
                        $"{_localizer["Click on the link to view the document that required to approve."]}",
                        $"<a href='http://{HttpContext.Request.Host}/workplace/store/details?id={storeId}'>{_localizer["Document link"]}</a>"}
                };

                await _emailSender.SendEmailBlankAsync(blankEmail);
            }

          bool IsFirstStage  = await approvalHandler.IsFirstStageAsync();
            if (!IsFirstStage)
            {

                BlankEmail blankEmail = new BlankEmail
                {
                    Subject = _localizer["Approval event"],
                    Email = userOwner.Email,
                    Header = _localizer["Approval process event"],
                    Content = new List<string> {
                        $"<strong>{_localizer["Document"]} - {mtdForm.Name}</strong>",
                        $"{_localizer["User"]} {userCurrent.Title} {_localizer["approved the document at"]} {DateTime.Now}",
                        $"{_localizer["Click on the link to view the document."]}",
                        $"<a href='http://{HttpContext.Request.Host}/workplace/store/details?id={storeId}'>{_localizer["Document link"]}</a>"}
                };
                await _emailSender.SendEmailBlankAsync(blankEmail);
            }
           
            return true;
        }

        private async Task<bool> SendEmailReject(ApprovalHandler approvalHandler)
        {            
            string ownerId = await approvalHandler.GetOwnerID();
            WebAppUser userCurrent = await _userHandler.GetUserAsync(HttpContext.User);
            WebAppUser userOwner = _userHandler.Users.Where(x => x.Id == ownerId).FirstOrDefault();
            MtdForm mtdForm = await approvalHandler.GetFormAsync();
            string storeId = await approvalHandler.GetStoreID();

            MtdApprovalStage stagePrev = await approvalHandler.GetPrevStage();
            MtdApprovalStage stageFirst = await approvalHandler.GetFirstStageAsync();
            if (stagePrev != null && stagePrev.Id != stageFirst.Id)
            {
                WebAppUser userNext = _userHandler.Users.Where(x => x.Id == stagePrev.UserId).FirstOrDefault();
                BlankEmail blankEmail = new BlankEmail
                {
                    Subject = _localizer["Approval event"],
                    Email = userNext.Email,
                    Header = _localizer["Approval required"],
                    Content = new List<string> {
                        $"<strong>{_localizer["Document"]} - {mtdForm.Name}</strong>",
                        $"{_localizer["User"]} {userCurrent.Title} {_localizer["rejected the document at"]} {DateTime.Now}",
                        $"{_localizer["Click on the link to view the document that required to approve."]}",
                        $"<a href='http://{HttpContext.Request.Host}/workplace/store/details?id={storeId}'>{_localizer["Document link"]}</a>"}
                };


                await _emailSender.SendEmailBlankAsync(blankEmail);
            }

            BlankEmail blankOwner = new BlankEmail
            {
                Subject = _localizer["Approval event"],
                Email = userOwner.Email,
                Header = _localizer["Approval process event"],
                Content = new List<string> {
                        $"<strong>{_localizer["Document"]} - {mtdForm.Name}</strong>",
                        $"{_localizer["User"]} {userCurrent.Title} {_localizer["rejected the document at"]} {DateTime.Now}",
                        $"{_localizer["Click on the link to view the document."]}",
                        $"<a href='http://{HttpContext.Request.Host}/workplace/store/details?id={storeId}'>{_localizer["Document link"]}</a>"}
            };

            await _emailSender.SendEmailBlankAsync(blankOwner);

            return true;
        }

    }
}
