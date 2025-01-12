using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Contents.Security;
using OrchardCore.Navigation;

namespace OrchardCore.Contents
{
    public class AdminMenu : INavigationProvider
    {
        private readonly IContentDefinitionManager _contentDefinitionManager;
        private readonly IContentManager _contentManager;

        public AdminMenu(
            IStringLocalizer<AdminMenu> localizer,
            IContentDefinitionManager contentDefinitionManager,
            IContentManager contentManager)
        {
            _contentDefinitionManager = contentDefinitionManager;
            _contentManager = contentManager;
            T = localizer;
        }

        public IStringLocalizer T { get; }

        public async Task BuildNavigationAsync(string name, NavigationBuilder builder)
        {
            if (!String.Equals(name, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var contentTypeDefinitions = _contentDefinitionManager.ListTypeDefinitions().OrderBy(d => d.Name);

            builder.Add(T["Content"], NavigationConstants.AdminMenuContentPosition, content => content
                .AddClass("content").Id("content")
                .Add(T["Content Items"], "1", contentItems => contentItems
                    .Permission(Permissions.EditOwnContent)
                    .Action("List", "Admin", new { area = "OrchardCore.Contents" })
                    .LocalNav())
                );

            var contentTypes = contentTypeDefinitions.Where(ctd => ctd.GetSettings<ContentTypeSettings>().Creatable).OrderBy(ctd => ctd.DisplayName);
            if (contentTypes.Any())
            {
                await builder.AddAsync(T["New"], "-1", async newMenu =>
                {
                    newMenu.LinkToFirstChild(false).AddClass("new").Id("new");
                    foreach (var contentTypeDefinition in contentTypes)
                    {
                        var ci = await _contentManager.NewAsync(contentTypeDefinition.Name);
                        var cim = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(ci);
                        var createRouteValues = cim.CreateRouteValues;
                        if (createRouteValues.Any())
                            newMenu.Add(new LocalizedString(contentTypeDefinition.DisplayName, contentTypeDefinition.DisplayName), "5", item => item
                                .Action(cim.CreateRouteValues["Action"] as string, cim.CreateRouteValues["Controller"] as string, cim.CreateRouteValues)
                                .Permission(ContentTypePermissions.CreateDynamicPermission(ContentTypePermissions.PermissionTemplates[Permissions.PublishOwnContent.Name], contentTypeDefinition))
                                );
                    }
                });
            }
        }
    }
}