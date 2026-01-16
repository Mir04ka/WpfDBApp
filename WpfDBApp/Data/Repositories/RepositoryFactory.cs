namespace WpfDBApp.Data.Repositories;

public class RepositoryFactory : IRepositoryFactory
{
    private readonly AppDbContext _ctx;
    
    public IPersonRepository Persons { get;  }

    public RepositoryFactory(string connectionString)
    {
        _ctx = new AppDbContext(connectionString);
        Persons = new PersonRepository(_ctx);
    }
    
    public ValueTask DisposeAsync() => _ctx.DisposeAsync();
}