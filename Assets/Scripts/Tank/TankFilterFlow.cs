using FishBreeder.Core;
using UnityEngine;

namespace FishBreeder
{
    /// <summary>
    /// The water a running filter moves: it pulls water in at the mouth of the intake tube at the
    /// bottom, and pours it back into the tank over the rim, where it splashes and sends bubbles
    /// down. Switched off while the filter is broken (the tank disables this component).
    /// </summary>
    public class TankFilterFlow : MonoBehaviour
    {
        const int Drops = 9, Splashes = 8, Motes = 10;

        Transform[] drops, splashes, motes;
        float[] dropPhase, splashPhase, motePhase;
        Vector3[] moteFrom;
        Vector3 spout, splashPoint, intake;
        float fallHeight, moteReach;

        /// <summary>Everything is in the tank's visuals space: where the water pours from, where the tube's mouth is, and the water's surface.</summary>
        public void Initialize(Vector3 spoutPoint, Vector3 intakePoint, float waterTop)
        {
            spout = spoutPoint;
            intake = intakePoint;
            splashPoint = new Vector3(spout.x, waterTop, spout.z);
            fallHeight = Mathf.Max(0.02f, spout.y - waterTop);
            moteReach = Mathf.Max(0.08f, fallHeight * 3f);

            var waterColor = new Color(0.78f, 0.92f, 1f, 0.55f);
            var pour = MaterialFactory.Transparent(waterColor);
            var bubble = MaterialFactory.Transparent(new Color(0.9f, 0.97f, 1f, 0.75f));

            // The stream itself, a thin sheet from the lip down to the surface.
            var sheet = MaterialFactory.Decor(PrimitiveType.Cube, "Pour", transform, new Vector3(spout.x, spout.y - fallHeight * 0.5f, spout.z), new Vector3(0.05f, fallHeight, 0.018f), Color.white);
            sheet.GetComponent<Renderer>().sharedMaterial = pour;

            drops = new Transform[Drops];
            dropPhase = new float[Drops];
            for (int i = 0; i < Drops; i++)
            {
                var drop = MaterialFactory.Decor(PrimitiveType.Sphere, "Drop", transform, spout, new Vector3(0.012f, 0.02f, 0.012f), Color.white);
                drop.GetComponent<Renderer>().sharedMaterial = pour;
                drops[i] = drop.transform;
                dropPhase[i] = i / (float)Drops;
            }

            splashes = new Transform[Splashes];
            splashPhase = new float[Splashes];
            for (int i = 0; i < Splashes; i++)
            {
                var splash = MaterialFactory.Decor(PrimitiveType.Sphere, "Splash", transform, splashPoint, Vector3.one * 0.01f, Color.white);
                splash.GetComponent<Renderer>().sharedMaterial = bubble;
                splashes[i] = splash.transform;
                splashPhase[i] = i / (float)Splashes;
            }

            // Specks of water drawn in at the tube's mouth, to show the pull.
            motes = new Transform[Motes];
            motePhase = new float[Motes];
            moteFrom = new Vector3[Motes];
            for (int i = 0; i < Motes; i++)
            {
                var mote = MaterialFactory.Decor(PrimitiveType.Sphere, "Intake", transform, intake, Vector3.one * 0.012f, Color.white);
                mote.GetComponent<Renderer>().sharedMaterial = bubble;
                motes[i] = mote.transform;
                motePhase[i] = i / (float)Motes;
                moteFrom[i] = Start(i);
            }
        }

        /// <summary>Where a speck starts its run into the tube's mouth: spread around it, mostly below.</summary>
        Vector3 Start(int i)
        {
            float angle = GameRandom.Range(0f, Mathf.PI * 2f);
            float reach = moteReach * GameRandom.Range(0.6f, 1f);
            return intake + new Vector3(Mathf.Cos(angle) * reach, -GameRandom.Range(0.1f, 0.8f) * reach, Mathf.Sin(angle) * reach);
        }

        void OnEnable() => Show(true);
        void OnDisable() => Show(false);

        void Show(bool show)
        {
            foreach (var t in transform.GetComponentsInChildren<Transform>(true))
                if (t != transform && (t.name == "Pour" || t.name == "Drop" || t.name == "Splash" || t.name == "Intake"))
                    t.gameObject.SetActive(show);
        }

        void Update()
        {
            if (drops == null) return;
            float dt = Time.deltaTime, time = Time.time;

            for (int i = 0; i < drops.Length; i++)
            {
                dropPhase[i] += dt * 2.6f;
                if (dropPhase[i] > 1f) dropPhase[i] -= 1f;
                float t = dropPhase[i];
                var p = Vector3.Lerp(spout, splashPoint, t * t);
                p.x += Mathf.Sin(time * 7f + i) * 0.008f;
                drops[i].localPosition = p;
                drops[i].localScale = new Vector3(0.012f, 0.02f + t * 0.012f, 0.012f);
            }

            for (int i = 0; i < splashes.Length; i++)
            {
                splashPhase[i] += dt * GameRandom.Range(0.8f, 0.9f);
                if (splashPhase[i] > 1f)
                {
                    splashPhase[i] -= 1f;
                    splashes[i].localPosition = splashPoint;
                }
                float t = splashPhase[i];
                float angle = i * 2.39996f;
                var p = splashPoint + new Vector3(Mathf.Cos(angle) * t * 0.09f, -t * fallHeight * 1.4f, Mathf.Sin(angle) * t * 0.05f);
                splashes[i].localPosition = p;
                splashes[i].localScale = Vector3.one * Mathf.Lerp(0.014f, 0.002f, t);
            }

            for (int i = 0; i < motes.Length; i++)
            {
                motePhase[i] += dt * 0.55f;
                if (motePhase[i] > 1f)
                {
                    motePhase[i] -= 1f;
                    moteFrom[i] = Start(i);
                }
                float t = motePhase[i];
                // Faster the closer it gets, as the pull grows towards the mouth.
                motes[i].localPosition = Vector3.Lerp(moteFrom[i], intake, t * t);
                motes[i].localScale = Vector3.one * Mathf.Lerp(0.012f, 0.002f, t * t);
            }
        }
    }
}
