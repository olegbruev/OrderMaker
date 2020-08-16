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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Data;
using Mtd.OrderMaker.Web.DataHandler.Approval;
using Mtd.OrderMaker.Web.Models.Store;
using Mtd.OrderMaker.Web.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mtd.OrderMaker.Web.Components.Store
{
    public enum FormType { Create, Edit, Details, Print }

    [ViewComponent(Name = "StoreForm")]
    public class Form : ViewComponent
    {
        private readonly OrderMakerContext _context;
        private readonly UserHandler _userHandler;

        public Form(OrderMakerContext orderMakerContext, UserHandler userHandler)
        {
            _context = orderMakerContext;
            _userHandler = userHandler;
        }

        private async Task<IList<MtdFormPart>> GetPartsAsync(string idForm)
        {
            IList<MtdFormPart> result = await _context.MtdFormPart
                .Include(m => m.MtdFormPartHeader)
                .Where(x => x.MtdForm == idForm)
                .OrderBy(s => s.Sequence)
                .ToListAsync();

            return result;
        }

        private async Task<IList<MtdFormPartField>> GetFieldsAsync(IList<MtdFormPart> formParts)
        {
            IList<string> partIds = formParts.Select(x => x.Id).ToList();

            return await _context.MtdFormPartField
                .Include(x => x.MtdFormList)
                .Where(x => partIds.Contains(x.MtdFormPart))
                .OrderBy(s => s.Sequence)
                .ToListAsync();
        }

        private async Task<DataSet> CreateDataSetAsync(MtdStore store, FormType type = FormType.Details)
        {
            if (store == null) return null;
            WebAppUser webAppUser = await _userHandler.GetUserAsync(HttpContext.User);            
            List<MtdFormPart> mtdFormParts = new List<MtdFormPart>();
            IList<MtdFormPart> parts = await GetPartsAsync(store.MtdForm);
            bool isReviewer = await _userHandler.IsReviewer(webAppUser, store.MtdForm);
            ApprovalHandler approvalHandler = new ApprovalHandler(_context, store.Id);
            List<string> blockedParts = new List<string>();
            if (!isReviewer)
            {
                blockedParts = await approvalHandler.GetBlockedPartsIds();
            }
            
            foreach (MtdFormPart formPart in parts)
            {
                if (type == FormType.Edit)
                {                    
                    bool isEditor = await _userHandler.IsEditorPartAsync(webAppUser, formPart.Id);
                    if (isEditor && !blockedParts.Contains(formPart.Id)) { mtdFormParts.Add(formPart);}

                } else
                {
                    bool isViewer = await _userHandler.IsViewerPartAsync(webAppUser, formPart.Id);
                    if (isViewer) { mtdFormParts.Add(formPart); }
                }                
            }

            IList<MtdFormPartField> mtdFormPartFields = await GetFieldsAsync(mtdFormParts);

            var mtdStore = await _context.MtdStore
                .Include(m => m.ParentNavigation)
                .Include(m => m.MtdFormNavigation)
                .ThenInclude(m => m.MtdFormHeader)
                .Include(m => m.MtdFormNavigation)
                .ThenInclude(m => m.ParentNavigation)
                .FirstOrDefaultAsync(m => m.Id == store.Id);

            IList<long> ids = await _context.MtdStoreStack.Where(x => x.MtdStore == mtdStore.Id).Select(x => x.Id).ToListAsync();

            IList<MtdStoreStack> stack = await _context.MtdStoreStack
                .Include(m => m.MtdStoreStackText)
                .Include(m => m.MtdStoreStackDecimal)
                .Include(m => m.MtdStoreStackFile)
                .Include(m => m.MtdStoreStackDate)
                .Include(m => m.MtdStoreStackInt)
                .Include(m => m.MtdStoreLink)
               .Where(x => ids.Contains(x.Id))
               .ToListAsync();


            DataSet result = new DataSet()
            {
                Store = mtdStore,
                Parts = mtdFormParts,
                Fields = mtdFormPartFields,
                Stack = stack,
            };

            return result;

        }


        public async Task<IViewComponentResult> InvokeAsync(MtdStore store, FormType type = FormType.Details)
        {

            if (store == null)
            {
                return View();
            }

            WebAppUser webAppUser = await _userHandler.GetUserAsync(HttpContext.User);

            if (type == FormType.Create)
            {
                store.MtdFormNavigation.MtdFormHeader = await _context.MtdFormHeader.FindAsync(store.MtdForm);
                store.MtdFormNavigation.ParentNavigation = await _context.MtdForm.FindAsync(store.MtdFormNavigation.Parent);

                List<MtdFormPart> mtdFormParts = new List<MtdFormPart>();
                IList<MtdFormPart> parts = await GetPartsAsync(store.MtdForm);

                foreach (MtdFormPart formPart in parts)
                {
                    bool isCreator = await _userHandler.IsCreatorPartAsync(webAppUser, formPart.Id);
                    if (isCreator) { mtdFormParts.Add(formPart); }
                }

                IList<MtdFormPartField> mtdFormPartFields = await GetFieldsAsync(mtdFormParts);

                DataSet dataSetForCreate = new DataSet()
                {
                    Store = store,
                    Parts = mtdFormParts,
                    Fields = mtdFormPartFields,
                    Stack = new List<MtdStoreStack>()
                };

                return View("Create", dataSetForCreate);
            }

            DataContainer dataContainer = new DataContainer
            {
                Owner = await CreateDataSetAsync(store, type),
                Parent = await CreateDataSetAsync(store.ParentNavigation)
            };

            return View(type.ToString(), dataContainer);




        }


    }
}
