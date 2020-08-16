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
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Data;
using Mtd.OrderMaker.Web.DataHandler.Approval;
using Mtd.OrderMaker.Web.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Mtd.OrderMaker.Web.Controllers.Store
{
    [Route("api/store")]
    [ApiController]
    [Authorize(Roles = "Admin,User")]
    public class DataController : ControllerBase
    {
        private readonly OrderMakerContext _context;
        private readonly UserHandler _userHandler;
        private enum TypeAction { Create, Edit };

        public DataController(OrderMakerContext context, UserHandler userHandler)
        {
            _context = context;
            _userHandler = userHandler;
        }

        // POST: api/store/save
        [HttpPost("save")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSaveAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string Id = requestForm["idStore"];

            MtdStore mtdStore = await _context.MtdStore.FirstOrDefaultAsync(x => x.Id == Id);
            if (mtdStore == null)
            {
                return NotFound();
            }

            WebAppUser webAppUser = await _userHandler.GetUserAsync(HttpContext.User);
            bool isEditor = await _userHandler.IsEditor(webAppUser, mtdStore.MtdForm, mtdStore.Id);
            ApprovalHandler approvalHandler = new ApprovalHandler(_context, mtdStore.Id);
            ApprovalStatus approvalStatus = await approvalHandler.GetStatusAsync(webAppUser);

            if (!isEditor || approvalStatus == ApprovalStatus.Rejected || approvalStatus == ApprovalStatus.Waiting)
            {
                return Ok(403);
            }

            MtdLogDocument mtdLog = new MtdLogDocument
            {
                MtdStore = mtdStore.Id,
                TimeCh = DateTime.Now,
                UserId = webAppUser.Id,
                UserName = webAppUser.Title
            };


            OutData outData = await CreateDataAsync(Id, webAppUser, TypeAction.Edit, requestForm);
            List<MtdStoreStack> stackNew = outData.MtdStoreStacks;


            IList<MtdStoreStack> stackOld = await _context.MtdStoreStack
                .Include(m => m.MtdStoreStackText)
                .Include(m => m.MtdStoreStackDecimal)
                .Include(m => m.MtdStoreStackFile)
                .Include(m => m.MtdStoreStackDate)
                .Include(m => m.MtdStoreStackInt)
                .Include(m => m.MtdStoreLink)
                .Where(x => x.MtdStore == Id).ToListAsync();

            foreach (MtdStoreStack stack in stackOld)
            {
                MtdStoreStack stackForField = stackNew.SingleOrDefault(x => x.MtdFormPartField == stack.MtdFormPartField);
                if (stackForField != null)
                {
                    stack.MtdStoreStackText = stackForField.MtdStoreStackText;
                    stack.MtdStoreLink = stackForField.MtdStoreLink;
                    stack.MtdStoreStackDate = stackForField.MtdStoreStackDate;
                    stack.MtdStoreStackDecimal = stackForField.MtdStoreStackDecimal;
                    stack.MtdStoreStackFile = stackForField.MtdStoreStackFile;
                    stack.MtdStoreStackInt = stackForField.MtdStoreStackInt;
                }
            }


            try
            {
                if (stackNew.Count > 0)
                {
                    int count = await _context.MtdStoreLink.Where(x => x.MtdStore == Id).CountAsync();
                    if (count > 0)
                    {
                        string titleText = outData.MtdStoreStacks.FirstOrDefault(x => x.MtdFormPartField == outData.MtdFormPartField.Id).MtdStoreStackText.Register;
                        IList<MtdStoreLink> linkIds = await _context.MtdStoreLink.Where(x => x.MtdStore == Id).Select(x => new MtdStoreLink { Id = x.Id, MtdStore = x.MtdStore, Register = titleText }).ToListAsync();
                        _context.MtdStoreLink.UpdateRange(linkIds);
                    }

                    _context.MtdStoreStack.UpdateRange(stackOld);


                    List<MtdStoreStack> stackNewOnly = stackNew.Where(x => !stackOld.Select(f => f.MtdFormPartField).Contains(x.MtdFormPartField)).ToList();

                    if (stackNewOnly.Any())
                    {
                        await _context.MtdStoreStack.AddRangeAsync(stackNewOnly);
                    }

                    _context.MtdLogDocument.Add(mtdLog);
                    await _context.SaveChangesAsync();
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MtdStoreExists(Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Ok();

        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostCreateAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string idForm = requestForm["idForm"];
            string idFormParent = requestForm["store-parent-id"];

            WebAppUser webAppUser = await _userHandler.GetUserAsync(HttpContext.User);
            bool isCreator = await _userHandler.IsCreator(webAppUser, idForm);
            if (!isCreator)
            {
                return Ok(403);
            }

            await _context.Database.BeginTransactionAsync();
            int sequence;
            var forms = await _context.MtdStore.Where(x => x.MtdForm == idForm).ToListAsync();
            if (forms != null && forms.Count > 0) { sequence = forms.Max(x => x.Sequence); } else sequence = 0;
            sequence++;

            MtdStore mtdStore = new MtdStore {Id = Guid.NewGuid().ToString(), MtdForm = idForm, Sequence = sequence, Parent = idFormParent.Length > 0 ? idFormParent : null };

            await _context.MtdStore.AddAsync(mtdStore);
            await _context.SaveChangesAsync();

            MtdLogDocument mtdLog = new MtdLogDocument
            {
                MtdStore = mtdStore.Id,
                TimeCh = mtdStore.Timecr,
                UserId = webAppUser.Id,
                UserName = webAppUser.Title
            };

            mtdStore.MtdStoreOwner = new MtdStoreOwner
            {
                UserId = webAppUser.Id,
                UserName = webAppUser.Title,
            };


            await _context.MtdLogDocument.AddAsync(mtdLog);

            OutData outParam = await CreateDataAsync(mtdStore.Id, webAppUser, TypeAction.Create, requestForm);
            List<MtdStoreStack> stackNew = outParam.MtdStoreStacks;
            await _context.MtdStoreStack.AddRangeAsync(stackNew);

            await _context.SaveChangesAsync();
            _context.Database.CommitTransaction();

            return Ok();
        }

        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string idStore = requestForm["store-delete-id"];
            MtdStore mtdStore = await _context.MtdStore.FindAsync(idStore);

            if (mtdStore == null)
            {
                return NotFound();
            }

            WebAppUser webAppUser = await _userHandler.GetUserAsync(HttpContext.User);
            bool isEraser = await _userHandler.IsEraser(webAppUser, mtdStore.MtdForm, mtdStore.Id);

            if (!isEraser)
            {
                return Ok(403);
            }


            _context.MtdStore.Remove(mtdStore);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("number/id")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostGetIDAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string result = "";
            string idFormPart = requestForm["idFromParent"];
            string parentNumber = requestForm["store-parent-number"];

            if (parentNumber.Length == 0) { return Ok(result); }

            parentNumber = parentNumber.TrimStart(new char[] { '0' });
            bool isOk = int.TryParse(parentNumber, out int num);

            if (!isOk) { return Ok(result); }

            MtdStore mtdStore = await _context.MtdStore.FirstOrDefaultAsync(x => x.MtdForm == idFormPart & x.Sequence == num);

            if (mtdStore != null)
            {
                result = mtdStore.Id;
            }

            return Ok(result);
        }

        [HttpPost("setowner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSetOwnerAsync()
        {
            IFormCollection requestForm = await Request.ReadFormAsync();
            string idStore = requestForm["setowner-id-store"];
            string idUser = requestForm["setowner-id-user"];

            WebAppUser webAppUser = await _userHandler.FindByIdAsync(idUser);
            
            if (webAppUser != null)
            {

                MtdStoreOwner mtdStoreOwner = await _context.MtdStoreOwner.Include(x=>x.IdNavigation).FirstOrDefaultAsync(x=>x.Id==idStore);                               

                if (mtdStoreOwner == null)
                {

                    string idForm = mtdStoreOwner.IdNavigation.MtdForm;
                    bool IsInstllerOwner = await _userHandler.IsInstallerOwner(webAppUser,idForm,mtdStoreOwner.Id);
                    if (!IsInstllerOwner)
                    {
                        return Forbid();
                    }

                    mtdStoreOwner = new MtdStoreOwner
                    {
                        Id = idStore,
                        UserId = webAppUser.Id,
                        UserName = webAppUser.Title
                    };

                    await _context.MtdStoreOwner.AddAsync(mtdStoreOwner);
                    await _context.SaveChangesAsync();

                    return Ok();

                }

                mtdStoreOwner.UserId = webAppUser.Id;
                mtdStoreOwner.UserName = webAppUser.Title;
                _context.Entry(mtdStoreOwner).State = EntityState.Modified;
                await _context.SaveChangesAsync();

            }

            return Ok();
        }

        private async Task<OutData> CreateDataAsync(string Id, WebAppUser user, TypeAction typeAction, IFormCollection requestForm)
        {
           
            var store = await _context.MtdStore
               .Include(m => m.MtdFormNavigation)
               .ThenInclude(p => p.MtdFormPart)
               .FirstOrDefaultAsync(m => m.Id == Id);
            
            List<string> partsIds = new List<string>();
            bool isReviewer = await _userHandler.IsReviewer(user, store.MtdForm);
            ApprovalHandler approvalHandler = new ApprovalHandler(_context, store.Id);
            List<string> blockedParts = new List<string>();
            if (!isReviewer)
            {
                blockedParts = await approvalHandler.GetBlockedPartsIds();
            }
            
            foreach (var part in store.MtdFormNavigation.MtdFormPart)
            {
                switch (typeAction)
                {
                    case TypeAction.Create:
                        {
                            if (await _userHandler.IsCreatorPartAsync(user, part.Id))
                            {
                                partsIds.Add(part.Id);
                            }
                            break;
                        }
                    default:
                        {
                            if (await _userHandler.IsEditorPartAsync(user, part.Id) && !blockedParts.Contains(part.Id))
                            {
                                partsIds.Add(part.Id);
                            }
                            break;
                        }
                }
            }


            var fields = await _context.MtdFormPartField.Include(m => m.MtdFormPartNavigation)
                .Where(x => partsIds.Contains(x.MtdFormPartNavigation.Id))
                .OrderBy(x => x.MtdFormPartNavigation.Sequence)
                .ThenBy(x => x.Sequence)
                .ToListAsync();

            var titleField = fields.FirstOrDefault(x => x.MtdSysType == 1);

            List<MtdStoreStack> stackNew = new List<MtdStoreStack>();

            foreach (MtdFormPartField field in fields)
            {
                string data = requestForm[field.Id];
                MtdStoreStack mtdStoreStack = new MtdStoreStack()
                {
                    MtdStore = Id,
                    MtdFormPartField = field.Id
                };


                switch (field.MtdSysType)
                {
                    case 2:
                        {
                            if (data != string.Empty)
                            {
                                bool isOkInt = int.TryParse(data, out int result);
                                if (isOkInt)
                                {
                                    mtdStoreStack.MtdStoreStackInt = new MtdStoreStackInt { Register = result };
                                }

                            }
                            break;
                        }
                    case 3:
                        {
                            if (data != string.Empty)
                            {
                                string separ = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                                bool isOkDecimal = decimal.TryParse(data.Replace(".", separ), out decimal result);
                                if (isOkDecimal)
                                {
                                    mtdStoreStack.MtdStoreStackDecimal = new MtdStoreStackDecimal { Register = result };
                                }

                            }
                            break;
                        }
                    case 5:
                    case 6:
                    case 10:
                        {
                            if (data != string.Empty)
                            {
                                bool isOkDate = DateTime.TryParse(data, out DateTime dateTime);
                                if (isOkDate)
                                {
                                    mtdStoreStack.MtdStoreStackDate = new MtdStoreStackDate { Register = dateTime };
                                }

                            }
                            break;
                        }
                    case 7:
                    case 8:
                        {

                            var actionDelete = requestForm[$"{field.Id}-delete"];

                            if (actionDelete.FirstOrDefault() == null || actionDelete.FirstOrDefault() == "false")
                            {

                                IFormFile file = requestForm.Files.FirstOrDefault(x => x.Name == field.Id);

                                if (file != null)
                                {
                                    byte[] streamArray = new byte[file.Length];
                                    await file.OpenReadStream().ReadAsync(streamArray, 0, streamArray.Length);
                                    mtdStoreStack.MtdStoreStackFile = new MtdStoreStackFile()
                                    {
                                        Register = streamArray,
                                        FileName = file.FileName,
                                        FileSize = streamArray.Length,
                                        FileType = file.ContentType
                                    };

                                }

                                if (file == null)
                                {
                                    MtdStoreStack stackOld = await _context.MtdStoreStack
                                        .Include(m => m.MtdStoreStackFile)
                                        .OrderByDescending(x => x.Id)
                                        .FirstOrDefaultAsync(x => x.MtdStore == Id & x.MtdFormPartField == field.Id);

                                    if (stackOld != null && stackOld.MtdStoreStackFile != null)
                                    {
                                        mtdStoreStack.MtdStoreStackFile = new MtdStoreStackFile()
                                        {
                                            FileName = stackOld.MtdStoreStackFile.FileName,
                                            FileSize = stackOld.MtdStoreStackFile.FileSize,
                                            Register = stackOld.MtdStoreStackFile.Register,
                                            FileType = stackOld.MtdStoreStackFile.FileType,
                                        };
                                    }
                                }
                            }

                            break;
                        }

                    case 11:
                        {
                            if (data != string.Empty)
                            {
                                string datalink = Request.Form[$"{field.Id}-datalink"];
                                mtdStoreStack.MtdStoreLink = new MtdStoreLink { MtdStore = data, Register = datalink };
                            }

                            break;
                        }

                    case 12:
                        {
                            bool isOkCheck = bool.TryParse(data, out bool check);
                            if (isOkCheck)
                            {
                                mtdStoreStack.MtdStoreStackInt = new MtdStoreStackInt { Register = check ? 1 : 0 };
                            }
                            break;
                        }

                    default:
                        {
                            if (data != string.Empty)
                            {
                                mtdStoreStack.MtdStoreStackText = new MtdStoreStackText() { Register = data };
                            }
                            break;
                        }
                }

                stackNew.Add(mtdStoreStack);
            }

            OutData outParam = new OutData()
            {
                MtdFormPartField = titleField,
                MtdStoreStacks = stackNew,
            };

            return outParam;
        }

        private bool MtdStoreExists(string id)
        {
            return _context.MtdStore.Any(e => e.Id == id);
        }

    }

    public class OutData
    {
        public List<MtdStoreStack> MtdStoreStacks { get; set; }
        public MtdFormPartField MtdFormPartField { get; set; }
    }
}
