using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    [SerializeField] private Synth[] synth;

    [SerializeField] private int desiredAmountOfSynths = 5;

    [SerializeField] private float2 frequencyRange;
    [SerializeField] private float2 gainRange;

    [SerializeField] private Transform synthHolder;

    [SerializeField] private float duration;
    [SerializeField] private float increment;

    void Start ()
    {
        synth = new Synth[desiredAmountOfSynths];

        for ( int i = 0; i < desiredAmountOfSynths; i++ )
        {
            var newObject = new GameObject($"Synth : {i}");
            newObject.transform.parent = synthHolder;

            synth[i] = newObject.AddComponent<Synth>();
            var audioSource = newObject.AddComponent<AudioSource>();
            audioSource.panStereo = UnityEngine.Random.Range(-1.0f, 1.0f);
        }

        for ( int i = 0; i < synth.Length; i++ )
        {
            synth[i].SetFrequency(UnityEngine.Random.Range(frequencyRange.x, frequencyRange.y));
            synth[i].SetGain(UnityEngine.Random.Range(gainRange.x, gainRange.y));

            IncrementFrequencyAynsc(synth[i]);
        }
    }

    private async void IncrementFrequencyAynsc ( Synth synth )
    {
        float timer = 0;

        int rand = UnityEngine.Random.Range(-1, 2);

        synth.SetGain(UnityEngine.Random.Range(0, 0.5f));

        var inc = increment * rand;

        for ( int i = 0; i < 2; i++ )
        {
            var ran = UnityEngine.Random.Range(0, 2);
            synth.SetChannelStatus(ran > 0, i);
        }

        while ( timer < duration )
        {
            timer += Time.deltaTime;

            inc += inc * 0.5f * Time.deltaTime;

            synth.IncrementFrequency(inc, frequencyRange);

            await Awaitable.NextFrameAsync();
        }

        IncrementFrequencyAynsc(synth);
    }

    private IEnumerator IncrementFrequency ( Synth synth )
    {
        float timer = 0;

        int rand = UnityEngine.Random.Range(-1, 2);

        var inc = increment * rand;

        while ( timer < duration )
        {
            timer += Time.deltaTime;

            synth.IncrementFrequency(inc, frequencyRange);

            yield return null;
        }

        StartCoroutine(IncrementFrequency(synth));
    }
}