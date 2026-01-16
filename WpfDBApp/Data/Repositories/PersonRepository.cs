using Microsoft.EntityFrameworkCore;
using WpfDBApp.Models;

namespace WpfDBApp.Data.Repositories;

public class PersonRepository : IPersonRepository
{
    private readonly AppDbContext _ctx;
    
    public PersonRepository(AppDbContext ctx)
    {
        _ctx = ctx;
    }
    
    public IQueryable<Person> Query() => _ctx.Persons.AsNoTracking();

    public async Task<List<Person>> GetAsync(Func<IQueryable<Person>, IQueryable<Person>> query)
    {
        return await query(Query()).ToListAsync();
    }

    public async Task<long> CountAsync(Func<IQueryable<Person>, IQueryable<Person>> query)
    {
        return await query(Query()).LongCountAsync();
    }

    public async Task ClearAsync()
    {
        await _ctx.Database.ExecuteSqlRawAsync("TRUNCATE TABLE Persons");
    }
}