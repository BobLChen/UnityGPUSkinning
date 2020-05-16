using UnityEngine;

public class SkinAnimationClip : ScriptableObject
{
    public static readonly int STRIDE = 7;
    
    public string animName = string.Empty;
    public int nodeCount = 0;
    public float frequency = 1.0f / 30.0f;
    public float length = 0.0f;
    public float[] times = null;
    public float[] datas = null;

    public SkinAnimationClip()
    {
        
    }
    
    public void Evaluate(float time, float[] pose, bool lerp)
    {
        int index0 = 0;
        int index1 = 0;
        
        if (times.Length == 1)
        {
            lerp = false;
            index0 = 0;
            index1 = 0;
        }
        else if (time <= 0.0f) 
        {
            lerp = false;
            index0 = 0;
            index1 = 0;
        }
        else if (time >= length) 
        {
            lerp = false;
            index0 = times.Length - 1;
            index1 = times.Length - 1;
        }
        else
        {
            index0 = (int)(time / frequency);
            index1 = index0 + 1;
        }
        
        float time0 = index0 * frequency;
        float time1 = index1 * frequency;
        
        index0 *= STRIDE;
        index1 *= STRIDE;
        
        if (!lerp)
        {
            // Nearest
            int index = (time - time0) < (time1 - time) ? index0 : index1;

            for (int i = 0; i < nodeCount; ++i)
            {
                int animIndex = i * times.Length * STRIDE;
                int poseIndex = i * STRIDE;
                
                for (int j = 0; j < STRIDE; ++j)
                {
                    pose[poseIndex + j] = datas[animIndex + index + j];
                }
            }
        }
        else
        {
            float t = (time - time0) / (time1 - time0);

            // Linear
            for (int i = 0; i < nodeCount; ++i)
            {
                int animIndex = i * times.Length * STRIDE;
                int poseIndex = i * STRIDE;
            
                for (int j = 0; j < STRIDE; ++j)
                {
                    float a = datas[animIndex + index0 + j];
                    float b = datas[animIndex + index1 + j];
                    float v = a + (b - a) * t;
                    pose[poseIndex + j] = v;
                }
            }
        }
        
    }

}
