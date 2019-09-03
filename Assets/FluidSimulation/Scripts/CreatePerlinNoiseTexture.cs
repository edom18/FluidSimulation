using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreatePerlinNoiseTexture
{
    static private float xOrg = 0;
    static private float yOrg = 0;

    static public Texture2D Create(int width, int height, float min, float max, float scale = 1.0f)
    {
        Texture2D noiseTex = new Texture2D(width, height);

        Color[] pix = new Color[noiseTex.width * noiseTex.height];

        float y = 0.0f;

        while (y < noiseTex.height)
        {
            float x = 0.0f;

            while (x < noiseTex.width)
            {
                float[] samples = new float[3];

                for (int i = 0; i < 3; i++)
                {
                    float xCoord = xOrg + (x / noiseTex.width * scale) + (125f * i);
                    float yCoord = yOrg + (y / noiseTex.height * scale) + (3021f * i);
                    float sample = Mathf.PerlinNoise(xCoord, yCoord);
                    samples[i] =  Mathf.Lerp(min, max, sample);
                }

                pix[(int)y * noiseTex.width + (int)x] = new Color(samples[0], samples[1], samples[2]);

                x++;
            }

            y++;
        }

        noiseTex.SetPixels(pix);
        noiseTex.Apply();

        pix = null;

        return noiseTex;
    }
}
