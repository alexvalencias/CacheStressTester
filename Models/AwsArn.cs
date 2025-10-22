using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CacheStressTester.Models;

public class AwsArn
{
    public string Partition { get; init; } = "aws";
    public string Service { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string AccountId { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public string ResourceId { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;

    public static AwsArn? Parse(string arn)
    {
        if (string.IsNullOrWhiteSpace(arn))
        {
            Console.WriteLine("ARN cannot be null or empty");
            return null;
        }

        var parts = arn.Split(':', 6);

        if (parts.Length < 6 || !arn.StartsWith("arn:"))
        {
            Console.WriteLine($"Invalid ARN format: {arn}");
            return null;
        }

        // Resource part could be "secret/MySecret" or "secret:MySecret-AbCdEf"
        string resourcePart = parts[5];
        string[] resourceSplit = resourcePart.Contains('/')
            ? resourcePart.Split('/', 2)
            : resourcePart.Split(':', 2);

        return new AwsArn
        {
            Partition = parts[1],
            Service = parts[2],
            Region = parts[3],
            AccountId = parts[4],
            ResourceType = resourceSplit[0],
            ResourceId = resourceSplit.Length > 1 ? resourceSplit[1] : string.Empty
        };
    }

    public override string ToString() => $"arn:{Partition}:{Service}:{Region}:{AccountId}:{ResourceType}:{ResourceId}";
}

