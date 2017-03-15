namespace Sitecore.Support.ContentSearch
{
    using Data.LanguageFallback;
    using Globalization;
    using Sitecore.Common;
    using Sitecore.ContentSearch;
    using Sitecore.Data;
    using Sitecore.Data.Items;
    using Sitecore.Data.Managers;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class SitecoreItemCrawler : Sitecore.ContentSearch.SitecoreItemCrawler
    {
        protected virtual IEnumerable<Item> GetItem(Item item) =>
            (from item1 in LanguageFallbackManager.GetDependentLanguages(item.Language, item.Database, item.ID).SelectMany<Language, Item>(delegate (Language language) {
                Item item1 = item.Database.GetItem(item.ID, language);
                if (item1 == null)
                {
                    return new Item[0];
                }
                if (item1.IsFallback)
                {
                    return new Item[] { item1 };
                }
                return item1.Versions.GetVersions();
            })
             where !this.IsExcludedFromIndex(item1, false)
             select item1);

        internal SitecoreIndexableItem PrepareIndexableVersion(Item item, IProviderUpdateContext context)
        {
            SitecoreIndexableItem item2 = item;
            IIndexableBuiltinFields fields = item2;
            fields.IsLatestVersion = item.Versions.IsLatestVersion();
            item2.IndexFieldStorageValueFormatter = context.Index.Configuration.IndexFieldStorageValueFormatter;
            return item2;
        }

        private void RemoveOutdatedFallbackItem(Item item, IProviderUpdateContext context)
        {
            foreach (Item item2 in this.GetItem(item).ToList<Item>())
            {
                int num = item2.Version.ToInt32();
                int num2 = num - 1;
                for (int i = num2; i > 0; i--)
                {
                    Item item4 = ItemManager.GetItem(item2.ID, item2.Language, new Sitecore.Data.Version(i), item.Database);
                    if ((item4 != null) && (i != num))
                    {
                        SitecoreIndexableItem indexable = this.PrepareIndexableVersion(item4, context);
                        base.Operations.Delete(indexable, context);
                    }
                }
                Item item3 = ItemManager.GetItem(item2.ID, item2.Language, new Sitecore.Data.Version(item.Version.ToInt32() + 1), item.Database);
                if ((item3 != null) && (item3.Version.ToInt32() != num))
                {
                    SitecoreIndexableItem item6 = this.PrepareIndexableVersion(item3, context);
                    base.Operations.Delete(item6, context);
                }
            }
        }

        private void UpdateClones(IProviderUpdateContext context, SitecoreIndexableItem versionIndexable)
        {
            IEnumerable<Item> clones;
            using (new WriteCachesDisabler())
            {
                clones = versionIndexable.Item.GetClones(false);
            }
            foreach (Item item in clones)
            {
                SitecoreIndexableItem indexable = this.PrepareIndexableVersion(item, context);
                if (!this.IsExcludedFromIndex(item, false))
                {
                    base.Operations.Update(indexable, context, context.Index.Configuration);
                }
            }
        }

        protected override void UpdateItemVersion(IProviderUpdateContext context, Item version, IndexEntryOperationContext operationContext)
        {
            SitecoreIndexableItem indexable = this.PrepareIndexableVersion(version, context);
            base.Operations.Update(indexable, context, context.Index.Configuration);
            this.UpdateClones(context, indexable);
            this.UpdateLanguageFallbackDependentItems(context, indexable, operationContext);
        }

        private void UpdateLanguageFallbackDependentItems(IProviderUpdateContext context, SitecoreIndexableItem versionIndexable, IndexEntryOperationContext operationContext)
        {
            if ((operationContext != null) && !operationContext.NeedUpdateAllLanguages)
            {
                Item item = versionIndexable.Item;
                bool? currentValue = Switcher<bool?, LanguageFallbackFieldSwitcher>.CurrentValue;
                bool flag3 = true;
                if (((currentValue.GetValueOrDefault() == flag3) ? !currentValue.HasValue : true) || (((currentValue = Switcher<bool?, LanguageFallbackItemSwitcher>.CurrentValue).GetValueOrDefault() == (flag3 = true)) ? !currentValue.HasValue : true))
                {
                    IEnumerable<Item> enumerable = this.GetItem(item);
                    foreach (Item item2 in enumerable)
                    {
                        SitecoreIndexableItem indexable = this.PrepareIndexableVersion(item2, context);
                        base.Operations.Update(indexable, context, context.Index.Configuration);
                    }
                }
                else if (item.Versions.IsLatestVersion())
                {
                    (from item1 in this.GetItem(item) select this.PrepareIndexableVersion(item1, context)).ToList<SitecoreIndexableItem>().ForEach(sitecoreIndexableItem => this.Operations.Update(sitecoreIndexableItem, context, context.Index.Configuration));
                    this.RemoveOutdatedFallbackItem(item, context);
                }
            }
        }
    }
}
