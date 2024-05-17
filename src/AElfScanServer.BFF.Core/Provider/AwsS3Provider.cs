using System;
using System.IO;
using System.Threading.Tasks;
using AElfScanServer.BFF.Core.Options;
using Amazon;
using Amazon.CognitoIdentity;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElfScanServer.BFF.Core.Provider;

public interface IAwsS3Provider
{
    public Task<string> GetFileFromS3Async(string fileKey);
}

public class AwsS3Provider : IAwsS3Provider, ISingletonDependency
{
    private readonly ILogger<AwsS3Provider> _logger;
    private readonly AwsS3Option _awsS3Option;

    private AmazonS3Client _amazonS3Client;

    public AwsS3Provider(ILogger<AwsS3Provider> logger, IOptions<AwsS3Option> awsS3Option)
    {
        _logger = logger;
        _awsS3Option = awsS3Option.Value;
        InitAwsS3Provider();
    }

    private void InitAwsS3Provider()
    {
        var identityPoolId = _awsS3Option.IdentityPoolId;
        var cognitoCredentials = new CognitoAWSCredentials(identityPoolId, RegionEndpoint.APNortheast1);
        _amazonS3Client = new AmazonS3Client(cognitoCredentials, RegionEndpoint.APNortheast1);
    }


    public async Task<string> GetFileFromS3Async(string fileKey)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _awsS3Option.BucketName,
                Key = fileKey
            };
            using (var response = await _amazonS3Client.GetObjectAsync(request))
            using (var reader = new StreamReader(response.ResponseStream))
            {
                return await reader.ReadToEndAsync();
            }
        }
        catch (AmazonS3Exception e)
        {
            _logger.LogError("Error encountered on server. Message: {message} when reading object.", e.Message);
        }
        catch (Exception e)
        {
            _logger.LogError("Unknown encountered on server. Message: {message} when reading object.", e.Message);
        }
        return null;
    }
}