using System.Collections.Generic;
using UnityEngine;

public class SkinAnimation : MonoBehaviour
{
    private static float[] TEMP_POSE_DATA0  = new float[128 * SkinAnimationClip.STRIDE];
    private static float[] TEMP_POSE_DATA1  = new float[128 * SkinAnimationClip.STRIDE];
    private static Vector4[] TEMP_DUAL_QUAT = new Vector4[128 * 2];
    
    public SkinAnimationClip[] clips = null;
    public SkinAvatar avatar = null;
    public System.Action OnPoseBufferUpdate = null;
    
    private int m_PoseDataID = Shader.PropertyToID("_PoseData");
    private MaterialPropertyBlock m_BufferBlock = null;
    
    private Animator m_Animator = null;
    private Dictionary<string, SkinAnimationClip> m_ClipsMap = null;
    private Dictionary<string, Transform> m_BonesMap = null;

    public MaterialPropertyBlock poseBuffer {
        get {
            return m_BufferBlock;
        }
    }

    private void Start()
    {
        m_Animator = GetComponent<Animator>();
        m_BufferBlock = new MaterialPropertyBlock();
        
        m_ClipsMap = new Dictionary<string, SkinAnimationClip>();
        for (int i = 0; i < clips.Length; ++i) {
            m_ClipsMap[clips[i].animName] = clips[i];
        }
        
        m_BonesMap = new Dictionary<string, Transform>();
        RecursiveBones(transform);
    }

    private void RecursiveBones(Transform root)
    {
        for (int i = 0; i < root.childCount; ++i)
        {
            Transform transform = root.GetChild(i);

            bool isBone = true;
            foreach (var comp in transform.GetComponents<Component>()) 
            {
                if (!(comp is Transform)) 
                {
                    isBone = false;
                    break;
                }
            }

            if (isBone)
            {
                m_BonesMap[transform.name] = transform;
                RecursiveBones(transform);
            }
        }
    }
    
    private float[] CalculateCurrentPose()
    {
        SkinAnimationClip currClip = null;
        float currBias = 1.0f;
        float currTime = 0.0f;
        if (m_Animator.GetCurrentAnimatorClipInfoCount(0) > 0)
        {
            var clipInfo  = m_Animator.GetCurrentAnimatorClipInfo(0)[0];
            var stateInfo = m_Animator.GetCurrentAnimatorStateInfo(0);
            currTime = stateInfo.normalizedTime * clipInfo.clip.length;
            currClip = m_ClipsMap[clipInfo.clip.name];
            currBias = clipInfo.weight;
        }
        
        SkinAnimationClip nextClip = null;
        float nextBias = 1.0f;
        float nextTime = 0.0f;
        if (m_Animator.GetNextAnimatorClipInfoCount(0) > 0)
        {
            var clipInfo  = m_Animator.GetNextAnimatorClipInfo(0)[0];
            var stateInfo = m_Animator.GetNextAnimatorStateInfo(0);
            nextTime = stateInfo.normalizedTime * clipInfo.clip.length;
            nextClip = m_ClipsMap[clipInfo.clip.name];
            nextBias = clipInfo.weight;
        }
        
        float[] finalPose = null;
        
        if (currClip != null && currBias > 0.0f) {
            currClip.Evaluate(Mathf.Repeat(currTime, currClip.length), TEMP_POSE_DATA0, false);
            finalPose = TEMP_POSE_DATA0;
        }
        
        if (nextClip != null && nextBias > 0.0f) {
            nextClip.Evaluate(Mathf.Repeat(nextTime, nextClip.length), TEMP_POSE_DATA1, false);
            finalPose = TEMP_POSE_DATA1;
        }
        
        if ((currClip != null && currBias > 0.0f) && (nextClip != null && nextBias > 0.0f))
        {
            float total = currBias + nextBias;
            currBias /= total;
            nextBias /= total;
            finalPose = TEMP_POSE_DATA0;
            for (int i = 1; i < avatar.bonesName.Length; ++i)
            {
                int idx = i * SkinAnimationClip.STRIDE;
                for (int j = 0; j < SkinAnimationClip.STRIDE; ++j)
                {
                    float a = TEMP_POSE_DATA0[idx + j];
                    float b = TEMP_POSE_DATA1[idx + j];
                    finalPose[idx + j] = a * currBias + b * nextBias;
                }
            }
        }
        
        return finalPose;
    }

