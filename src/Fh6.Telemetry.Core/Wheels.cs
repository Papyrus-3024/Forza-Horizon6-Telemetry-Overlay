namespace Fh6.Telemetry.Core;

public readonly record struct Wheels(float FrontLeft, float FrontRight, float RearLeft, float RearRight)
{
    public bool Any(Func<float, bool> predicate) =>
        predicate(FrontLeft) || predicate(FrontRight) || predicate(RearLeft) || predicate(RearRight);

    public bool All(Func<float, bool> predicate) =>
        predicate(FrontLeft) && predicate(FrontRight) && predicate(RearLeft) && predicate(RearRight);
}

public readonly record struct WheelsInt(int FrontLeft, int FrontRight, int RearLeft, int RearRight)
{
    public bool Any(Func<int, bool> predicate) =>
        predicate(FrontLeft) || predicate(FrontRight) || predicate(RearLeft) || predicate(RearRight);
}
