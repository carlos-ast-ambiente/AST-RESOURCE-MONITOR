# AST-RESOURCE-MONITOR

Develop and deploy a background Windows Service (AST_Resource_Monitor) designed to monitor critical hardware metrics (CPU, RAM, Disk) on plants. The goal is to shift from reactive support (waiting for client complaints) to proactive infrastructure management.

### Business Value
- Minimize Downtime: Identify memory leaks or disk saturation before they crash plant SCADA/HMI software.
- Reduced Support Costs: Lower the number of emergency call-outs by resolving resource bottlenecks during scheduled maintenance.
- Client Confidence: Demonstrate a high level of "Industrial IoT" maturity by managing infrastructure health autonomously.

### Technical Scope

- Environment: Windows Service (BackgroundWorker) targeting .NET 8/9.

- Metrics Collected:

  - RAM: Real-time physical memory load (via Native Win32 API).

  - CPU: Processor utilization percentage.

  - Disk: Free space monitoring for all logical drives.

### Alerting Logic

- Thresholds: RAM > 90%, CPU > 90% (sustained), Disk > 95%.

- Anti-Spam: Cooldown period (once per day at 7 AM GMT time) between alerts for the same resource.

- Notification: SMTP integration via MailKit for automated email alerts to the DevOps team.

### Requirements for application to run:

- At least 90MB of free space
- Install the program in C:/ProgramFiles(x86) directory
- After install ensure the service is up and running
