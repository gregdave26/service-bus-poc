function Wait-ServiceBusEmulatorReady {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ComposePath,

        [int]$InitialDelaySeconds = 30,

        [int]$PollingSeconds = 90,

        [scriptblock]$CancellationCheck
    )

    Write-Host "  Waiting $InitialDelaySeconds seconds for emulator startup..."
    for ($delay = 0; $delay -lt ($InitialDelaySeconds * 10); $delay++) {
        if ($CancellationCheck -and (& $CancellationCheck)) {
            throw [System.OperationCanceledException]::new('Ctrl+C requested during emulator startup.')
        }
        Start-Sleep -Milliseconds 100
    }

    for ($attempt = 1; $attempt -le $PollingSeconds; $attempt++) {
        if ($CancellationCheck -and (& $CancellationCheck)) {
            throw [System.OperationCanceledException]::new('Ctrl+C requested during emulator readiness polling.')
        }

        $socket = $null
        try {
            $socket = [System.Net.Sockets.TcpClient]::new()
            $socket.ReceiveTimeout = 2000
            $socket.SendTimeout = 2000
            $connectTask = $socket.ConnectAsync('localhost', 5672)

            if ($connectTask.Wait(2000) -and $socket.Connected) {
                $stream = $socket.GetStream()
                $stream.ReadTimeout = 2000
                $stream.WriteTimeout = 2000
                $amqpHeader = [byte[]]@(0x41, 0x4D, 0x51, 0x50, 0x00, 0x01, 0x00, 0x00)
                $stream.Write($amqpHeader, 0, $amqpHeader.Length)
                $stream.Flush()

                $response = [byte[]]::new(8)
                $bytesRead = $stream.Read($response, 0, $response.Length)
                if ($bytesRead -gt 0) {
                    Write-Host "  ✓ AMQP handshake completed on localhost:5672" -ForegroundColor Green
                    return
                }
            }
        }
        catch [System.OperationCanceledException] {
            throw
        }
        catch {
            # The emulator can refuse connections while SQL Edge and AMQP initialize.
        }
        finally {
            if ($socket) {
                $socket.Dispose()
            }
        }

        if ($attempt % 10 -eq 0) {
            Write-Host "    [$attempt/$PollingSeconds seconds of polling] AMQP handshake not ready..."
        }
        Start-Sleep -Seconds 1
    }

    Write-Host ""
    Write-Host "AMQP readiness failed. Emulator diagnostics:" -ForegroundColor Red
    docker-compose -f $ComposePath ps 2>&1 | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
    Write-Host "Container logs (emulator and sqledge):" -ForegroundColor Red
    docker-compose -f $ComposePath logs --tail 200 emulator sqledge 2>&1 |
        ForEach-Object { Write-Host "  $_" }

    throw "Service Bus emulator did not complete the AMQP handshake on localhost:5672 within the readiness timeout."
}
