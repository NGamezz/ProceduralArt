using Unity.Mathematics;
using UnityEngine;

public class Synth : MonoBehaviour
{
    [SerializeField] private float frequency = 440;
    [SerializeField, Range(0, 1)] private float gain;

    private int sampleRate = 48000;
    private double phase;

    [SerializeField] private bool[] channelsActive;

    public void SetFrequency ( float fr )
    {
        frequency = fr;
    }

    public double GetFrequency ()
    {
        return frequency;
    }

    public void IncrementFrequency ( float fr, float2 bounds )
    {
        if ( frequency + fr <= bounds.x || frequency + fr >= bounds.y )
            return;

        frequency += fr;
    }

    private void Awake ()
    {
        sampleRate = AudioSettings.outputSampleRate;
        channelsActive = new bool[32];

        for ( int i = 0; i < channelsActive.Length; i++ )
        {
            channelsActive[i] = true;
        }
    }

    public void SetChannelStatus ( bool state, int index )
    {
        if ( index >= channelsActive.Length )
            return;

        channelsActive[index] = state;
    }

    public void SetGain ( float gain )
    {
        if ( gain > 1.0f )
        {
            gain = 1.0f;
        }
        else if ( gain < 0.0f )
        {
            gain = 0.0f;
        }

        this.gain = gain;
    }

    private void OnAudioFilterRead ( float[] data, int channels )
    {
        double increment = frequency / sampleRate;

        for ( int i = 0; i < data.Length; i += channels )
        {
            float value = math.sin((float)phase * 2 * math.PI) * gain;

            phase = (increment + phase) % 1;

            for ( int t = 0; t < channels; t++ )
            {
                if ( channelsActive[t] == false )
                    continue;

                data[i + t] = value;
            }
        }
    }
}
