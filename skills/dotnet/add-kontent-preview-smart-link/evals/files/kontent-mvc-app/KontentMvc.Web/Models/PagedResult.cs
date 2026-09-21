namespace KontentMvc.Web.Models;

/// <summary>
/// An app-owned page of results, so no Delivery response envelope escapes the content service.
/// </summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int? TotalCount, int Skip, int Limit)
{
    public int PageNumber => Limit > 0 ? (Skip / Limit) + 1 : 1;

    public bool HasPrevious => Skip > 0;

    // TotalCount is null unless the query asked for it with WithTotalCount(); falling back to a
    // full page is safer than pretending the page size is the total.
    public bool HasNext => TotalCount is { } total ? Skip + Items.Count < total : Items.Count == Limit;

    public PagedResult<TOther> Map<TOther>(Func<T, TOther> map) =>
        new([.. Items.Select(map)], TotalCount, Skip, Limit);
}
