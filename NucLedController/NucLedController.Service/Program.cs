using NucLedController.Service;

var builder = Host.CreateApplicationBuilder(args);

// Configure service to run as Windows Service
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "NucLedControllerService";
});

// Add our custom service
builder.Services.AddHostedService<NucLedService>();

var host = builder.Build();
host.Run();
