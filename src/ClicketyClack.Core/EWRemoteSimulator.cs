// Copyright (c) 2024 DVoaviarison
using System.Text.Json;
using System.Text.Json.Serialization;
using ClicketyClack.Core.Models;
using Microsoft.Extensions.Logging;

namespace ClicketyClack.Core;

public class EWRemoteSimulator : IEWRemoteSimulator
{
    private readonly IEWClient _client;
    private readonly ILogger<EWRemoteSimulator> _logger;
    private const int HeartBeatsEveryMs = 3000;
    private Status Status { get; set; } = new();
    private readonly JsonSerializerOptions _deSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public EWRemoteSimulator(IEWClient client, ILogger<EWRemoteSimulator> logger)
    {
        _client = client;
        _logger = logger;
    }
    
    public async Task SetupPairingAsync(CancellationToken cancellationToken)
    {
        // Connect
        await _client.ConnectAsync();
        
        // Start HeartBeat Job
        RunHeartBeats(HeartBeatsEveryMs, cancellationToken);
        
        // Start Reception Job
        RunReceiveJob(cancellationToken);
        
        // Request for pairing
        await _client.SendAsync(Messages.PairingRequest);
    }

    public async Task NextSlideAsync()
    {
        try
        {
            _logger.LogDebug("Next >");
            await _client.SendAsync(Messages.NextSlide(Status.RequestRev));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, exception.Message);
        }
    }

    public async Task PreviousSlideAsync()
    {
        try
        {
            _logger.LogDebug("< Previous");
            await _client.SendAsync(Messages.PreviousSlide(Status.RequestRev));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, exception.Message);
        }
    }

    public async Task TerminatePairingAsync()
    {
        _logger.LogDebug("Terminating Pairing Gracefully...");
       await _client.DisconnectAsync();
    }

    private void RunHeartBeats(int sendEveryMs, CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _client.SendAsync(Messages.HeartBeat);
                }
                catch (Exception exception)
                {
                    _logger.LogError($"\ud83d\udc94 Heartbeat stopped with exception: {exception.Message}");
                }
            
                Thread.Sleep(sendEveryMs);
            }
            
            _logger.LogInformation("\ud83d\udc4b Heartbeat stopped gracefully");
        }, cancellationToken);
    }
    
    private void RunReceiveJob(CancellationToken cancellationToken)
    {
        Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var receivedRaw = await _client.ReceiveAsync();
                    _logger.LogTrace($"ReceivedRaw: {receivedRaw}");
                    
                    var received = receivedRaw.GetFirstPacketObject();
                    if (!string.IsNullOrEmpty(receivedRaw) && !string.IsNullOrEmpty(received))
                    {
                        _logger.LogDebug($"Received: {received}");
                        if (received.IsStatusMessage())
                        {
                            var previousPermissions = Status.Permissions;
                            Status = JsonSerializer.Deserialize<Status>(received, _deSerializerOptions) ?? new Status();
                            if (Status.Permissions is 0)
                            {
                                _logger.LogInformation("\ud83d\udd10 Readonly mode. Please reach our to EW admin.");
                            }

                            if (Status.Permissions is 1 && previousPermissions is 0)
                            {
                                _logger.LogInformation("\ud83d\udd13 Remote command permission granted. You can start using the app now!");
                            }
                        }

                        if (received.IsNotPairedMessage())
                        {
                            _logger.LogInformation("\ud83d\udfe1 Remote connected but NOT paired. Please reach our to EW admin.");
                        }
                        
                        if (received.IsPairedMessage())
                        {
                            var modeMessage = Status.Permissions is 1
                                ? "You can start using the app now!"
                                : "Readonly Mode. Please reach out to EW admin.";
                            _logger.LogInformation($"\ud83d\udfe2 Remote connected and paired. {modeMessage}");
                        }
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError($"\u2620\ufe0f Reception stopped with exception: {exception.Message}");
                    _logger.LogDebug(exception.StackTrace);
                    if (exception.Message.Contains("Connection reset by peer", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            _logger.LogInformation($"\ud83d\udd59 Trying to reconnect...");
                            await _client.DisconnectAsync();
                            await _client.ConnectAsync();
                            await _client.SendAsync(Messages.PairingRequest);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                }

                Thread.Sleep(500);
            }
            
            _logger.LogInformation("\ud83d\udc4b Listening stopped gracefully");

        }, cancellationToken);
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}