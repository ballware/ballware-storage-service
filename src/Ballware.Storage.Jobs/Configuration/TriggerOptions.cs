namespace Ballware.Storage.Jobs.Configuration;

public class TriggerOptions
{
    public string TemporaryCleanupCron { get; set; } = "0 0/10 * ? * * *";
}