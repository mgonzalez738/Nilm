using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.UriParser;

namespace EnergyMetersService.Api.Extensions;

public static class ODataExtensions
{
    public static IEnumerable<string> GetExpandProperties(this SelectExpandQueryOption? selectExpandOption)
    {
        if (selectExpandOption == null || selectExpandOption.SelectExpandClause == null)
            return Enumerable.Empty<string>();

        return selectExpandOption.SelectExpandClause.SelectedItems
            .OfType<ExpandedNavigationSelectItem>()
            .Select(x => {
                var lastSegment = x.PathToNavigationProperty?.LastSegment as NavigationPropertySegment;
                return lastSegment?.NavigationProperty.Name ?? string.Empty;
            })
            .Where(name => !string.IsNullOrEmpty(name));
    }

    public static IEnumerable<string> GetSelectProperties(this SelectExpandQueryOption? selectExpandOption)
    {
        if (selectExpandOption == null || selectExpandOption.SelectExpandClause == null)
            return Enumerable.Empty<string>();

        return selectExpandOption.SelectExpandClause.SelectedItems
            .OfType<PathSelectItem>()
            .Select(x => {
                var lastSegment = x.SelectedPath?.LastSegment as PropertySegment;
                return lastSegment?.Property.Name ?? string.Empty;
            })
            .Where(name => !string.IsNullOrEmpty(name));
    }
}
