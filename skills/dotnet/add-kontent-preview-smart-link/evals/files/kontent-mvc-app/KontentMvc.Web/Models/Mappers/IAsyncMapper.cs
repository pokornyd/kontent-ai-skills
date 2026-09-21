namespace KontentMvc.Web.Models.Mappers;

/// <summary>
/// The asynchronous mapper contract, for a mapping that has something to await (a further Delivery
/// request, for instance). A pure transformation uses <see cref="IMapper{TSource,TDestination}"/>.
/// </summary>
public interface IAsyncMapper<in TSource, TDestination>
{
    Task<TDestination> MapAsync(TSource source);
}
