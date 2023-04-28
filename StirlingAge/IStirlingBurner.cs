/// Anything that can serve as a heat source for a Stirling Engine.
public interface IStirlingBurner {
    float HotSideTemperature { get; }
    float ColdSideTemperature { get; }
}
