﻿using System.Collections.Generic;
using Datory;
using Newtonsoft.Json;
using SiteServer.Abstractions;
using SiteServer.CMS.Framework;
using SiteServer.CMS.Repositories;


namespace SiteServer.Cli.Updater.Tables
{
    public partial class TableRelatedField
    {
        [JsonProperty("relatedFieldID")]
        public long RelatedFieldId { get; set; }

        [JsonProperty("relatedFieldName")]
        public string RelatedFieldName { get; set; }

        [JsonProperty("publishmentSystemID")]
        public long PublishmentSystemId { get; set; }

        [JsonProperty("totalLevel")]
        public long TotalLevel { get; set; }

        [JsonProperty("prefixes")]
        public string Prefixes { get; set; }

        [JsonProperty("suffixes")]
        public string Suffixes { get; set; }
    }

    public partial class TableRelatedField
    {
        public static readonly List<string> OldTableNames = new List<string>
        {
            "siteserver_RelatedField",
            "wcm_RelatedField"
        };

        public static ConvertInfo Converter => new ConvertInfo
        {
            NewTableName = NewTableName,
            NewColumns = NewColumns,
            ConvertKeyDict = ConvertKeyDict,
            ConvertValueDict = ConvertValueDict
        };

        private static readonly string NewTableName = DataProvider.RelatedFieldRepository.TableName;

        private static readonly List<TableColumn> NewColumns = DataProvider.RelatedFieldRepository.TableColumns;

        private static readonly Dictionary<string, string> ConvertKeyDict =
            new Dictionary<string, string>
            {
                {nameof(RelatedField.Id), nameof(RelatedFieldId)},
                {nameof(RelatedField.Title), nameof(RelatedFieldName)},
                {nameof(RelatedField.SiteId), nameof(PublishmentSystemId)}
            };

        private static readonly Dictionary<string, string> ConvertValueDict = null;
    }
}
