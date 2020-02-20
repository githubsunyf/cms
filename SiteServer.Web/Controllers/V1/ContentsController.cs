﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using NSwag.Annotations;
using SiteServer.Abstractions;
using SiteServer.API.Context;
using SiteServer.CMS.Core;
using SiteServer.CMS.Framework;
using SiteServer.CMS.Plugin;

namespace SiteServer.API.Controllers.V1
{
    [RoutePrefix("v1/contents")]
    public partial class ContentsController : ApiController
    {
        private const string Route = "";
        private const string RouteCheck = "check";
        private const string RouteChannel = "{siteId:int}/{channelId:int}";
        private const string RouteContent = "{siteId:int}/{channelId:int}/{id:int}";

        private readonly ICreateManager _createManager;

        public ContentsController(ICreateManager createManager)
        {
            _createManager = createManager;
        }

        [OpenApiOperation("添加内容API", "")]
        [HttpPost, Route(RouteChannel)]
        public async Task<IHttpActionResult> Create(int siteId, int channelId)
        {
            var request = await AuthenticatedRequest.GetAuthAsync();
            var sourceId = request.GetPostInt(ContentAttribute.SourceId.ToCamelCase());
            bool isAuth;
            if (sourceId == SourceManager.User)
            {
                isAuth = request.IsUserLoggin && await request.UserPermissions.HasChannelPermissionsAsync(siteId, channelId, Constants.ChannelPermissions.ContentAdd);
            }
            else
            {
                isAuth = request.IsApiAuthenticated && await
                             DataProvider.AccessTokenRepository.IsScopeAsync(request.ApiToken, Constants.ScopeContents) ||
                         request.IsUserLoggin &&
                         await request.UserPermissions.HasChannelPermissionsAsync(siteId, channelId,
                             Constants.ChannelPermissions.ContentAdd) ||
                         request.IsAdminLoggin &&
                         await request.AdminPermissions.HasChannelPermissionsAsync(siteId, channelId,
                             Constants.ChannelPermissions.ContentAdd);
            }
            if (!isAuth) return Unauthorized();

            var site = await DataProvider.SiteRepository.GetAsync(siteId);
            if (site == null) return BadRequest("无法确定内容对应的站点");

            var channelInfo = await DataProvider.ChannelRepository.GetAsync(channelId);
            if (channelInfo == null) return BadRequest("无法确定内容对应的栏目");

            var attributes = request.GetPostObject<Dictionary<string, object>>();
            if (attributes == null) return BadRequest("无法从body中获取内容实体");
            var checkedLevel = request.GetPostInt("checkedLevel");

            var isChecked = checkedLevel >= site.CheckContentLevel;
            if (isChecked)
            {
                if (sourceId == SourceManager.User || request.IsUserLoggin)
                {
                    isChecked = await request.UserPermissionsImpl.HasChannelPermissionsAsync(siteId, channelId,
                        Constants.ChannelPermissions.ContentCheckLevel1);
                }
                else if (request.IsAdminLoggin)
                {
                    isChecked = await request.AdminPermissionsImpl.HasChannelPermissionsAsync(siteId, channelId,
                        Constants.ChannelPermissions.ContentCheckLevel1);
                }
            }

            var contentInfo = new Content(attributes)
            {
                SiteId = siteId,
                ChannelId = channelId,
                AdminId = request.AdminId,
                LastEditAdminId = request.AdminId,
                UserId = request.UserId,
                LastEditDate = DateTime.Now,
                SourceId = sourceId,
                Checked = isChecked,
                CheckedLevel = checkedLevel
            };

            contentInfo.Id = await DataProvider.ContentRepository.InsertAsync(site, channelInfo, contentInfo);

            foreach (var service in await PluginManager.GetServicesAsync())
            {
                try
                {
                    service.OnContentFormSubmit(new ContentFormSubmitEventArgs(siteId, channelId, contentInfo.Id, attributes, contentInfo));
                }
                catch (Exception ex)
                {
                    await DataProvider.ErrorLogRepository.AddErrorLogAsync(service.PluginId, ex, nameof(IPluginService.ContentFormSubmit));
                }
            }

            if (contentInfo.Checked)
            {
                await _createManager.CreateContentAsync(siteId, channelId, contentInfo.Id);
                await _createManager.TriggerContentChangedEventAsync(siteId, channelId);
            }

            await request.AddSiteLogAsync(siteId, channelId, contentInfo.Id, "添加内容",
                $"栏目:{await DataProvider.ChannelRepository.GetChannelNameNavigationAsync(siteId, contentInfo.ChannelId)},内容标题:{contentInfo.Title}");

            return Ok(new
            {
                Value = contentInfo
            });
        }

