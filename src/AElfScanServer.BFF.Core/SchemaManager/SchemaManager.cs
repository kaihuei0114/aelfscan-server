using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.BFF.Core.Adaptor;
using AElfScanServer.BFF.Core.Options;
using AElfScanServer.BFF.Core.Provider;
using HotChocolate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.Common.BFF.Core.SchemaManager;

public interface ISchemaManager
{
    public Task<ISchema> GetSchemaAsync(string route);
}

public class SchemaManager : ISchemaManager, ISingletonDependency
{
    private const string SchemasDirectory = "Schemas/";
    private readonly IOptionsMonitor<SchemaOption> _schemaOption;
    private readonly IAwsS3Provider _awsS3Provider;
    private readonly ILogger<SchemaManager> _logger;
    private readonly IServiceProvider _serviceProvider;

    public SchemaManager(IOptionsMonitor<SchemaOption> schemaOption, ILogger<SchemaManager> logger, 
        IServiceProvider serviceProvider, IAwsS3Provider awsS3Provider)
    {
        _schemaOption = schemaOption;
        _awsS3Provider = awsS3Provider;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _awsS3Provider = awsS3Provider;
    }

    public async Task<ISchema> GetSchemaAsync(string route)
    {
        var schemas = _schemaOption.CurrentValue.Items;
        var schemaItem = schemas.FirstOrDefault(schema => schema.Route == route);

        if (schemaItem == null)
        {
            throw new BusinessException("not found schema.");
        }

        try
        { 
            var schemaContent = await GetSchemaContentAsync(schemaItem.FileKey);
            if (string.IsNullOrEmpty(schemaContent))
            {
                throw new InvalidOperationException("Schema content is empty or null.");
            }
            //replace Url
            foreach (var (urlKey, urlValue) in schemaItem.UrlDict)
            {
                schemaContent = schemaContent.Replace(urlKey, urlValue);
            }
            // Build schema using SchemaBuilder
            var schema = SchemaBuilder.New()
                .AddDirectiveType<HttpDirectiveType>()
                .AddDirectiveType<FromJsonDirectiveType>()
                .AddDocumentFromString(schemaContent)
                .AddServices(_serviceProvider)
                .Create();

            return schema;
        }
        catch (BusinessException be)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "load schema fail. route: {route}", route);
            throw new Exception("load schema fail.");
        }
    }

    private async Task<string> GetSchemaContentAsync(string fileKey)
    {
        if (_schemaOption.CurrentValue.OpenReadFromS3)
        {
            return  await _awsS3Provider.GetFileFromS3Async(fileKey);
        }
        var basePath = AppDomain.CurrentDomain.BaseDirectory + SchemasDirectory;
        var schemaContent = await File.ReadAllTextAsync(basePath + fileKey);
        return schemaContent;
        
    }
}