using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CacheStressTester.Models;

public class AwsSecretArn : AwsArn
{
    // arn:aws:secretsmanager:us-east-1:288761759343:secret:a208790-eais-redis-connect-string-S97DA8
    // arn:aws:secretsmanager:us-east-1:288761759343:secret:a208790-eais-redis-connect-string-S97DA8:RedisConnectionString::

    private static readonly Regex ConsoleSuffixRegex = new(@"^[A-Za-z0-9]{6}$", RegexOptions.Compiled);

    public string JsonKey { get; private set; } = string.Empty;
    public string Stage { get; private set; } = string.Empty;

    public string SecretName
    {
        get
        {
            var parts = ResourceId.Split('-');

            if (parts.Length == 1)
            {
                return ResourceId;
            }

            var last = parts[^1];
            if (ConsoleSuffixRegex.IsMatch(last))
            {
                return string.Join('-', parts.Take(parts.Length - 1));
            }

            return ResourceId;
        }
    }

    public static new AwsSecretArn? Parse(string arn)
    {
        if (string.IsNullOrWhiteSpace(arn))
        {
            Console.WriteLine("ARN cannot be null or empty");
            return null;
        }

        var parts = arn.Split(':', StringSplitOptions.None);
        if (parts.Length < 7)
        {
            Console.WriteLine($"Invalid ARN format: {arn}");
            return null;
        }

        // base ARN
        AwsArn? baseArn = AwsArn.Parse(string.Join(':', parts.Take(7)));

        if (baseArn == null)
        {
            return null;
        }

        // parse extended segments (key, stage, etc.)
        var remainder = parts.Length > 7 ? parts.Skip(7).ToArray() : Array.Empty<string>();

        string jsonKey = string.Empty;
        string stage = string.Empty;

        if (remainder.Length >= 1 && !string.IsNullOrEmpty(remainder[0]))
        {
            jsonKey = remainder[0];
        }
        if (remainder.Length >= 2 && !string.IsNullOrEmpty(remainder[1]))
        {
            stage = remainder[1];
        }

        return new AwsSecretArn
        {
            Partition = baseArn.Partition,
            Service = baseArn.Service,
            Region = baseArn.Region,
            AccountId = baseArn.AccountId,
            ResourceType = baseArn.ResourceType,
            ResourceId = baseArn.ResourceId,
            JsonKey = jsonKey,
            Stage = stage
        };
    }

    public override string ToString()
    {
        var baseArn = base.ToString();
        if (!string.IsNullOrEmpty(JsonKey))
        {
            baseArn += $":{JsonKey}";
        }
        if (!string.IsNullOrEmpty(Stage))
        {
            baseArn += $":{Stage}";
        }
        return baseArn;
    }
}

