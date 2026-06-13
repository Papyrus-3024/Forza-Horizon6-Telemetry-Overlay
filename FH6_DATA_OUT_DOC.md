Forza Horizon 6 carries forward the technology from previous Forza titles that powers motion sleds, companion apps, and more. We call it "Data Out," and players can configure it in-game to fit their needs. This page covers details of how to use and configure the feature.

# Overview

After being configured in-game, telemetry output sends data packets for use by external apps. This one-way UDP traffic is sent to a remote IP address at a rate equal to the game's frame rate. This functionality is also available to the localhost address (127.0.0.1).

A single packet format is sent to the configured address, containing vehicle dynamics, tire data, race status, and player inputs.

# Configuration

The following settings can be configured in-game and are found under SETTINGS > HUD AND GAMEPLAY:

**Data Out:** Toggles the data output function on and off. When set to On, data will begin to send as soon as the player starts driving.

**Data Out IP Address:** The target IP address of the remote machine receiving data. The localhost address (127.0.0.1) is supported.

**Data Out IP Port:** The target IP port of the remote machine receiving data. Be sure your app is listening on the same port and that firewall rules allow data on these ports to be received by your app. Avoid ports 5200 through 5300, as the game binds its own outgoing socket to a port in this range.

# Output Structure

## Type Notes

[Letter][Number]

The letter defines the type from one of the following:

**S** -- Signed Integer

**U** -- Unsigned Integer

**F** -- Floating Point

The number defines the amount of bits used.

Examples:

-   S8 is a signed byte with potential values between -128 and 127.
-   F32 is a 32-bit floating point number, equivalent to float/single.
-   U32 is a 32-bit unsigned integer.

## Packet Format

Total packet size: 324 bytes.

// = 1 when race is on. = 0 when in menus/race stopped.

S32 IsRaceOn;

// Can overflow to 0 eventually

U32 TimestampMS;

// Engine RPM values

F32 EngineMaxRpm;

F32 EngineIdleRpm;

F32 CurrentEngineRpm;

// In the car's local space; X = right, Y = up, Z = forward

F32 AccelerationX;

F32 AccelerationY;

F32 AccelerationZ;

// In the car's local space; X = right, Y = up, Z = forward

F32 VelocityX;

F32 VelocityY;

F32 VelocityZ;

// Angular velocity in the car's local space (rad/s); X = pitch, Y = yaw, Z = roll

F32 AngularVelocityX;

F32 AngularVelocityY;

F32 AngularVelocityZ;

// Car orientation (radians)

F32 Yaw;

F32 Pitch;

F32 Roll;

// Suspension travel normalized: 0.0f = max stretch; 1.0 = max compression

F32 NormalizedSuspensionTravelFrontLeft;

F32 NormalizedSuspensionTravelFrontRight;

F32 NormalizedSuspensionTravelRearLeft;

F32 NormalizedSuspensionTravelRearRight;

// Tire normalized slip ratio, = 0 means 100% grip and |ratio| > 1.0 means loss of grip.

F32 TireSlipRatioFrontLeft;

F32 TireSlipRatioFrontRight;

F32 TireSlipRatioRearLeft;

F32 TireSlipRatioRearRight;

// Wheel rotation speed radians/sec.

F32 WheelRotationSpeedFrontLeft;

F32 WheelRotationSpeedFrontRight;

F32 WheelRotationSpeedRearLeft;

F32 WheelRotationSpeedRearRight;

// = 1 when wheel is on rumble strip, = 0 when off.

S32 WheelOnRumbleStripFrontLeft;

S32 WheelOnRumbleStripFrontRight;

S32 WheelOnRumbleStripRearLeft;

S32 WheelOnRumbleStripRearRight;

// = 1 when wheel is in a puddle, = 0 when not.

S32 WheelInPuddleFrontLeft;

S32 WheelInPuddleFrontRight;

S32 WheelInPuddleRearLeft;

S32 WheelInPuddleRearRight;

// Non-dimensional surface rumble values passed to controller force feedback

F32 SurfaceRumbleFrontLeft;

F32 SurfaceRumbleFrontRight;

F32 SurfaceRumbleRearLeft;

F32 SurfaceRumbleRearRight;

// Tire normalized slip angle, = 0 means 100% grip and |angle| > 1.0 means loss of grip.

F32 TireSlipAngleFrontLeft;

F32 TireSlipAngleFrontRight;

F32 TireSlipAngleRearLeft;

F32 TireSlipAngleRearRight;

// Tire normalized combined slip, = 0 means 100% grip and |slip| > 1.0 means loss of grip.

F32 TireCombinedSlipFrontLeft;

F32 TireCombinedSlipFrontRight;

F32 TireCombinedSlipRearLeft;

F32 TireCombinedSlipRearRight;

// Actual suspension travel in meters

F32 SuspensionTravelMetersFrontLeft;

F32 SuspensionTravelMetersFrontRight;

F32 SuspensionTravelMetersRearLeft;

F32 SuspensionTravelMetersRearRight;

// Unique ID of the car make/model

S32 CarOrdinal;

// Between 0 (D -- worst cars) and 7 (X class -- best cars) inclusive

S32 CarClass;

// Between 100 (worst car) and 999 (best car) inclusive

S32 CarPerformanceIndex;

// 0 = FWD, 1 = RWD, 2 = AWD

S32 DrivetrainType;

// Number of cylinders in the engine

S32 NumCylinders;

// Car group identifier

U32 CarGroup;

// Velocity loss from smashable object collision (m/s)

F32 SmashableVelDiff;

// Mass of recently hit smashable object (kg)

F32 SmashableMass;

// Position in world space (meters)

F32 PositionX;

F32 PositionY;

F32 PositionZ;

// Speed in meters per second

F32 Speed;

// Power in watts

F32 Power;

// Torque in newton-meters

F32 Torque;

// Tire temperature

F32 TireTempFrontLeft;

F32 TireTempFrontRight;

F32 TireTempRearLeft;

F32 TireTempRearRight;

// Turbo/supercharger boost (PSI above atmospheric)

F32 Boost;

// Fuel level (0.0 = empty, 1.0 = full)

F32 Fuel;

// Total distance traveled (meters)

F32 DistanceTraveled;

// Lap times (seconds); 0.0 if not applicable

F32 BestLap;

F32 LastLap;

F32 CurrentLap;

// Total race time (seconds since driving started)

F32 CurrentRaceTime;

// Number of laps completed

U16 LapNumber;

// Current race position

U8 RacePosition;

// Player inputs (0 to 255)

U8 Accel;

U8 Brake;

U8 Clutch;

U8 HandBrake;

// Current gear

U8 Gear;

// Steering input (-127 = full left, 0 = center, 127 = full right)

S8 Steer;

// Normalized driving line position (-127 to 127)

S8 NormalizedDrivingLine;

// Normalized AI braking difference (-127 to 127)

S8 NormalizedAIBrakeDifference;

# Notes

-   Data is only sent while the player is actively driving. It is not sent during menus, pauses, replays, rewinds, or after finishing a race.
-   Data is transmitted out only (one-way UDP). No data is received by the game.
-   The packet format is fixed. Unlike Forza Motorsport, there is no option to select between different formats.
-   Developers of companion apps should note that Forza Horizon 6 includes three fields not present in Forza Motorsport: CarGroup, SmashableVelDiff, and SmashableMass. These are inserted after NumCylinders and before PositionX.
-   Forza Horizon 6 does not include TireWear or TrackOrdinal fields that are present in Forza Motorsport's "Dash" format.
