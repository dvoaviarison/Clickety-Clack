﻿// Copyright (c) 2024 DVoaviarison
using ClicketyClack.Core;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

// Configure Logs
var serilog = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
    .CreateLogger();
var loggerFactory = new LoggerFactory().AddSerilog(serilog);
var logger = loggerFactory.CreateLogger<Program>();

// Bootstrap
var finder = new EWServerFinder(loggerFactory.CreateLogger<EWServerFinder>());
var client = new EWClient();
using var remote = new EWRemoteSimulator(
    finder,
    client,
    loggerFactory.CreateLogger<EWRemoteSimulator>());
var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += async (_, _) =>
{
    logger.LogInformation("\ud83d\udd34 Cancellation received. Terminating Connections Gracefully...");
    cancellationSource.Cancel();
    await remote.TerminatePairingAsync();
    Thread.Sleep(3000);
    logger.LogInformation("\ud83d\udc4b Connections Terminated Gracefully");
};
await remote.InitiatePairingAsync(cancellationSource.Token);

// Run
while (!cancellationSource.Token.IsCancellationRequested)
{
    var keyInfo = Console.ReadKey(intercept: true);
    if (KeyboardMapping.PreviousKeys.Contains(keyInfo.Key))
    {
        logger.LogInformation("\u2b05\ufe0f Previous Slide");
        await remote.PreviousSlideAsync();
    }
    else if (KeyboardMapping.NextKeys.Contains(keyInfo.Key))
    {
        logger.LogInformation("\u27a1\ufe0f Next Slide");
        await remote.NextSlideAsync();
    }
    Thread.Sleep(500);
}
