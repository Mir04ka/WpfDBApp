using WpfDBApp.Models;

namespace WpfDBApp.Data.Repositories;

public interface IPersonRepository
{
    IQueryable<Person> Query();
    Task<List<Person>> GetAsync(Func<IQueryable<Person>, IQueryable<Person>> query);
    Task<long> CountAsync(Func<IQueryable<Person>, IQueryable<Person>> query);
    Task ClearAsync();
}