        [OpenApiOperation("修改内容API", "")]
        [HttpPut, Route(RouteContent)]
        public async Task<IHttpActionResult> Update(int siteId, int channelId, int id)
        {
            var request = await AuthenticatedRequest.GetAuthAsync();
            var sourceId = request.GetPostInt(ContentAttribute.SourceId.ToCamelCase());
            bool isAuth;
            if (sourceId == SourceManager.User)
            {
                isAuth = request.IsUserLoggin && await request.UserPermissions.HasChannelPermissionsAsync(siteId, channelId, Constants.ChannelPermissions.ContentEdit);
            }
            else
            {
                isAuth = request.IsApiAuthenticated && await
                             DataProvider.AccessTokenRepository.IsScopeAsync(request.ApiToken, Constants.ScopeContents) ||
                         request.IsUserLoggin &&
                         await request.UserPermissions.HasChannelPermissionsAsync(siteId, channelId,
                             Constants.ChannelPermissions.ContentEdit) ||
                         request.IsAdminLoggin &&
                         await request.AdminPermissions.HasChannelPermissionsAsync(siteId, channelId,
                             Constants.ChannelPermissions.ContentEdit);
            }
            if (!isAuth) return Unauthorized();

            var site = await DataProvider.SiteRepository.GetAsync(siteId);
            if (site == null) return BadRequest("无法确定内容对应的站点");

            var channelInfo = await DataProvider.ChannelRepository.GetAsync(channelId);
            if (channelInfo == null) return BadRequest("无法确定内容对应的栏目");

            var attributes = request.GetPostObject<Dictionary<string, object>>();
            if (attributes == null) return BadRequest("无法从body中获取内容实体");

            var contentInfo = await DataProvider.ContentRepository.GetAsync(site, channelInfo, id);
            if (contentInfo == null) return NotFound();

            foreach (var attribute in attributes)
            {
                contentInfo.Set(attribute.Key, attribute.Value);
            }

            contentInfo.SiteId = siteId;
            contentInfo.ChannelId = channelId;
            contentInfo.LastEditAdminId = request.AdminId;
            contentInfo.LastEditDate = DateTime.Now;
            contentInfo.SourceId = sourceId;

            var postCheckedLevel = request.GetPostInt(ContentAttribute.CheckedLevel.ToCamelCase());
            var isChecked = postCheckedLevel >= site.CheckContentLevel;
            var checkedLevel = postCheckedLevel;

            contentInfo.Checked = isChecked;
            contentInfo.CheckedLevel = checkedLevel;

            await DataProvider.ContentRepository.UpdateAsync(site, channelInfo, contentInfo);

            foreach (var service in await PluginManager.GetServicesAsync())
            {
                try
                {
                    service.OnContentFormSubmit(new ContentFormSubmitEventArgs(siteId, channelId, contentInfo.Id, attributes, contentInfo));
                }
                catch (Exception ex)
                {
                    await DataProvider.ErrorLogRepository.AddErrorLogAsync(service.PluginId, ex, nameof(IPluginService.ContentFormSubmit));
                }
            }

            if (contentInfo.Checked)
            {
                await _createManager.CreateContentAsync(siteId, channelId, contentInfo.Id);
                await _createManager.TriggerContentChangedEventAsync(siteId, channelId);
            }

            await request.AddSiteLogAsync(siteId, channelId, contentInfo.Id, "修改内容",
                $"栏目:{await DataProvider.ChannelRepository.GetChannelNameNavigationAsync(siteId, contentInfo.ChannelId)},内容标题:{contentInfo.Title}");

            return Ok(new
            {
                Value = contentInfo
            });
        }

