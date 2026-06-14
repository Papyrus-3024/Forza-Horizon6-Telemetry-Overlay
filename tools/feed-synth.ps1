param(
    [double]$SpeedMph = 120,
    [int]$Steer = 0,
    [int]$Seconds = 20
)

# Build a 324-byte FH6 Data Out packet with controllable fields, send to 127.0.0.1:20440.
$pkt = New-Object byte[] 324

function PutF([int]$off, [single]$v) {
    [BitConverter]::GetBytes($v).CopyTo($pkt, $off)
}
function PutI([int]$off, [int]$v) {
    [BitConverter]::GetBytes([int32]$v).CopyTo($pkt, $off)
}

PutI 0   1                       # IsRaceOn
PutI 4   12345                   # TimestampMs
PutF 8   8000                    # EngineMaxRpm
PutF 12  900                     # EngineIdleRpm
PutF 16  6000                    # CurrentEngineRpm  (0.75 fraction)
PutF 256 ([single]($SpeedMph / 2.23694))  # Speed m/s
PutF 260 280000                  # Power (W)
PutF 264 480                     # Torque
PutF 268 200                     # TireTemp FL (building->green)
PutF 272 240                     # TireTemp FR (warm/amber)
PutF 276 165                     # TireTemp RL (cool/blue)
PutF 280 285                     # TireTemp RR (hot/red)
PutF 284 14                      # Boost psi
PutF 288 0.62                    # Fuel fraction
PutF 296 75.5                    # BestLap
PutF 300 78.2                    # LastLap
PutF 304 41.0                    # CurrentLap
$pkt[314] = 1                    # RacePosition
$pkt[315] = 220                  # Accel
$pkt[316] = 0                    # Brake
$pkt[317] = 0                    # Clutch
$pkt[319] = 4                    # Gear
$pkt[320] = [byte]([sbyte]$Steer) # Steer (sbyte -127..127)

$udp = New-Object System.Net.Sockets.UdpClient
$udp.Connect("127.0.0.1", 20440)
$end = (Get-Date).AddSeconds($Seconds)
while ((Get-Date) -lt $end) {
    [void]$udp.Send($pkt, $pkt.Length)
    Start-Sleep -Milliseconds 16
}
$udp.Close()
Write-Output "feed done (speed=$SpeedMph mph, steer=$Steer)"
