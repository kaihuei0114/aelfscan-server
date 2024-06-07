using System.Threading.Tasks;

namespace AElfScanServer.Common.Data;

public interface IAElfScanServerDbSchemaMigrator
{
    Task MigrateAsync();
}
