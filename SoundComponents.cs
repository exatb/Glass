using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoundGenerators;
using AudioFilters;

namespace SoundComponents
{
    public class SoundComponent : ISoundGenerator
    {
        public ISoundGenerator? Generator;
        public IAudioFilter? Filter;
        public float StartTime;
        public float Lifetime;  // по истечении компонент удаляется

        public bool IsExpired(float currentTime)
        {
            return (currentTime >= StartTime + Lifetime);
        }

        public float GenerateSample(float currentTime)
        {
            float raw = Generator?.GenerateSample(currentTime) ?? 0f;
            if (Filter != null)
                raw = Filter.Process(raw);

            return raw;
        }
    }

}
