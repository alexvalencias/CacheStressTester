using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using CacheStressTester.Models;
using System.Text.Json;

namespace CacheStressTester.Helpers;

public static class AwsSecretHelper
{
    public static async Task<string> GetRedisConnectionStringAsync(string secretArn)
    {
        AwsSecretArn? redisSecretArn = AwsSecretArn.Parse(secretArn);

        if (redisSecretArn == null)
        {
            return string.Empty;
        }

        using var client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(redisSecretArn.Region));

        var request = new GetSecretValueRequest
        {
            SecretId = redisSecretArn.SecretName
        };

        try
        {
            var response = await client.GetSecretValueAsync(request);

            if (response == null)
            {
                throw new Exception("Secret retrieval returned null response");
            }

            // Some secrets are stored as plain text, others as JSON.
            if (!string.IsNullOrEmpty(response.SecretString))
            {
                string secretValue = response.SecretString;

                // Detect if it’s JSON and extract RedisConnectionString if applicable
                try
                {
                    var json = JsonDocument.Parse(secretValue);
                    if (json.RootElement.TryGetProperty("RedisConnectionString", out var conn))
                    {
                        return conn.GetString() ?? string.Empty;
                    }
                }
                catch (JsonException)
                {
                    // If not JSON, return as-is (plain string secret)
                    return secretValue;
                }
            }

            throw new Exception("Secret value is empty or unsupported format");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to read secret: {ex.Message}");
            return string.Empty;
        }
    }

    public static async Task GetSecret()
    {
        string secretName = "a208790-eais-redis-connect-string";
        string region = "us-east-1";

        IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));

        GetSecretValueRequest request = new GetSecretValueRequest
        {
            SecretId = secretName,
            VersionStage = "AWSCURRENT", // VersionStage defaults to AWSCURRENT if unspecified.
        };

        GetSecretValueResponse response;

        try
        {
            response = await client.GetSecretValueAsync(request);
        }
        catch (Exception e)
        {
            // For a list of the exceptions thrown, see
            // https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html
            throw e;
        }

        string secret = response.SecretString;

        // Your code goes here
    }
}