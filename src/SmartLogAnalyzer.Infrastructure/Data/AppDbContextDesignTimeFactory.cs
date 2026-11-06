using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SmartLogAnalyzer.Infrastructure.Data;

namespace SmartLogAnalyzer.Infrastructure.Data;

/// <summary>
/// Factoría de diseño para Entity Framework Core Tools.
/// Permite ejecutar `dotnet ef migrations` sin necesidad de un host de aplicación completo.
/// La cadena de conexión se lee de variables de entorno y, de no encontrarse, se usa una cadena
/// por defecto para SQL Server local (suficiente para generar el modelo; no se conecta en realidad).
/// </summary>
public class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost,1433;Database=SmartLogAnalyzer;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.CommandTimeout(30);
            sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        });

        return new AppDbContext(optionsBuilder.Options);
    }
}
