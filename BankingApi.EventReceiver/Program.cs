using Azure.Messaging.ServiceBus;
using BankingApi.EventReceiver.Infrastructure;
using BankingApi.EventReceiver.Messaging;
using BankingApi.EventReceiver.Worker;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var sql = builder.Configuration.GetConnectionString("SqlServer");
var sb = builder.Configuration.GetValue<string>("ServiceBus:ConnectionString");
var queue = builder.Configuration.GetValue<string>("ServiceBus:QueueName");

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(sql));

builder.Services.AddSingleton(new ServiceBusClient(sb));
builder.Services.AddSingleton<IEventReceiver>(sp =>
    new AzureServiceBusEventReceiver(sp.GetRequiredService<ServiceBusClient>(), queue));

builder.Services.AddHostedService<MessageWorker>();

builder.Services.AddControllers();
builder.WebHost.UseUrls("http://*:7000");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapControllers();

await app.RunAsync();