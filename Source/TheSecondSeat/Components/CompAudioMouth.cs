using UnityEngine;
using Verse;
using RimWorld;

namespace TheSecondSeat.Components
{
    /// <summary>
    /// CompProperties for CompAudioMouth
    /// 必须用这个类来在 XML 中注册组件
    /// </summary>
    public class CompProperties_AudioMouth : CompProperties
    {
        public CompProperties_AudioMouth()
        {
            this.compClass = typeof(CompAudioMouth);
        }
    }

    /// <summary>
    /// 挂在 Pawn 身上，用于驱动口型动画
    /// 通过实时分析 Unity 的音频输出（RMS）来计算嘴巴开合度
    /// </summary>
    public class CompAudioMouth : ThingComp
    {
        // 当前的嘴巴张开度 (0.0 = 闭嘴, 1.0 = 张大嘴)
        public float currentMouthOpen = 0f;

        // 采样数组 (256个采样点)
        private float[] samples = new float[256];
        
        // 灵敏度，根据实际情况调整 (值越大嘴巴张得越大)
        private const float SENSITIVITY = 10.0f; 
        
        // 平滑度 (0.0-1.0)，值越大变化越慢，防止嘴巴抽搐
        private const float SMOOTHING = 0.2f;

        public override void CompTick()
        {
            if (Find.TickManager.Paused || !parent.Spawned) return;

            UpdateMouthOpen();
        }

        public void UpdateMouthOpen()
        {
            // 直接读取全局采样器的 RMS 值
            float rms = AudioSampler.CurrentRMS;

            // 映射到 0~1 范围
            float targetOpen = Mathf.Clamp01(rms * SENSITIVITY);

            // 平滑插值，让嘴巴动作更自然
            currentMouthOpen = Mathf.Lerp(currentMouthOpen, targetOpen, SMOOTHING);
            
            // 如果小于阈值直接归零
            if (currentMouthOpen < 0.05f) currentMouthOpen = 0f;
        }
    }

    /// <summary>
    /// 全局音频采样器，挂载在不可销毁的 GameObject 上。
    /// 确保 AudioListener.GetOutputData 在主线程的 Update 中调用。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class AudioSamplerInitializer
    {
        static AudioSamplerInitializer()
        {
            var go = new GameObject("TSS_AudioSampler");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<AudioSampler>();
        }
    }

    public class AudioSampler : MonoBehaviour
    {
        public static float CurrentRMS { get; private set; }
        private float[] samples = new float[256];

        void Update()
        {
            // 必须在主线程调用
            AudioListener.GetOutputData(samples, 0);

            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }
            CurrentRMS = Mathf.Sqrt(sum / samples.Length);
        }
    }
}