    private void Update()
    {
        float[] finalPose = CalculateCurrentPose();
        
        if (finalPose == null) {
            return;
        }
        
        for (int i = 0; i < avatar.bonesName.Length; ++i) 
        {
            if (avatar.bonesParent[i] != -1) {
                CombineTwoJoint(finalPose, i, finalPose, avatar.bonesParent[i], finalPose, i);
            }
        }

        for (int i = 0; i < avatar.bonesName.Length; ++i)
        {
            CombineTwoJoint(avatar.invBindPose, i, finalPose, i, finalPose, i);
        }
        
        for (int i = 0; i < avatar.bonesName.Length; ++i)
        {
            int cpuBoneIndex = i * SkinAnimationClip.STRIDE;

            float px = finalPose[cpuBoneIndex + 0];
            float py = finalPose[cpuBoneIndex + 1];
            float pz = finalPose[cpuBoneIndex + 2];

            float rx = finalPose[cpuBoneIndex + 3];
            float ry = finalPose[cpuBoneIndex + 4];
            float rz = finalPose[cpuBoneIndex + 5];
            float rw = finalPose[cpuBoneIndex + 6];

            float dx = (+0.5f) * ( px * rw + py * rz - pz * ry);
            float dy = (+0.5f) * (-px * rz + py * rw + pz * rx);
            float dz = (+0.5f) * ( px * ry - py * rx + pz * rw);
            float dw = (-0.5f) * ( px * rx + py * ry + pz * rz);
            
            int gpuBoneIndex = i * 2;
            TEMP_DUAL_QUAT[gpuBoneIndex + 0].x = dx;
            TEMP_DUAL_QUAT[gpuBoneIndex + 0].y = dy;
            TEMP_DUAL_QUAT[gpuBoneIndex + 0].z = dz;
            TEMP_DUAL_QUAT[gpuBoneIndex + 0].w = dw;

            TEMP_DUAL_QUAT[gpuBoneIndex + 1].x = rx;
            TEMP_DUAL_QUAT[gpuBoneIndex + 1].y = ry;
            TEMP_DUAL_QUAT[gpuBoneIndex + 1].z = rz;
            TEMP_DUAL_QUAT[gpuBoneIndex + 1].w = rw;
        }
        
        m_BufferBlock.SetVectorArray(m_PoseDataID, TEMP_DUAL_QUAT);
        
        if (OnPoseBufferUpdate != null) {
            OnPoseBufferUpdate.Invoke();
        }
    }
    
    private static void CombineTwoJoint(float[] lhsData, int lhsIndex, float [] rhsData, int rhsIndex, float[] retData, int resIndex)
    {
        rhsIndex *= SkinAnimationClip.STRIDE;
        lhsIndex *= SkinAnimationClip.STRIDE;
        resIndex *= SkinAnimationClip.STRIDE;

        float parentTRX = rhsData[rhsIndex + 0];
        float parentTRY = rhsData[rhsIndex + 1];
        float parentTRZ = rhsData[rhsIndex + 2];

		float parentORX = rhsData[rhsIndex + 3];
		float parentORY = rhsData[rhsIndex + 4];
		float parentORZ = rhsData[rhsIndex + 5];
		float parentORW = rhsData[rhsIndex + 6];
        
		float childTRX = lhsData[lhsIndex + 0];
		float childTRY = lhsData[lhsIndex + 1];
		float childTRZ = lhsData[lhsIndex + 2];

        float childORX = lhsData[lhsIndex + 3];
		float childORY = lhsData[lhsIndex + 4];
		float childORZ = lhsData[lhsIndex + 5];
		float childORW = lhsData[lhsIndex + 6];

		float w1 = -parentORX * childTRX - parentORY * childTRY - parentORZ * childTRZ;
		float x1 =  parentORW * childTRX + parentORY * childTRZ - parentORZ * childTRY;
		float y1 =  parentORW * childTRY - parentORX * childTRZ + parentORZ * childTRX;
		float z1 =  parentORW * childTRZ + parentORX * childTRY - parentORY * childTRX;
        
		retData[resIndex + 0] = -w1 * parentORX + x1 * parentORW - y1 * parentORZ + z1 * parentORY + parentTRX;
		retData[resIndex + 1] = -w1 * parentORY + x1 * parentORZ + y1 * parentORW - z1 * parentORX + parentTRY;
		retData[resIndex + 2] = -w1 * parentORZ - x1 * parentORY + y1 * parentORX + z1 * parentORW + parentTRZ;
        
		retData[resIndex + 6] = parentORW * childORW - parentORX * childORX - parentORY * childORY - parentORZ * childORZ;
		retData[resIndex + 3] = parentORW * childORX + parentORX * childORW + parentORY * childORZ - parentORZ * childORY;
		retData[resIndex + 4] = parentORW * childORY - parentORX * childORZ + parentORY * childORW + parentORZ * childORX;
		retData[resIndex + 5] = parentORW * childORZ + parentORX * childORY - parentORY * childORX + parentORZ * childORW;
    }
}
