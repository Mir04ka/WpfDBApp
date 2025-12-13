using System.Windows;
using WpfDBApp.Data;

namespace WpfDBApp;

public partial class App : Application
{
    public static string ConnectionString = "Server=(localdb)\\v11.0;Database=WpfDBAppDb;Trusted_Connection=True;"; // In prod MUST be in .env file
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        using var ctx = new AppDbContext(ConnectionString);
        ctx.Database.EnsureCreated();
    }
}