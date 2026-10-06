using Docker.DotNet;
using Docker.DotNet.Models;

object _fileLock = new();

const string Marker = "# docker-hosts generated";

Console.WriteLine($"{DateTime.Now} - docker-hosts Started");

CleanUpHosts();

using var config = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock"));
using var client = config.CreateClient();

var cts = new CancellationTokenSource();

Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Environment.Exit(1);
};

await SyncExistingContainersAsync(client);

try
{
    await ListenDockerEventsAsync(client, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine($"{DateTime.Now} - Exiting");
}

async Task ListenDockerEventsAsync(DockerClient client, CancellationToken token)
{
    var progress = new Progress<Message>(async msg =>
    {
        if (msg.Type != "container") return;

        string containerName = msg.Actor.Attributes.TryGetValue("name", out var name) ? name : msg.Actor.ID[..12];

        if (msg.Action == "start")
        {
            var inspect = await client.Containers.InspectContainerAsync(msg.Actor.ID, token);
            string? ip = inspect.NetworkSettings.Networks.Values.FirstOrDefault()?.IPAddress;

            if (!string.IsNullOrEmpty(ip))
                UpdateHostsEntry(containerName, ip);
        }
        else if (msg.Action == "die")
        {
            RemoveHostsEntry(containerName);
        }
    });

    await client.System.MonitorEventsAsync(new ContainerEventsParameters(), progress, token);
}

async Task SyncExistingContainersAsync(DockerClient client)
{
    Console.WriteLine($"{DateTime.Now} - Scanning running containers...");
    var containers = await client.Containers.ListContainersAsync(new ContainersListParameters { All = false });

    foreach (var c in containers)
    {
        string name = c.Names.FirstOrDefault()?.TrimStart('/') ?? c.ID[..12];
        string? ip = c.NetworkSettings.Networks.Values.FirstOrDefault()?.IPAddress;

        if (!string.IsNullOrEmpty(ip))
            UpdateHostsEntry(name, ip);
    }
}

void UpdateHostsEntry(string containerName, string ip)
{
    lock (_fileLock)
    {
        try
        {
            var lines = File.ReadAllLines("/etc/hosts").ToList();
            lines.RemoveAll(line => line.Contains($"{containerName} {Marker}"));

            string newLine = $"{ip,-15} {containerName} {Marker}";
            lines.Add(newLine);

            File.WriteAllLines("/etc/hosts", lines);
            Console.WriteLine($"{DateTime.Now} - Added entry: {ip} -> {containerName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now} - Failed to update /etc/hosts: {ex.Message}");
        }
    }
}

void RemoveHostsEntry(string containerName)
{
    lock (_fileLock)
    {
        try
        {
            var lines = File.ReadAllLines("/etc/hosts").ToList();
            int removed = lines.RemoveAll(line => line.Contains($"{containerName} {Marker}"));

            if (removed > 0)
            {
                File.WriteAllLines("/etc/hosts", lines);
                Console.WriteLine($"{DateTime.Now} - Removed entry: {containerName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now} - Failed to update /etc/hosts: {ex.Message}");
        }
    }
}

void CleanUpHosts()
{
    lock (_fileLock)
    {
        try
        {
            var lines = File.ReadAllLines("/etc/hosts").ToList();
            int countBefore = lines.Count;
            
            lines.RemoveAll(line => line.Contains(Marker));

            if (lines.Count != countBefore)
            {
                File.WriteAllLines("/etc/hosts", lines);
                Console.WriteLine($"{DateTime.Now} - Cleaned up {countBefore - lines.Count} entries from /etc/hosts");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{DateTime.Now} - Failed to clean up /etc/hosts: {ex.Message}");
        }
    }
}
