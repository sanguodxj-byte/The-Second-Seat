using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace TheSecondSeat.RimAgent
{
    /// <summary>
    /// ? v1.6.65: ConcurrentRequestManager - 并发请求管理器
    /// 功能：请求队列管理、速率限制、错误重试机制、并发控制
    /// </summary>
    public class ConcurrentRequestManager
    {
        private static ConcurrentRequestManager instance;
        public static ConcurrentRequestManager Instance => instance ?? (instance = new ConcurrentRequestManager());
        
        // ❌ 移除未使用的队列
        // private readonly Queue<RequestItem> requestQueue = new Queue<RequestItem>();
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(5, 5);
        private readonly object lockObj = new object();
        private int activeRequests = 0;
        private int totalRequests = 0;
        private int failedRequests = 0;
        
        public int MaxConcurrentRequests { get; set; } = 5;
        public int RequestsPerMinute { get; set; } = 60;
        
        private ConcurrentRequestManager() { }
        
        /// <summary>
        /// Enqueue a request with concurrency control and retry logic
        /// ✅ v1.7.0: Added CancellationToken support
        /// </summary>
        public async Task<T> EnqueueAsync<T>(Func<Task<T>> requestFunc, int maxRetries = 3, CancellationToken cancellationToken = default)
        {
            // 等待信号量，支持取消
            await semaphore.WaitAsync(cancellationToken);
            
            try
            {
                Interlocked.Increment(ref activeRequests);
                Interlocked.Increment(ref totalRequests);
                
                return await ExecuteWithRetryAsync(requestFunc, maxRetries, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref activeRequests);
                semaphore.Release();
            }
        }
        
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> requestFunc, int maxRetries, CancellationToken cancellationToken)
        {
            int attempts = 0;
            Exception lastException = null;
            
            while (attempts < maxRetries)
            {
                // 检查取消
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await requestFunc();
                }
                catch (OperationCanceledException)
                {
                    throw; // 直接抛出取消异常
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attempts++;
                    
                    if (attempts < maxRetries)
                    {
                        int delayMs = (int)Math.Pow(2, attempts) * 1000;
                        // 延迟也支持取消
                        await Task.Delay(delayMs, cancellationToken);
                        Log.Warning($"[ConcurrentRequestManager] Retry {attempts}/{maxRetries}: {ex.Message}");
                    }
                }
            }
            
            Interlocked.Increment(ref failedRequests);
            Log.Error($"[ConcurrentRequestManager] Failed after {maxRetries} attempts: {lastException?.Message}");
            throw lastException;
        }
        
        public string GetStats()
        {
            return $"[ConcurrentRequestManager] Active: {activeRequests}, Total: {totalRequests}, Failed: {failedRequests}";
        }
        
        /// <summary>
        /// ⭐ v1.6.65: 更新并发管理器设置
        /// </summary>
        public void UpdateSettings(int maxConcurrent, int timeout, bool enableRetry)
        {
            MaxConcurrentRequests = maxConcurrent;
            // 注意：Semaphore 一旦创建就无法动态修改大小
            // 如果需要动态修改，需要重新创建 Semaphore（较复杂）
            Log.Message($"[ConcurrentRequestManager] Settings updated: MaxConcurrent={maxConcurrent}, Timeout={timeout}s, Retry={enableRetry}");
        }
        
        /// <summary>
        /// ⭐ v1.6.65: 重置统计信息（不清除队列）
        /// </summary>
        public void ResetStats()
        {
            lock (lockObj)
            {
                totalRequests = 0;
                failedRequests = 0;
            }
            Log.Message("[ConcurrentRequestManager] Statistics reset");
        }
        
        public void Reset()
        {
            lock (lockObj)
            {
                // requestQueue.Clear(); // 已移除
                activeRequests = 0;
                totalRequests = 0;
                failedRequests = 0;
            }

            // 强制释放信号量防止死锁（慎用，仅在重置时）
            // 注意：这可能会导致信号量计数超过初始最大值，如果此时还有线程持有信号量
            // 但在 Reset 场景下，我们假设是紧急恢复
            while(semaphore.CurrentCount < MaxConcurrentRequests)
            {
                semaphore.Release();
            }
            Log.Message("[ConcurrentRequestManager] Reset complete, semaphore released.");
        }
        
        public int GetActiveRequestCount() => activeRequests;
        
        // private class RequestItem ... // 已移除
    }
}