        [OpenApiOperation("删除内容API", "")]
        [HttpDelete, Route(RouteContent)]
        public async Task<IHttpActionResult> Delete(int siteId, int channelId, int id)
        {
            var request = await AuthenticatedRequest.GetAuthAsync();
            var sourceId = request.GetPostInt(ContentAttribute.SourceId.ToCamelCase());
            bool isAuth;
            if (sourceId == SourceManager.User)
            {
                isAuth = request.IsUserLoggin && await request.UserPermissions.HasChannelPermissionsAsync(siteId, channelId, Constants.ChannelPermissions.ContentDelete);
            }
            else
            {
                isAuth = request.IsApiAuthenticated && await
                             DataProvider.AccessTokenRepository.IsScopeAsync(request.ApiToken, Constants.ScopeContents) ||
                         request.IsUserLoggin &&
                         await request.UserPermissions.HasChannelPermissionsAsync(siteId, channelId,
                             Constants.ChannelPermissions.ContentDelete) ||
                         request.IsAdminLoggin &&
                         await request.AdminPermissions.HasChannelPermissionsAsync(siteId, channelId,
                             Constants.ChannelPermissions.ContentDelete);
            }
            if (!isAuth) return Unauthorized();

            var site = await DataProvider.SiteRepository.GetAsync(siteId);
            if (site == null) return BadRequest("无法确定内容对应的站点");

            var channel = await DataProvider.ChannelRepository.GetAsync(channelId);
            if (channel == null) return BadRequest("无法确定内容对应的栏目");

            if (!await request.AdminPermissionsImpl.HasChannelPermissionsAsync(siteId, channelId,
                Constants.ChannelPermissions.ContentDelete)) return Unauthorized();

            var contentInfo = await DataProvider.ContentRepository.GetAsync(site, channel, id);
            if (contentInfo == null) return NotFound();

            await DataProvider.ContentRepository.DeleteAsync(site, channel, id);

            return Ok(new
            {
                Value = contentInfo
            });
        }

        [OpenApiOperation("获取内容API", "")]
        [HttpGet, Route(RouteContent)]
        public async Task<IHttpActionResult> Get(int siteId, int channelId, int id)
        {
            var request = await AuthenticatedRequest.GetAuthAsync();
            var sourceId = request.GetPostInt(ContentAttribute.SourceId.ToCamelCase());
            bool isAuth;
            if (sourceId == SourceManager.User)
            {
                isAuth = request.IsUserLoggin && await request.UserPermissions.HasChannelPermissionsAsync(siteId, channelId, Constants.ChannelPermissions.ContentView);
            }
            else
            {
                isAuth = request.IsApiAuthenticated && await
                             DataProvider.AccessTokenRepository.IsScopeAsync(request.ApiToken, Constants.ScopeContents) ||
                         request.IsUserLoggin &&
                         await request.UserPermissions.HasChannelPermissionsAsync(siteId, channelId,
                             Constants.ChannelPermissions.ContentView) ||
                         request.IsAdminLoggin &&
                         await request.AdminPermissions.HasChannelPermissionsAsync(siteId, channelId,
                             Constants.ChannelPermissions.ContentView);
            }
            if (!isAuth) return Unauthorized();

            var site = await DataProvider.SiteRepository.GetAsync(siteId);
            if (site == null) return BadRequest("无法确定内容对应的站点");

            var channelInfo = await DataProvider.ChannelRepository.GetAsync(channelId);
            if (channelInfo == null) return BadRequest("无法确定内容对应的栏目");

            if (!await request.AdminPermissionsImpl.HasChannelPermissionsAsync(siteId, channelId,
                Constants.ChannelPermissions.ContentView)) return Unauthorized();

            var contentInfo = await DataProvider.ContentRepository.GetAsync(site, channelInfo, id);
            if (contentInfo == null) return NotFound();

            return Ok(new
            {
                Value = contentInfo
            });
        }

