using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mtd.OrderMaker.Web.Areas.Identity.Data;
using Mtd.OrderMaker.Web.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Mtd.OrderMaker.Web.Services
{
    public enum RightsType
    {
        View, Create, Edit, Delete, ViewOwn, EditOwn, DeleteOwn, ViewGroup, EditGroup, DeleteGroup, SetOwn, Reviewer
    };

    public class UserHandler : UserManager<WebAppUser>
    {

        private readonly OrderMakerContext _context;
        private readonly SignInManager<WebAppUser> _signInManager;        

        public UserHandler(OrderMakerContext context,
            IUserStore<WebAppUser> store,
            IOptions<IdentityOptions> optionsAccessor, 
            IPasswordHasher<WebAppUser> passwordHasher, 
            IEnumerable<IUserValidator<WebAppUser>> userValidators, 
            IEnumerable<IPasswordValidator<WebAppUser>> passwordValidators, 
            ILookupNormalizer keyNormalizer, IdentityErrorDescriber errors, 
            IServiceProvider services, ILogger<UserManager<WebAppUser>> logger, SignInManager<WebAppUser> signInManager) : 
            base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
        {
            _context = context;
            _signInManager = signInManager;
        }

        private string RightTypeToString(RightsType rightsType)
        {
            string value = $"-{rightsType.ToString().ToLower()}";
            value = value.Replace("own", "-own");
            value = value.Replace("group", "-group");
            return value;
        }

        public async Task<List<string>> GetFormIdsAsync(WebAppUser user, params RightsType[] rightsTypes)
        {
            List<string> formIds = new List<string>();
            if (user == null) return formIds;

            foreach (RightsType rightsType in rightsTypes)
            {
                string right = RightTypeToString(rightsType);
                IList<Claim> claims = await GetClaimsAsync(user);
                List<string> fids = claims.Where(x => x.Value == right).Select(x => x.Type).ToList();
                formIds.AddRange(fids.Where(x => !formIds.Contains(x)));
            }


            return formIds;
        }

        public async Task<bool> IsRightAsync(WebAppUser user, RightsType rightsType, string idForm)
        {
            bool result;

            string right = RightTypeToString(rightsType);
            IList<Claim> claims = await GetClaimsAsync(user);
            result = claims.Where(x => x.Type == idForm && x.Value == right).Any();

            return result;
        }

        public async Task<bool> IsAdmin(WebAppUser user)
        {
            return await IsInRoleAsync(user, "Admin");
        }

        public async Task<bool> IsOwner(WebAppUser user, string idStore)
        {
            return await _context.MtdStoreOwner.Where(x => x.Id == idStore && x.UserId == user.Id).AnyAsync();
        }

        private async Task<bool> IsRights(string right, WebAppUser user, string idForm, string idStore = null)
        {

            IList<Claim> claims = await GetClaimsAsync(user);

            bool ownRight = claims.Where(x => x.Type == idForm && x.Value == $"{right}-own").Any();
            if (ownRight && idStore != null)
            {
                bool isOkOwner = await _context.MtdStoreOwner.Where(x => x.Id == idStore & x.UserId == user.Id).AnyAsync();
                return isOkOwner;
            }

            bool groupRight = claims.Where(x => x.Type == idForm && x.Value == $"{right}-group").Any();
            if (groupRight && idStore != null)
            {
                List<WebAppUser> users = await GetUsersInGroupsAsync(user);
                List<string> userIds = users.Select(x => x.Id).ToList();
                bool isOkGroup = await _context.MtdStoreOwner.Where(x => x.Id == idStore & userIds.Contains(x.UserId)).AnyAsync();
                return isOkGroup;
            }

            
            bool isOk = claims.Where(x => x.Type == idForm && x.Value == right).Any();
            if (isOk) return true;

            return false;
        }


        public async Task<bool> IsCreator(WebAppUser user, string idForm, string idStore = null)
        {
            return await IsRights("-create", user, idForm);
        }

        public async Task<bool> IsViewer(WebAppUser user, string idForm, string idStore = null)
        {            
            return await IsRights("-view", user, idForm, idStore);
        }


        public async Task<bool> IsEditor(WebAppUser user, string idForm, string idStore = null)
        {
            
            return await IsRights("-edit", user, idForm, idStore);
        }

        public async Task<bool> IsEraser(WebAppUser user, string idForm, string idStore = null)
        {
            return  await IsRights("-delete", user, idForm, idStore);
        }

        public async Task<bool> IsInstallerOwner(WebAppUser user, string idForm, string idStore)
        {
            bool result = await IsRights("-set-own", user, idForm);
            if (!result) return result;

            MtdStoreOwner mtdStoreOwner =  await _context.MtdStoreOwner.FindAsync(idStore);
            if (mtdStoreOwner == null) { return await IsAdmin(user); }

            List<WebAppUser> webAppUsers = await GetUsersInGroupsAsync(user);
            if (webAppUsers.Count == 0 ) { return result; }

            List<string> userIds = webAppUsers.Select(x => x.Id).ToList();
            return webAppUsers.Where(x => userIds.Contains(mtdStoreOwner.UserId)).Any();
            
        }

        public async Task<bool> IsReviewer(WebAppUser user, string idForm, string idStore = null)
        {
            return await IsRights("-reviewer", user, idForm, idStore);
        }

        public async Task<bool> IsCreatorPartAsync(WebAppUser user, string idPart)
        {
            IList<Claim> claims = await GetClaimsAsync(user);
            return claims.Where(x => x.Type == idPart && x.Value == "-part-create").Any();
        }

        public async Task<bool> IsEditorPartAsync(WebAppUser user, string idPart)
        {
            IList<Claim> claims = await GetClaimsAsync(user);
            return claims.Where(x => x.Type == idPart && x.Value == "-part-edit").Any();
        }

        public async Task<bool> IsViewerPartAsync(WebAppUser user, string idPart)
        {
            IList<Claim> claims = await GetClaimsAsync(user);
            return claims.Where(x => x.Type == idPart && x.Value == "-part-view").Any();
        }

        public async Task<List<string>> GetAllowPartsForView(WebAppUser user, string idForm)
        {
            List<string> idsAll = await _context.MtdFormPart.Where(x => x.MtdForm == idForm).Select(x => x.Id).ToListAsync();
            IList<Claim> claims = await GetClaimsAsync(user);
            return claims.Where(x => idsAll.Contains(x.Type) && x.Value == "-part-view").Select(x => x.Type).ToList();
        }
        
        public async Task<List<WebAppUser>> GetUsersInGroupsAsync(WebAppUser webAppUser)
        {
            List<WebAppUser> webAppUsers = new List<WebAppUser>();
            IList<Claim> claims = await GetClaimsAsync(webAppUser);
            IList<Claim> groups = claims.Where(c => c.Value == "-group").ToList();

            foreach (var claim in groups)
            {
                IList<WebAppUser> users = await GetUsersForClaimAsync(claim);
                if (users != null)
                {
                    var temp = users.Where(x => !webAppUsers.Select(w => w.Id).Contains(x.Id)).ToList();
                    if (temp != null)
                    {
                        webAppUsers.AddRange(temp);
                    }
                }
            }

            return webAppUsers;
        }

        public override async Task<WebAppUser> GetUserAsync(ClaimsPrincipal principal)
        {
            if (principal == null)
            {
                throw new ArgumentNullException(nameof(principal));
            }
            var id = GetUserId(principal);
            if (id == null)  { return null; }
            WebAppUser user = await FindByIdAsync(id);
            if (user == null)
            {
                await _signInManager.SignOutAsync();
                return null;
            }

            return user;
        }        

    }
}
