namespace Mongo2Go.Helper
{
    public interface IPortPool
    {
        /// <summary>
        /// Returns and reserves a new port
        /// </summary>
        int GetNextOpenPort();
    }
}