        [OpenApiOperation("获取内容列表API", "")]
        [HttpPost, Route(Route)]
        public async Task<QueryResult> GetContents([FromBody] QueryRequest request)
        {
            var req = await AuthenticatedRequest.GetAuthAsync();
            var sourceId = req.GetPostInt(ContentAttribute.SourceId.ToCamelCase());
            var channelId = request.ChannelId ?? request.SiteId;

            bool isAuth;
            if (sourceId == SourceManager.User)
            {
                
                isAuth = req.IsUserLoggin && await req.UserPermissions.HasChannelPermissionsAsync(request.SiteId, channelId, Constants.ChannelPermissions.ContentView);
            }
            else
            {
                isAuth = req.IsApiAuthenticated && await
                             DataProvider.AccessTokenRepository.IsScopeAsync(req.ApiToken, Constants.ScopeContents) ||
                         req.IsUserLoggin &&
                         await req.UserPermissions.HasChannelPermissionsAsync(request.SiteId, channelId,
                             Constants.ChannelPermissions.ContentView) ||
                         req.IsAdminLoggin &&
                         await req.AdminPermissions.HasChannelPermissionsAsync(request.SiteId, channelId,
                             Constants.ChannelPermissions.ContentView);
            }
            if (!isAuth) return Request.Unauthorized<QueryResult>();

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return Request.BadRequest<QueryResult>("无法确定内容对应的站点");

            var tableName = site.TableName;
            var query = await GetQueryAsync(request.SiteId, request.ChannelId, request);
            var totalCount = await DataProvider.ContentRepository.GetCountAsync(tableName, query);
            var summaries = await DataProvider.ContentRepository.GetSummariesAsync(tableName, query.ForPage(request.Page, request.PerPage));

            var contents = new List<Content>();
            foreach (var summary in summaries)
            {
                var content = await DataProvider.ContentRepository.GetAsync(site, summary.ChannelId, summary.Id);
                contents.Add(content);
            }

            return new QueryResult
            {
                Contents = contents,
                TotalCount = totalCount
            };
        }

        [OpenApiOperation("审核内容API", "")]
        [HttpPost, Route(RouteCheck)]
        public async Task<CheckResult> CheckContents([FromBody] CheckRequest request)
        {
            var auth = await AuthenticatedRequest.GetAuthAsync();
            if (!auth.IsApiAuthenticated ||
                !await DataProvider.AccessTokenRepository.IsScopeAsync(auth.ApiToken, Constants.ScopeContents))
            {
                return Request.Unauthorized<CheckResult>();
            }

            var site = await DataProvider.SiteRepository.GetAsync(request.SiteId);
            if (site == null) return Request.BadRequest<CheckResult>("无法确定内容对应的站点");

            var contents = new List<Content>();
            foreach (var channelContentId in request.Contents)
            {
                var channel = await DataProvider.ChannelRepository.GetAsync(channelContentId.ChannelId);
                var content = await DataProvider.ContentRepository.GetAsync(site, channel, channelContentId.Id);
                if (content == null) continue;

                content.CheckAdminId = auth.AdminId;
                content.CheckDate = DateTime.Now;
                content.CheckReasons = request.Reasons;

                content.Checked = true;
                content.CheckedLevel = 0;

                await DataProvider.ContentRepository.UpdateAsync(site, channel, content);

                contents.Add(content);

                await DataProvider.ContentCheckRepository.InsertAsync(new ContentCheck
                {
                    SiteId = request.SiteId,
                    ChannelId = content.ChannelId,
                    ContentId = content.Id,
                    AdminId = auth.AdminId,
                    Checked = true,
                    CheckedLevel = 0,
                    CheckDate = DateTime.Now,
                    Reasons = request.Reasons
                });
            }

            await auth.AddSiteLogAsync(request.SiteId, "批量审核内容");

            foreach (var content in request.Contents)
            {
                await _createManager.CreateContentAsync(request.SiteId, content.ChannelId, content.Id);
            }

            foreach (var distinctChannelId in request.Contents.Select(x => x.ChannelId).Distinct())
            {
                await _createManager.TriggerContentChangedEventAsync(request.SiteId, distinctChannelId);
            }

            await _createManager.CreateChannelAsync(request.SiteId, request.SiteId);

            return new CheckResult
            {
                Contents = contents
            };
        }

