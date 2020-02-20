﻿using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using SiteServer.Abstractions;
using SiteServer.API.Context;
using SiteServer.CMS.Api.Sys.Stl;
using SiteServer.CMS.Core;
using SiteServer.CMS.Framework;
using SiteServer.CMS.Repositories;

namespace SiteServer.API.Controllers.Sys
{
    public class SysStlActionsDownloadController : ApiController
    {
        [HttpGet]
        [Route(ApiRouteActionsDownload.Route)]
        public async Task Main()
        {
            try
            {
                var request = await AuthenticatedRequest.GetAuthAsync();

                if (!string.IsNullOrEmpty(request.GetQueryString("siteId")) && !string.IsNullOrEmpty(request.GetQueryString("fileUrl")) && string.IsNullOrEmpty(request.GetQueryString("contentId")))
                {
                    var siteId = request.GetQueryInt("siteId");
                    var fileUrl = TranslateUtils.DecryptStringBySecretKey(request.GetQueryString("fileUrl"), WebConfigUtils.SecretKey);

                    if (PageUtils.IsProtocolUrl(fileUrl))
                    {
                        ContextUtils.Redirect(fileUrl);
                        return;
                    }

                    var site = await DataProvider.SiteRepository.GetAsync(siteId);
                    var filePath = await PathUtility.MapPathAsync(site, fileUrl);
                    var fileType = FileUtils.GetType(PathUtils.GetExtension(filePath));
                    if (FileUtils.IsDownload(fileType))
                    {
                        if (FileUtils.IsFileExists(filePath))
                        {
                            request.Download(HttpContext.Current.Response, filePath);
                            return;
                        }
                    }
                    else
                    {
                        ContextUtils.Redirect(await PageUtility.ParseNavigationUrlAsync(site, fileUrl, false));
                        return;
                    }
                }
                else if (!string.IsNullOrEmpty(request.GetQueryString("filePath")))
                {
                    var filePath = TranslateUtils.DecryptStringBySecretKey(request.GetQueryString("filePath"), WebConfigUtils.SecretKey);
                    var fileType = FileUtils.GetType(PathUtils.GetExtension(filePath));
                    if (FileUtils.IsDownload(fileType))
                    {
                        if (FileUtils.IsFileExists(filePath))
                        {
                            request.Download(HttpContext.Current.Response, filePath);
                            return;
                        }
                    }
                    else
                    {
                        var fileUrl = PageUtils.GetRootUrlByPhysicalPath(filePath);
                        ContextUtils.Redirect(PageUtils.ParseNavigationUrl(fileUrl));
                        return;
                    }
                }
                else if (!string.IsNullOrEmpty(request.GetQueryString("siteId")) && !string.IsNullOrEmpty(request.GetQueryString("channelId")) && !string.IsNullOrEmpty(request.GetQueryString("contentId")) && !string.IsNullOrEmpty(request.GetQueryString("fileUrl")))
                {
                    var siteId = request.GetQueryInt("siteId");
                    var channelId = request.GetQueryInt("channelId");
                    var contentId = request.GetQueryInt("contentId");
                    var fileUrl = TranslateUtils.DecryptStringBySecretKey(request.GetQueryString("fileUrl"), WebConfigUtils.SecretKey);
                    var site = await DataProvider.SiteRepository.GetAsync(siteId);
                    var channelInfo = await DataProvider.ChannelRepository.GetAsync(channelId);
                    var contentInfo = await DataProvider.ContentRepository.GetAsync(site, channelInfo, contentId);

                    await DataProvider.ContentRepository.AddDownloadsAsync(await DataProvider.ChannelRepository.GetTableNameAsync(site, channelInfo), channelId, contentId);

                    if (!string.IsNullOrEmpty(contentInfo?.Get<string>(ContentAttribute.FileUrl)))
                    {
                        if (PageUtils.IsProtocolUrl(fileUrl))
                        {
                            ContextUtils.Redirect(fileUrl);
                            return;
                        }

                        var filePath = await PathUtility.MapPathAsync(site, fileUrl, true);
                        var fileType = FileUtils.GetType(PathUtils.GetExtension(filePath));
                        if (FileUtils.IsDownload(fileType))
                        {
                            if (FileUtils.IsFileExists(filePath))
                            {
                                request.Download(HttpContext.Current.Response, filePath);
                                return;
                            }
                        }
                        else
                        {
                            ContextUtils.Redirect(await PageUtility.ParseNavigationUrlAsync(site, fileUrl, false));
                            return;
                        }
                    }
                }
            }
            catch
            {
                // ignored
            }

            HttpContext.Current.Response.Write("下载失败，不存在此文件！");
        }
    }
}
