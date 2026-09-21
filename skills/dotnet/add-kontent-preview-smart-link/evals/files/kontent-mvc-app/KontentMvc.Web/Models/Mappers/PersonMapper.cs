using Kontent.Ai.Delivery.Abstractions;
using KontentMvc.Web.Content;

namespace KontentMvc.Web.Models.Mappers;

public sealed class PersonMapper : IMapper<IContentItem<Person>, PersonViewModel>
{
    public PersonViewModel Map(IContentItem<Person> source)
    {
        var elements = source.Elements;
        var name = string.Join(' ', new[] { elements.FirstName, elements.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        return new PersonViewModel
        {
            ItemId = source.System.Id,
            Name = string.IsNullOrWhiteSpace(name) ? source.System.Name : name,
            JobTitle = elements.JobTitle,
            Photo = elements.Image?.FirstOrDefault(),
        };
    }
}