        //[OpenApiOperation("获取站点内容API", "")]
        //[HttpGet, Route(RouteSite)]
        //public async Task<IHttpActionResult> GetSiteContents(int siteId)
        //{
        //    try
        //    {
        //        var request = await AuthenticatedRequest.GetAuthAsync();
        //        var sourceId = request.GetPostInt(ContentAttribute.SourceId.ToCamelCase());
        //        bool isAuth;
        //        if (sourceId == SourceManager.User)
        //        {
        //            isAuth = request.IsUserLoggin && await request.UserPermissions.HasChannelPermissionsAsync(siteId, siteId, Constants.ChannelPermissions.ContentView);
        //        }
        //        else
        //        {
        //            isAuth = request.IsApiAuthenticated && await
        //                         DataProvider.AccessTokenRepository.IsScopeAsync(request.ApiToken, Constants.ScopeContents) ||
        //                     request.IsUserLoggin &&
        //                     await request.UserPermissions.HasChannelPermissionsAsync(siteId, siteId,
        //                         Constants.ChannelPermissions.ContentView) ||
        //                     request.IsAdminLoggin &&
        //                     await request.AdminPermissions.HasChannelPermissionsAsync(siteId, siteId,
        //                         Constants.ChannelPermissions.ContentView);
        //        }
        //        if (!isAuth) return Unauthorized();

        //        var site = await DataProvider.SiteRepository.GetAsync(siteId);
        //        if (site == null) return BadRequest("无法确定内容对应的站点");

        //        if (!await request.AdminPermissionsImpl.HasChannelPermissionsAsync(siteId, siteId,
        //            Constants.ChannelPermissions.ContentView)) return Unauthorized();

        //        var tableName = site.TableName;

        //        var parameters = new ApiContentsParameters(request);

        //        var (channelContentIds, count) = await DataProvider.ContentRepository.GetChannelContentIdListBySiteIdAsync(tableName, siteId, parameters);
        //        var value = new List<IDictionary<string, object>>();
        //        foreach (var channelContentId in channelContentIds)
        //        {
        //            var contentInfo = await DataProvider.ContentRepository.GetAsync(site, channelContentId.ChannelId, channelContentId.Id);
        //            if (contentInfo != null)
        //            {
        //                value.Add(contentInfo.ToDictionary());
        //            }
        //        }

        //        return Ok(new PageResponse(value, parameters.Top, parameters.Skip, request.HttpRequest.Url.AbsoluteUri) {Count = count});
        //    }
        //    catch (Exception ex)
        //    {
        //        await LogUtils.AddErrorLogAsync(ex);
        //        return InternalServerError(ex);
        //    }
        //}

        //[OpenApiOperation("获取栏目内容API", "")]
        //[HttpGet, Route(RouteChannel)]
        //public async Task<IHttpActionResult> GetChannelContents(int siteId, int channelId)
        //{
        //    try
        //    {
        //        var request = await AuthenticatedRequest.GetAuthAsync();
        //        var sourceId = request.GetPostInt(ContentAttribute.SourceId.ToCamelCase());
        //        bool isAuth;
        //        if (sourceId == SourceManager.User)
        //        {
        //            isAuth = request.IsUserLoggin && await request.UserPermissions.HasChannelPermissionsAsync(siteId, channelId, Constants.ChannelPermissions.ContentView);
        //        }
        //        else
        //        {
        //            isAuth = request.IsApiAuthenticated && await
        //                         DataProvider.AccessTokenRepository.IsScopeAsync(request.ApiToken, Constants.ScopeContents) ||
        //                     request.IsUserLoggin &&
        //                     await request.UserPermissions.HasChannelPermissionsAsync(siteId, channelId,
        //                         Constants.ChannelPermissions.ContentView) ||
        //                     request.IsAdminLoggin &&
        //                     await request.AdminPermissions.HasChannelPermissionsAsync(siteId, channelId,
        //                         Constants.ChannelPermissions.ContentView);
        //        }
        //        if (!isAuth) return Unauthorized();

        //        var site = await DataProvider.SiteRepository.GetAsync(siteId);
        //        if (site == null) return BadRequest("无法确定内容对应的站点");

