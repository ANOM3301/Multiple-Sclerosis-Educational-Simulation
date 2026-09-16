namespace Convai.Runtime.Animation
{
    /// <summary>
    ///     Source of normalized speech energy for embodiment modules.
    /// </summary>
    public interface ISpeechEnergyProvider
    {
        float Current { get; }
        void Sample(float deltaTime);
    }

    public interface IConfigurableSpeechEnergyProvider : ISpeechEnergyProvider
    {
        void Configure(float windowSeconds);
    }
}
