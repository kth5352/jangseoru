namespace Jangseoru.Core
{
    /// <summary>Logging boundary so Core has no dependency on BepInEx.</summary>
    public interface ILog
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
