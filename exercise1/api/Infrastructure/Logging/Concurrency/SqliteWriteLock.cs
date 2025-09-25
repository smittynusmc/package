namespace StargateAPI.Infrastructure.Concurrency
{
    public static class SqliteWriteLock
    {
        public static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
    }
}
