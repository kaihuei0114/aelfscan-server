using System.Threading.Tasks;

namespace AElfScanServer.Data;

public interface IAElfScanServerDbSchemaMigrator
{
    Task MigrateAsync();
}
