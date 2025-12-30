using AST_Resource_Monitor.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;


namespace AST_Resource_Monitor;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(4);

    private const int TargetHourGmt = 7;
    private bool _isFirstRun = true; 

    private const int DiskUsageThreshold = 95;
    private const int RAMUsageThreshold = 90;
    private const int CPUUsageThreshold = 80;

    private readonly ConcurrentDictionary<string, DateTime> _lastAlerts = new();

    private readonly PerformanceCounter _cpuCounter =
        new PerformanceCounter("Processor", "% Processor Time", "_Total", true);

    private readonly MEMORYSTATUSEX _memoryStatus;
    public Worker(ILogger<Worker> logger, IEmailSender emailSender, IConfiguration configuration)
    {
        _logger = logger;
        _emailSender = emailSender;
        _configuration = configuration;
        _cpuCounter.NextValue();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Industrial Resource Monitor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_isFirstRun)
            {
                TimeSpan delay = GetNextDelay();

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }
            else
            {
                _logger.LogInformation("Initial run: Performing first resource check now...");
            }

            try
            {
                await ProcessResourceCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resource check failed.");
            }
            finally
            {
                _isFirstRun = false;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
    private TimeSpan GetNextDelay()
    {
        DateTime nowGmt = DateTime.UtcNow;
        DateTime nextRunGmt = new DateTime(nowGmt.Year, nowGmt.Month, nowGmt.Day, TargetHourGmt, 0, 0, DateTimeKind.Utc);

        if (nowGmt >= nextRunGmt)
        {
            //nextRunGmt = nowGmt.AddMinutes(1);
            nextRunGmt = nextRunGmt.AddDays(1);
        }

        return nextRunGmt - nowGmt;
    }
    private async Task ProcessResourceCheckAsync(CancellationToken token)
    {
        var ramUsage = GetRamUsage();
        var diskUsages = GetDiskUsages();

        var cpuUsage = await Task.Run(() => CheckCpuAsync(token), token);

        bool isCpuHigh = cpuUsage >= CPUUsageThreshold;
        bool isRamHigh = ramUsage >= RAMUsageThreshold;
        bool isAnyDiskHigh = diskUsages.Any(d => d.UsedPercent >= DiskUsageThreshold);

        // 3. If anything is wrong, send the combined report
        if ((isCpuHigh || isRamHigh || isAnyDiskHigh) && CanSendAlert("GlobalStatus"))
        {
            var message = BuildCombinedAlertMessage(cpuUsage, ramUsage, diskUsages);
            Console.WriteLine(message);
            SendAlert(message);
            MarkAlertSent("GlobalStatus");
        }
    }
    private string BuildCombinedAlertMessage(float cpu, uint ram, List<DiskInfo> disks)
    {
        string hostName = Dns.GetHostName();
        string ipAddress = GetLocalIpAddress();
        string userName = Environment.UserName;

        var sb = new System.Text.StringBuilder();
        sb.Append("<div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>");
        sb.Append("<h2 style='color: #0056b3;'>AST - Industrial Resource Report</h2>");
        sb.Append($"<p><strong>Timestamp:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} GMT<br>");
        sb.Append($"<strong>Machine:</strong> {hostName} <br>");
        sb.Append($"<strong>User:</strong> {userName}<br>");
        sb.Append($"<strong>Ip:</strong> {ipAddress}</p>");

        sb.Append("<table border='1' cellpadding='8' style='border-collapse: collapse; width: 100%;'>");
        sb.Append("<tr style='background-color: #f2f2f2;'><th>Resource</th><th>Usage</th><th>Status</th></tr>");

        // CPU Row
        string cpuColor = cpu >= CPUUsageThreshold ? "red" : "green";
        sb.Append($"<tr><td>CPU</td><td>{cpu:F1}%</td><td style='color:{cpuColor}; font-weight:bold;'>{(cpu >= CPUUsageThreshold ? "HIGH" : "OK")}</td></tr>");

        // RAM Row
        string ramColor = ram >= RAMUsageThreshold ? "red" : "green";
        sb.Append($"<tr><td>RAM</td><td>{ram}%</td><td style='color:{ramColor}; font-weight:bold;'>{(ram >= RAMUsageThreshold ? "HIGH" : "OK")}</td></tr>");

        // Disk Rows
        foreach (var disk in disks)
        {
            string diskColor = disk.UsedPercent >= DiskUsageThreshold ? "red" : "green";
            sb.Append($"<tr><td>Disk {disk.Name}</td><td>{disk.UsedPercent:F1}% ({disk.FreeGB:F2} GB Free)</td>");
            sb.Append($"<td style='color:{diskColor}; font-weight:bold;'>{(disk.UsedPercent >= DiskUsageThreshold ? "LOW SPACE" : "OK")}</td></tr>");
        }

        sb.Append("</table>");
        sb.Append("<p style='font-size: 0.8em; color: #777;'>This is an automated message from AST Ambiente Resource Monitor.</p>");
        sb.Append("</div>");

        return sb.ToString();
    }
    private string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                // We look for AddressFamily.InterNetwork to get the standard IPv4 (e.g., 192.168.1.5)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "No IPv4 found";
        }
        catch
        {
            return "Unknown";
        }
    }
    private async Task waitForCounter()
    {
        await Task.Delay(500);
    }
    private float CheckCpuAsync(CancellationToken token)
    {
        waitForCounter().Wait(token);

        return _cpuCounter.NextValue();

    }
    private uint GetRamUsage()
    {
        var memStatus = new MEMORYSTATUSEX();
        return GlobalMemoryStatusEx(memStatus) ? memStatus.dwMemoryLoad : 0;
    }
    private List<DiskInfo> GetDiskUsages()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new DiskInfo
            {
                Name = d.Name,
                UsedPercent = 100.0 * (d.TotalSize - d.AvailableFreeSpace) / d.TotalSize,
                // Convert Bytes to Megabytes
                FreeGB = (double) d.AvailableFreeSpace / (1024 * 1024 * 1024)
            }).ToList();
    }

    private bool CanSendAlert(string key)
    {
        return !_lastAlerts.TryGetValue(key, out var last) ||
               DateTime.UtcNow - last > TimeSpan.FromHours(6);
    }

    private void MarkAlertSent(string key)
    {
        _lastAlerts[key] = DateTime.UtcNow;
    }

    private async void SendAlert(string htmlBody)
    {
        var subject = "AST - Resources Communication";

        string emailConfig = _configuration.GetValue<string>("EmailsToSend") ?? "";
        var recipients = emailConfig.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(e => e.Trim())
                                    .ToList();

        if (recipients.Count == 0)
        {
            _logger.LogWarning("No email recipients found in configuration.");
            return;
        }

        // 2. Send via the EmailSender service
        _logger.LogInformation("Sending resource alert to {Count} recipients.", recipients.Count);
        await _emailSender.SendEmailAsync(recipients, subject, htmlBody);
    }

    private class DiskInfo { public string Name; public double UsedPercent; public double FreeGB { get; set; } }

    #region Native memory API

    [StructLayout(LayoutKind.Sequential)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("Kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    #endregion
}
