﻿using System;
using System.Threading.Tasks;
using System.Web.Http;
using SiteServer.Abstractions;
using SiteServer.API.Context;
using SiteServer.CMS.Core;
using SiteServer.CMS.Framework;
using SiteServer.CMS.Repositories;

namespace SiteServer.API.Controllers.Pages.Settings.User
{
    
    [RoutePrefix("pages/settings/userConfig")]
    public class PagesUserConfigController : ApiController
    {
        private const string Route = "";

        [HttpGet, Route(Route)]
        public async Task<IHttpActionResult> GetConfig()
        {
            try
            {
                var request = await AuthenticatedRequest.GetAuthAsync();
                if (!request.IsAdminLoggin ||
                    !await request.AdminPermissionsImpl.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsUserConfig))
                {
                    return Unauthorized();
                }

                var config = await DataProvider.ConfigRepository.GetAsync();

                return Ok(new
                {
                    Value = config
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost, Route(Route)]
        public async Task<IHttpActionResult> Submit()
        {
            try
            {
                var request = await AuthenticatedRequest.GetAuthAsync();
                if (!request.IsAdminLoggin ||
                    !await request.AdminPermissionsImpl.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsUserConfig))
                {
                    return Unauthorized();
                }

                var config = await DataProvider.ConfigRepository.GetAsync();

                config.IsUserRegistrationAllowed = request.GetPostBool("isUserRegistrationAllowed");
                config.IsUserRegistrationChecked = request.GetPostBool("isUserRegistrationChecked");
                config.IsUserUnRegistrationAllowed = request.GetPostBool("isUserUnRegistrationAllowed");
                config.UserPasswordMinLength = request.GetPostInt("userPasswordMinLength");
                config.UserPasswordRestriction = TranslateUtils.ToEnum(request.GetPostString("userPasswordRestriction"), PasswordRestriction.None);
                config.UserRegistrationMinMinutes = request.GetPostInt("userRegistrationMinMinutes");
                config.IsUserLockLogin = request.GetPostBool("isUserLockLogin");
                config.UserLockLoginCount = request.GetPostInt("userLockLoginCount");
                config.UserLockLoginType = request.GetPostString("userLockLoginType");
                config.UserLockLoginHours = request.GetPostInt("userLockLoginHours");

                await DataProvider.ConfigRepository.UpdateAsync(config);

                await request.AddAdminLogAsync("修改用户设置");

                return Ok(new
                {
                    Value = config
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}
