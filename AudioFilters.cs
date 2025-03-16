using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioFilters
{
    // Интерфейс фильтра – для единообразия
    public interface IAudioFilter
    {
        float Process(float inputSample);
    }

    /// <summary>
    /// Реализация НЧ, ВЧ и BP фильтров, взята из
    /// https://www.w3.org/TR/audio-eq-cookbook/
    /// </summary>
    public class BiQuadFilter : IAudioFilter
    {
        public enum FilterType { LowPass, HighPass, BandPass }

        // == Параметры, сохраняемые для удобного изменения ==
        public FilterType Type { get; private set; }
        public double SampleRate { get; private set; }
        public double CenterFrequency { get; private set; }
        public double QFactor { get; private set; }

        // == Внутренние коэффициенты (a0..a2, b1..b2) ==
        private float a0, a1, a2, b1, b2;

        // == Предыдущие отсчёты ==
        private float x1, x2, y1, y2;

        public BiQuadFilter(FilterType type, double sampleRate, double centerFrequency, double qFactor)
        {
            SetParameters(type, sampleRate, centerFrequency, qFactor);
        }

        /// <summary>
        /// Универсальный метод обновления параметров
        /// </summary>
        public void SetParameters(FilterType type, double sampleRate, double centerFrequency, double qFactor)
        {
            Type = type;
            SampleRate = sampleRate;
            CenterFrequency = centerFrequency;
            QFactor = qFactor;

            UpdateCoefficients();
        }

        /// <summary>
        /// Пересчитывает коэффициенты при смене параметров
        /// </summary>
        private void UpdateCoefficients()
        {
            float omega = (float)(2.0 * Math.PI * CenterFrequency / SampleRate);
            float alpha = MathF.Sin(omega) / (2f * (float)QFactor);
            float cosw = MathF.Cos(omega);
            float norm;

            switch (Type)
            {
                case FilterType.LowPass:
                    norm = 1f / (1f + alpha);
                    a0 = (1f - cosw) * 0.5f * norm;
                    a1 = (1f - cosw) * norm;
                    a2 = a0;
                    b1 = -2f * cosw * norm;
                    b2 = (1f - alpha) * norm;
                    break;

                case FilterType.HighPass:
                    norm = 1f / (1f + alpha);
                    a0 = (1f + cosw) * 0.5f * norm;
                    a1 = -(1f + cosw) * norm;
                    a2 = a0;
                    b1 = -2f * cosw * norm;
                    b2 = (1f - alpha) * norm;
                    break;

                case FilterType.BandPass:
                    norm = 1f / (1f + alpha);
                    a0 = alpha * norm;
                    a1 = 0f;
                    a2 = -alpha * norm;
                    b1 = -2f * cosw * norm;
                    b2 = (1f - alpha) * norm;
                    break;
            }
        }

        public float Process(float inputSample)
        {
            float output = a0 * inputSample
                         + a1 * x1
                         + a2 * x2
                         - b1 * y1
                         - b2 * y2;

            // Сдвигаем пред. значения
            x2 = x1;
            x1 = inputSample;
            y2 = y1;
            y1 = output;

            return output;
        }

        public void Reset()
        {
            x1 = x2 = y1 = y2 = 0f;
        }
    }

    /// <summary>
    /// Дискретный резонатор 2-го порядка
    /// реализация взята из [Сергиенко А.Б ЦОС]
    /// и согласована с функцией iireak из MatLab
    /// </summary>
    public class IIRPeakFilter : IAudioFilter
    {
        // == Параметры ==
        public double SampleRate { get; private set; }
        public double CenterFrequency { get; private set; } // f0
        public double Bandwidth { get; private set; }       // df

        // Коэффициенты
        private double[] a = new double[3];
        private double[] b = new double[3];

        // Предыдущие отсчёты
        private float x1, x2, y1, y2;

        public IIRPeakFilter(double sampleRate, double centerFrequency, double bandwidth)
        {
            SetParameters(sampleRate, centerFrequency, bandwidth);
        }

        /// <summary>
        /// Универсальный метод обновления параметров
        /// </summary>
        public void SetParameters(double sampleRate, double centerFrequency, double bandwidth)
        {
            SampleRate = sampleRate;
            CenterFrequency = centerFrequency;
            Bandwidth = bandwidth;
            UpdateCoefficients();
        }

        private void UpdateCoefficients()
        {
            // w0 = 2 * pi * f0 / fs
            // bw = df * 2 / fs
            double w0 = 2.0 * Math.PI * CenterFrequency / SampleRate;
            double bw = Bandwidth * 2.0 / SampleRate;

            // a[2] = -(bw - 0.5)*2
            // b[0] = bw, b[1]=0, b[2] = -bw
            // a[1] = -(a[2]+1)*cos(w0)
            a[0] = 1.0;
            a[2] = -(bw - 0.5) * 2.0;

            b[0] = bw;
            b[1] = 0.0;
            b[2] = -bw;

            a[1] = -(a[2] + 1.0) * Math.Cos(w0);
        }

        public float Process(float inputSample)
        {
            double output = b[0] * inputSample
                          + b[1] * x1
                          + b[2] * x2
                          - a[1] * y1
                          - a[2] * y2;

            // сдвиг
            x2 = x1;
            x1 = inputSample;
            y2 = y1;
            y1 = (float)output;

            return (float)output;
        }

        public void Reset()
        {
            x1 = x2 = y1 = y2 = 0f;
        }
    }


    /// <summary>
    /// Дискретный фильтр 2-го порядка, реализующий функциональность параметрического эквалайзера, реализация взята из 
    ///  [Ekeroot, Jonas "Implementing a parametric EQ plug-in in C++ using the multi-platform VST specification" 
    ///  ISSN 1402-1773 / ISRN LTU-CUPP--03/044--SE / NR 2003:044]
    /// местоположение статьи: http://epubl.luth.se/1402-1773/2003/044/    
    /// Создает дискретный фильтр 2-го порядка 
    /// с центральной частотой CenterFrequency(Гц), усилением GainInDB (дБ)
    /// добротностью QFactor, с учетом частоты дискретизации SampleRate
    /// GainInDB>0 - усиление, GainInDB<0 - ослабление соответствующей частоты
    /// </summary>
    public class IIRParamEqFilter : IAudioFilter
    {
        // == Параметры ==
        public double SampleRate { get; private set; }
        public double CenterFrequency { get; private set; } // F0
        public double GainInDB { get; private set; }   // G dB
        public double QFactor { get; private set; }   // Q

        private double[] a = new double[3];
        private double[] b = new double[3];

        // Предыдущие отсчёты
        private float x1, x2, y1, y2;

        public IIRParamEqFilter(double sampleRate, double centerFrequency, double gainInDB, double qFactor)
        {
            SetParameters(sampleRate, centerFrequency, gainInDB, qFactor);
        }

        public void SetParameters(double sampleRate, double centerFrequency, double gainInDB, double qFactor)
        {
            SampleRate = sampleRate;
            CenterFrequency = centerFrequency;
            GainInDB = gainInDB;
            QFactor = qFactor;

            UpdateCoefficients();
        }

        private void UpdateCoefficients()
        {
            // A = 10^(G/40)
            // w = 2*pi*f0 / Fs
            // sn = sin(w), cs = cos(w)
            // alpha = sn/(2*Q)

            double A = Math.Pow(10.0, GainInDB / 40.0);
            double w = 2.0 * Math.PI * CenterFrequency / SampleRate;
            double sn = Math.Sin(w);
            double cs = Math.Cos(w);
            double alpha = sn / (2.0 * QFactor);

            // Расчет
            double a0 = 1.0 + alpha / A;
            double a0_inv = 1.0 / a0;

            a[0] = 1.0;
            a[1] = (-2.0 * cs) * a0_inv;
            a[2] = (1.0 - alpha / A) * a0_inv;

            b[0] = (1.0 + alpha * A) * a0_inv;
            b[1] = (-2.0 * cs) * a0_inv;
            b[2] = (1.0 - alpha * A) * a0_inv;
        }

        public float Process(float inputSample)
        {
            double output = b[0] * inputSample
                          + b[1] * x1
                          + b[2] * x2
                          - a[1] * y1
                          - a[2] * y2;

            // сдвиг
            x2 = x1;
            x1 = inputSample;
            y2 = y1;
            y1 = (float)output;

            return (float)output;
        }

        public void Reset()
        {
            x1 = x2 = y1 = y2 = 0f;
        }
    }
}
