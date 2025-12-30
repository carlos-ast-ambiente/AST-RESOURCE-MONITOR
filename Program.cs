using AST_Resource_Monitor;
using AST_Resource_Monitor.Services;


var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AST Resource Monitor";
});

builder.Services.AddSingleton<IEmailSender, EmailSender>();

builder.Services.Configure<MailSettings>(builder.Configuration.GetSection("MailSettings"));

var host = builder.Build();

host.Run();