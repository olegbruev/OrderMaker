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

using Microsoft.EntityFrameworkCore;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mtd.OrderMaker.Web.DataHandler.Approval
{
    public class ApprovalHandler
    {
        private readonly OrderMakerContext _context;
        private readonly string idStore;
        private MtdStore storeCache;
        private MtdApproval approvalCache;

        public static async Task<List<string>> GetWaitStoreIds(OrderMakerContext context, WebAppUser user, string formId)
        {
            List<string> approvalIds = await context.MtdApproval.Where(x => x.MtdForm == formId).Select(x => x.Id).ToListAsync();
            List<int> stagesUser = await context.MtdApprovalStage.Where(x => approvalIds.Contains(x.MtdApproval) && x.UserId == user.Id).Select(x => x.Id).ToListAsync();
            return await context.MtdStoreApproval.Where(x => x.Complete == 0 && stagesUser.Contains(x.MtdApproveStage)).Select(x => x.Id).ToListAsync();
        }

        public static async Task<List<ApprovalStore>> GetStoreStatusAsync(OrderMakerContext context, IList<string> storeIds, WebAppUser appUser)
        {
            List<ApprovalStore> result = new List<ApprovalStore>();

            IList<MtdStoreApproval> mtdStoreApprovals = await context.MtdStoreApproval
                .Include(x => x.MdApproveStageNavigation)
                .Where(x => storeIds.Contains(x.Id))
                .ToListAsync();

            List<string> approvalIds = mtdStoreApprovals.Select(x => x.MdApproveStageNavigation.MtdApproval).ToList();
            IList<MtdApprovalStage> stages = await context.MtdApprovalStage
                .Where(x => approvalIds.Contains(x.MtdApproval))
                .OrderBy(x => x.Stage).ToListAsync();

            foreach (string storeId in storeIds)
            {
                ApprovalStore approvalStore = new ApprovalStore
                {
                    StoreId = storeId
                };
                MtdStoreApproval sa = mtdStoreApprovals.Where(x => x.Id == storeId).FirstOrDefault();
                if (sa == null)
                {
                    approvalStore.Status = ApprovalStatus.Start;
                }
                else
                {
                    bool isComplete = sa.Complete == 1 ? true : false;
                    int approved = sa.Result;
                    string approvalId = sa.MdApproveStageNavigation.MtdApproval;
                    int currentId = sa.MtdApproveStage;

                    int firstId = stages.Where(x => x.MtdApproval == approvalId)
                        .OrderBy(x => x.Stage).Select(x => x.Id).FirstOrDefault();

                    bool isFirst = currentId == firstId ? true : false;
                    bool isApprover = sa.MdApproveStageNavigation.UserId == appUser.Id ? true : false;
                    approvalStore.Status = DefineStatus(isComplete, approved, isFirst, isApprover);
                }

                result.Add(approvalStore);
            }

            return result;
        }

        public static async Task<bool> IsApprovalFormAsync(OrderMakerContext context, string formId)
        {
            bool isApprovalForm = await context.MtdApproval.Where(x => x.MtdForm == formId).AnyAsync();
            return isApprovalForm;
        }

        private async Task<MtdStore> GetStoreAsync()
        {
            if (storeCache != null) return storeCache;
            storeCache = await _context.MtdStore.AsNoTracking()
                .Include(x => x.MtdFormNavigation)
                .Include(x => x.MtdStoreApproval)
                .Include(x => x.MtdStoreOwner)
                .Where(x => x.Id == idStore)
                .FirstOrDefaultAsync();

            return storeCache;
        }

        private async Task<MtdApproval> GetApproval()
        {
            if (approvalCache != null) return approvalCache;
            MtdStore mtdStore = await GetStoreAsync();
            return await _context.MtdApproval.AsNoTracking()
                .Include(x => x.MtdApprovalStage)
                .Where(x => x.MtdForm == mtdStore.MtdForm)
                .FirstOrDefaultAsync();
        }

        public ApprovalHandler(OrderMakerContext context, string idstore)
        {
            _context = context;
            idStore = idstore;
            storeCache = null;
            approvalCache = null;
        }

        public void ClearCache()
        {
            approvalCache = null;
            storeCache = null;
        }

        public async Task<bool> IsApprovalFormAsync()
        {
            MtdApproval mtdApproval = await GetApproval();
            return mtdApproval == null ? false : true;
        }

        public async Task<MtdApprovalStage> GetCurrentStageAsync()
        {
            MtdApproval approval = await GetApproval();
            if (approval == null) return null;

            MtdStore mtdStore = await GetStoreAsync();
            if (mtdStore.MtdStoreApproval == null) return await GetFirstStageAsync();
            return approval.MtdApprovalStage.Where(x => x.Id == mtdStore.MtdStoreApproval.MtdApproveStage).FirstOrDefault();
        }

        public async Task<bool> IsApproverAsync(WebAppUser user)
        {
            bool isComplete = await IsComplete();
            bool isApprovalForm = await IsApprovalFormAsync();
            if (!isApprovalForm || isComplete) { return false; }

            MtdApprovalStage mtdApprovalStage = await GetCurrentStageAsync();
            MtdStore store = await GetStoreAsync();
            bool forOwner = false;
            if (store.MtdStoreOwner != null)
            {
                MtdApprovalStage firstStage = await GetFirstStageAsync();
                if (firstStage != null)
                {
                    forOwner = (store.MtdStoreOwner.UserId.Equals(user.Id) && mtdApprovalStage.Id.Equals(firstStage.Id));
                }
            }

            if (mtdApprovalStage != null && (mtdApprovalStage.UserId.Equals(user.Id) || forOwner)) { return true; }

            return false;

        }

        public async Task<bool> IsFirstStageAsync()
        {
            MtdApprovalStage first = await GetCurrentStageAsync();
            MtdApprovalStage current = await GetFirstStageAsync();
            if (current == null) { return false; }
            return first.Id.Equals(current.Id);
        }

        public async Task<bool> IsComplete()
        {
            bool result = false;
            MtdStore mtdStore = await GetStoreAsync();
            if (mtdStore.MtdStoreApproval != null && mtdStore.MtdStoreApproval.Complete == 1)
            {
                result = true;
            }
            return result;
        }

        public async Task<IList<MtdApprovalStage>> GetStagesAsync()
        {
            MtdApproval mtdApproval = await GetApproval();
            if (mtdApproval != null)
            {
                return mtdApproval.MtdApprovalStage.OrderBy(x => x.Stage).ToList();
            }

            return new List<MtdApprovalStage>();
        }

        public async Task<List<MtdApprovalStage>> GetStagesDownAsync()
        {
            MtdApproval mtdApproval = await GetApproval();
            if (mtdApproval != null)
            {
                MtdApprovalStage currentStage = await GetCurrentStageAsync();
                return mtdApproval.MtdApprovalStage.Where(x => x.Stage < currentStage.Stage).OrderBy(x => x.Stage).ToList();
            }

            return new List<MtdApprovalStage>();
        }

        public async Task<MtdApprovalStage> GetFirstStageAsync()
        {
            IList<MtdApprovalStage> mtdApprovalStages = await GetStagesAsync();
            return mtdApprovalStages.FirstOrDefault();
        }

        public async Task<MtdApprovalStage> GetLastStageAsync()
        {
            IList<MtdApprovalStage> mtdApprovalStages = await GetStagesAsync();
            return mtdApprovalStages.OrderByDescending(x => x.Stage).FirstOrDefault();
        }

        public async Task<MtdApprovalStage> GetPrevStage()
        {
            MtdApprovalStage prevStage = await GetCurrentStageAsync();
            IList<MtdApprovalStage> stages = await GetStagesAsync();
            var stage = stages.Where(x => x.Stage < prevStage.Stage).FirstOrDefault();
            if (stage != null) { prevStage = stage; }
            return prevStage;
        }

        public async Task<MtdApprovalStage> GetNextStageAsync()
        {
            MtdApprovalStage current = await GetCurrentStageAsync();
            IList<MtdApprovalStage> stages = await GetStagesAsync();
            return stages.Where(x => x.Stage > current.Stage).FirstOrDefault();

        }

        public async Task<MtdForm> GetFormAsync()
        {
            MtdStore mtdStore = await GetStoreAsync();
            return mtdStore.MtdFormNavigation;
        }

        public async Task<bool> ActionApprove(WebAppUser webAppUser)
        {
            MtdStore mtdStore = await GetStoreAsync();
            if (mtdStore.MtdStoreApproval != null && mtdStore.MtdStoreApproval.Complete == 1) { return false; }

            MtdApprovalStage currentStage = await GetCurrentStageAsync();
            MtdApprovalStage nextStage = await GetNextStageAsync();
            sbyte complete = 0;
            if (nextStage == null) { complete = 1; nextStage = await GetLastStageAsync(); };

            MtdStoreApproval storeApproval = new MtdStoreApproval
            {
                Id = mtdStore.Id,
                MtdApproveStage = nextStage.Id,
                PartsApproved = currentStage.BlockParts,
                Complete = complete,
                Result = 1,
            };

            if (mtdStore.MtdStoreApproval == null)
            {
                await _context.MtdStoreApproval.AddAsync(storeApproval);
            }
            else
            {
                _context.MtdStoreApproval.Update(storeApproval);
            }

            MtdLogApproval mtdLogApproval = new MtdLogApproval()
            {
                MtdStore = mtdStore.Id,
                Result = 1,
                Stage = currentStage.Id,
                Timecr = DateTime.Now,
                UserId = webAppUser.Id
            };

            await _context.MtdLogApproval.AddAsync(mtdLogApproval);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw ex.InnerException;
            }

            return true;
        }

        public async Task<bool> ActionReject(bool complete, int idStage, WebAppUser webAppUser)
        {
            MtdStore mtdStore = await GetStoreAsync();
            if (mtdStore.MtdStoreApproval != null && mtdStore.MtdStoreApproval.Complete == 1) { return false; }

            MtdApproval mtdApproval = await GetApproval();
            MtdApprovalStage currentStage = await GetCurrentStageAsync();
            MtdApprovalStage prevStage;
            if (!complete)
            {
                prevStage = mtdApproval.MtdApprovalStage.Where(x => x.Id == idStage).FirstOrDefault();
            }
            else
            {
                prevStage = await GetCurrentStageAsync();
            }

            if (prevStage == null) { return false; }

            var allStages = await GetStagesAsync();
            var blockPartsStage = allStages.Where(x => x.Stage < prevStage.Stage).FirstOrDefault();

            MtdStoreApproval storeApproval = new MtdStoreApproval
            {
                Id = mtdStore.Id,
                MtdApproveStage = prevStage.Id,
                PartsApproved = blockPartsStage == null ? "&" : blockPartsStage.BlockParts,
                Complete = complete ? (sbyte)1 : (sbyte)0,
                Result = -1,
            };

            if (mtdStore.MtdStoreApproval == null)
            {
                await _context.MtdStoreApproval.AddAsync(storeApproval);
            }
            else
            {
                _context.MtdStoreApproval.Update(storeApproval);
            }

            MtdLogApproval mtdLogApproval = new MtdLogApproval()
            {
                MtdStore = mtdStore.Id,
                Result = -1,
                Stage = currentStage.Stage,
                Timecr = DateTime.Now,
                UserId = webAppUser.Id
            };

            await _context.MtdLogApproval.AddAsync(mtdLogApproval);



            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                return false;
            }

            return true;
        }


        public async Task<List<string>> GetBlockedPartsIds()
        {

            MtdStore mtdStore = await GetStoreAsync();
            List<string> ids = new List<string>();
            if (mtdStore.MtdStoreApproval != null)
            {
                ids = mtdStore.MtdStoreApproval.PartsApproved.Split("&").ToList();
                if (ids.Any())
                {
                    ids.RemoveAt(ids.Count - 1);
                }
            }

            return ids;
        }

        public async Task<List<string>> GetWilBeBlockedPartsIds()
        {
            List<string> ids = new List<string>();
            MtdApprovalStage mtdApprovalStage = await GetCurrentStageAsync();
            if (mtdApprovalStage != null)
            {
                ids = mtdApprovalStage.BlockParts.Split("&").ToList();
                if (ids.Any())
                {
                    ids.RemoveAt(ids.Count - 1);
                }
            }

            return ids;
        }

        public async Task<string> GetOwnerID()
        {
            MtdStore mtdStore = await GetStoreAsync();
            if (mtdStore.MtdStoreOwner == null) return string.Empty;
            return mtdStore.MtdStoreOwner.UserId;
        }

        public async Task<string> GetStoreID()
        {
            string result = string.Empty;
            MtdStore mtdStore = await GetStoreAsync();
            if (mtdStore != null) { result = mtdStore.Id; }
            return result;
        }

        public async Task<ApprovalStatus> GetStatusAsync(WebAppUser appUser)
        {
            ApprovalStatus status = ApprovalStatus.Start;
            MtdApprovalStage stage = await GetCurrentStageAsync();
            if (stage == null) return status;

            bool isComplete = await IsComplete();
            int result = await GetResultAsync();
            bool isFirst = await IsFirstStageAsync();
            bool isApprover = await IsApproverAsync(appUser);

            status = DefineStatus(isComplete, result, isFirst, isApprover);

            return status;
        }

        private static ApprovalStatus DefineStatus(bool isComplete, int result, bool isFirst, bool isApprover)
        {
            ApprovalStatus status = ApprovalStatus.Start;

            switch (result)
            {
                case 0:
                    {
                        status = ApprovalStatus.Start;
                        break;
                    }
                case -1:
                    {
                        status = ApprovalStatus.Rejected;
                        break;
                    }
                case 1:
                    {
                        status = ApprovalStatus.Approved;
                        break;
                    }
            }

            if (!isComplete && result != 0) { status = ApprovalStatus.Waiting; }

            if (!isComplete && isFirst && result == -1)
            {
                status = ApprovalStatus.Iteraction;
            }

            if (!isComplete && isApprover && !isFirst)
            {
                status = ApprovalStatus.Required;
            }

            return status;
        }

        public async Task<int> GetResultAsync()
        {
            MtdStore mtdStore = await GetStoreAsync();

            if (mtdStore.MtdStoreApproval != null)
            {
                return mtdStore.MtdStoreApproval.Result;
            }

            return 0;
        }
    }
}