        //        var channelInfo = await DataProvider.ChannelRepository.GetAsync(siteId, channelId);
        //        if (channelInfo == null) return BadRequest("无法确定内容对应的栏目");

        //        if (!await request.AdminPermissionsImpl.HasChannelPermissionsAsync(siteId, channelId,
        //            Constants.ChannelPermissions.ContentView)) return Unauthorized();

        //        var tableName = await DataProvider.ChannelRepository.GetTableNameAsync(site, channelInfo);

        //        var top = request.GetQueryInt("top", 20);
        //        var skip = request.GetQueryInt("skip");
        //        var like = request.GetQueryString("like");
        //        var orderBy = request.GetQueryString("orderBy");

        //        var (list, count) = await DataProvider.ContentRepository.ApiGetContentIdListByChannelIdAsync(tableName, siteId, channelId, top, skip, like, orderBy, request.QueryString);
        //        var value = new List<IDictionary<string, object>>();
        //        foreach(var (contentChannelId, contentId) in list)
        //        {
        //            var contentInfo = await DataProvider.ContentRepository.GetAsync(site, contentChannelId, contentId);
        //            if (contentInfo != null)
        //            {
        //                value.Add(contentInfo.ToDictionary());
        //            }
        //        }

        //        return Ok(new PageResponse(value, top, skip, request.HttpRequest.Url.AbsoluteUri) { Count = count });
        //    }
        //    catch (Exception ex)
        //    {
        //        await LogUtils.AddErrorLogAsync(ex);
        //        return InternalServerError(ex);
        //    }
        //}

        //[OpenApiOperation("获取内容API", "")]
        //[HttpPost, Route(RouteSite)]
        //public async Task<QueryResult> GetSiteContents([FromUri]int siteId, [FromBody] QueryRequest request)
        //{
        //    var req = await AuthenticatedRequest.GetAuthAsync();
        //    var sourceId = req.GetPostInt(ContentAttribute.SourceId.ToCamelCase());
        //    bool isAuth;
        //    if (sourceId == SourceManager.User)
        //    {
        //        isAuth = req.IsUserLoggin && await req.UserPermissions.HasChannelPermissionsAsync(siteId, siteId, Constants.ChannelPermissions.ContentView);
        //    }
        //    else
        //    {
        //        isAuth = req.IsApiAuthenticated && await
        //                     DataProvider.AccessTokenRepository.IsScopeAsync(req.ApiToken, Constants.ScopeContents) ||
        //                 req.IsUserLoggin &&
        //                 await req.UserPermissions.HasChannelPermissionsAsync(siteId, siteId,
        //                     Constants.ChannelPermissions.ContentView) ||
        //                 req.IsAdminLoggin &&
        //                 await req.AdminPermissions.HasChannelPermissionsAsync(siteId, siteId,
        //                     Constants.ChannelPermissions.ContentView);
        //    }
        //    if (!isAuth) return Request.Unauthorized<QueryResult>();

        //    var site = await DataProvider.SiteRepository.GetAsync(siteId);
        //    if (site == null) return Request.BadRequest<QueryResult>("无法确定内容对应的站点");

        //    if (!await req.AdminPermissionsImpl.HasChannelPermissionsAsync(siteId, siteId,
        //        Constants.ChannelPermissions.ContentView)) return Request.Unauthorized<QueryResult>();

        //    var tableName = site.TableName;
        //    var query = GetQuery(siteId, null, request);
        //    var totalCount = await DataProvider.ContentRepository.GetTotalCountAsync(tableName, query);
        //    var channelContentIds = await DataProvider.ContentRepository.GetChannelContentIdListAsync(tableName, query);

        //    var contents = new List<Content>();
        //    foreach (var channelContentId in channelContentIds)
        //    {
        //        var content = await DataProvider.ContentRepository.GetAsync(site, channelContentId.ChannelId, channelContentId.Id);
        //        contents.Add(content);
        //    }

        //    return new QueryResult
        //    {
        //        Contents = contents,
        //        TotalCount = totalCount
        //    };
        //}
    }
}
