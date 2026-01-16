namespace WpfDBApp.Data.Repositories;

public interface IRepositoryFactory : IAsyncDisposable
{
    IPersonRepository Persons { get; }
}