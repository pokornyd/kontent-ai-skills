namespace KontentMvc.Web.Models.Mappers;

/// <summary>Maps a Delivery content item to a view model. One mapper per content type.</summary>
public interface IMapper<in TSource, out TDestination>
{
    TDestination Map(TSource source);
}
